using System.Reflection;
using System.Text;
using Pulumi;
using Pulumi.Oci.Budget;
using Pulumi.Oci.Core;
using Pulumi.Oci.Core.Inputs;
using Pulumi.Oci.Identity;

return await Deployment.RunAsync(() =>
{
    var config = new Config();

    var tenancyOcid = config.Require("ociTenancyOcid");
    var domain = config.Require("domain");
    var sshPublicKey = config.Require("sshPublicKey");
    var apiKey = config.RequireSecret("apiKey");
    var imageTag = config.Get("imageTag");
    if (string.IsNullOrWhiteSpace(imageTag))
    {
        imageTag = "latest";
    }
    var alertEmail = config.Require("alertEmail");
    // Administrator CIDR allowed to reach SSH (port 22). Leave unset (or set
    // to an empty value) to omit the SSH ingress rule entirely.
    var sshAdminCidr = config.Get("sshAdminCidr");
    var hubImage = $"ghcr.io/anton-makarevich/sanet.transport/hub:{imageTag}";

    // ------------------------------------------------------------------
    // Compartment
    // ------------------------------------------------------------------
    var compartment = new Compartment("hub-compartment", new CompartmentArgs
    {
        CompartmentId = tenancyOcid,
        Name = "makamek-hub",
        Description = "MakaMek SignalR relay hub (Always Free)",
    });

    // ------------------------------------------------------------------
    // Networking: VCN + Internet Gateway + public subnet + security list
    // ------------------------------------------------------------------
    var vcn = new Vcn("hub-vcn", new VcnArgs
    {
        CompartmentId = compartment.Id,
        CidrBlocks = { "10.0.0.0/16" },
        DisplayName = "makamek-hub-vcn",
        DnsLabel = "makamekhub",
    });

    var internetGateway = new InternetGateway("hub-igw", new InternetGatewayArgs
    {
        CompartmentId = compartment.Id,
        VcnId = vcn.Id,
        DisplayName = "makamek-hub-igw",
        Enabled = true,
    });

    var defaultRouteTable = new DefaultRouteTable("hub-route-table", new DefaultRouteTableArgs
    {
        CompartmentId = compartment.Id,
        ManageDefaultResourceId = vcn.Id,
        RouteRules =
        {
            new DefaultRouteTableRouteRuleArgs
            {
                Destination = "0.0.0.0/0",
                DestinationType = "CIDR_BLOCK",
                NetworkEntityId = internetGateway.Id,
            },
        },
    });

    var ingressRules = new List<SecurityListIngressSecurityRuleArgs>
    {
        new()
        {
            Protocol = "6",
            Source = "0.0.0.0/0",
            SourceType = "CIDR_BLOCK",
            TcpOptions = new SecurityListIngressSecurityRuleTcpOptionsArgs
            {
                Min = 80,
                Max = 80,
            },
            Description = "HTTP (ACME challenge)",
        },
        new()
        {
            Protocol = "6",
            Source = "0.0.0.0/0",
            SourceType = "CIDR_BLOCK",
            TcpOptions = new SecurityListIngressSecurityRuleTcpOptionsArgs
            {
                Min = 443,
                Max = 443,
            },
            Description = "HTTPS / WSS",
        },
    };
    if (!string.IsNullOrWhiteSpace(sshAdminCidr))
    {
        ingressRules.Insert(0, new SecurityListIngressSecurityRuleArgs
        {
            Protocol = "6",
            Source = sshAdminCidr,
            SourceType = "CIDR_BLOCK",
            TcpOptions = new SecurityListIngressSecurityRuleTcpOptionsArgs
            {
                Min = 22,
                Max = 22,
            },
            Description = $"SSH (admin {sshAdminCidr})",
        });
    }

    // 22 (SSH, admin CIDR only when configured), 80 (ACME HTTP-01), 443 (HTTPS/WSS)
    var securityList = new SecurityList("hub-security-list", new SecurityListArgs
    {
        CompartmentId = compartment.Id,
        VcnId = vcn.Id,
        DisplayName = "makamek-hub-sl",
        EgressSecurityRules =
        {
            new SecurityListEgressSecurityRuleArgs
            {
                Protocol = "all",
                Destination = "0.0.0.0/0",
                DestinationType = "CIDR_BLOCK",
                Description = "Allow all egress",
            },
        },
        IngressSecurityRules = ingressRules,
    });

    var subnet = new Subnet("hub-subnet", new SubnetArgs
    {
        CompartmentId = compartment.Id,
        VcnId = vcn.Id,
        CidrBlock = "10.0.1.0/24",
        DisplayName = "makamek-hub-public",
        DnsLabel = "hublic",
        RouteTableId = defaultRouteTable.Id,
        SecurityListIds = { securityList.Id },
    });

    // ------------------------------------------------------------------
    // Budget tripwire: Always Free A1 is $0, so this must never trigger.
    // If it does, something billable leaked into the compartment.
    // ------------------------------------------------------------------
    var budget = new Budget("hub-budget", new BudgetArgs
    {
        CompartmentId = compartment.Id,
        TargetCompartmentId = compartment.Id,
        Amount = 1,
        ResetPeriod = "MONTHLY",
        BudgetProcessingPeriodStartOffset = 1,
        DisplayName = "makamek-hub-budget",
    });

    _ = new Rule("hub-budget-alert", new RuleArgs
    {
        BudgetId = budget.Id,
        Type = "ACTUAL",
        ThresholdType = "ABSOLUTE",
        Threshold = 1,
        Recipients = alertEmail,
        Message =
            "The relay hub compartment has incurred cost. All resources here are supposed to be Always Free.",
        DisplayName = "any-spend",
    });

    // ------------------------------------------------------------------
    // Compute instance (A1.Flex ARM).
    // WARNING: Oracle's Always Free allowance for A1 is 2 OCPU / 12 GB per
    // TENANCY (halved from 4/24 in June 2026). Do not increase these values
    // or add more A1 instances without checking the free-tier quota.
    // ------------------------------------------------------------------
    var availabilityDomain = compartment.Id.Apply(cid =>
        GetAvailabilityDomains.Invoke(new GetAvailabilityDomainsInvokeArgs
        {
            CompartmentId = cid,
        }).Apply(ads => ads.AvailabilityDomains.Length > 0
            ? ads.AvailabilityDomains[0].Name
            : throw new InvalidOperationException(
                "No availability domains found in compartment.")));

    // Latest Ubuntu 22.04 aarch64 image compatible with the A1 shape.
    var ubuntuImage = compartment.Id.Apply(cid =>
        GetImages.Invoke(new GetImagesInvokeArgs
        {
            CompartmentId = cid,
            OperatingSystem = "Canonical Ubuntu",
            OperatingSystemVersion = "22.04",
            Shape = "VM.Standard.A1.Flex",
            SortBy = "TIMECREATED",
            SortOrder = "DESC",
        }).Apply(images => images.Images.Length > 0
            ? images.Images[0].Id
            : throw new InvalidOperationException(
                "No Ubuntu 22.04 A1-compatible image found.")));

    var userData = RenderCloudInit(domain, apiKey, hubImage);

    var instance = new Instance("hub-instance", new InstanceArgs
    {
        AvailabilityDomain = availabilityDomain,
        CompartmentId = compartment.Id,
        Shape = "VM.Standard.A1.Flex",
        ShapeConfig = new InstanceShapeConfigArgs
        {
            Ocpus = 2,
            MemoryInGbs = 12,
        },
        SourceDetails = new InstanceSourceDetailsArgs
        {
            SourceType = "image",
            SourceId = ubuntuImage,
        },
        SubnetId = subnet.Id,
        DisplayName = "makamek-hub-vm",
        Metadata =
        {
            ["ssh_authorized_keys"] = sshPublicKey,
            ["user_data"] = userData.Apply(data => Convert.ToBase64String(Encoding.UTF8.GetBytes(data))),
        },
        CreateVnicDetails = new InstanceCreateVnicDetailsArgs
        {
            AssignPublicIp = "false", // reserved IP is attached below
            HostnameLabel = "makamek-hub",
        },
    });

    // Reserved public IP so the DNS target survives instance recreation.
    var primaryVnicId = Output.Tuple(compartment.Id, instance.Id).Apply(values =>
        GetVnicAttachments.Invoke(new GetVnicAttachmentsInvokeArgs
        {
            CompartmentId = values.Item1,
            InstanceId = values.Item2,
        }).Apply(attachments => attachments.VnicAttachments.Length > 0
            ? attachments.VnicAttachments[0].VnicId
            : throw new InvalidOperationException("Instance has no VNIC attachment.")));

    var privateIp = primaryVnicId.Apply(vnicId =>
        GetPrivateIps.Invoke(new GetPrivateIpsInvokeArgs
        {
            VnicId = vnicId,
        }).Apply(ips => ips.PrivateIps.Length > 0
            ? ips.PrivateIps[0].Id
            : throw new InvalidOperationException("Primary VNIC has no private IP.")));

    var reservedIp = new PublicIp("hub-reserved-ip", new PublicIpArgs
    {
        CompartmentId = compartment.Id,
        Lifetime = "RESERVED",
        PrivateIpId = privateIp,
        DisplayName = "makamek-hub-ip",
    });

    return new Dictionary<string, object?>
    {
        ["publicIp"] = reservedIp.IpAddress,
        ["instanceOcid"] = instance.Id,
        ["compartmentOcid"] = compartment.Id,
        ["healthUrl"] = Output.Format($"https://{domain}/health"),
    };
});

// Renders the cloud-init user-data by substituting placeholders in the embedded templates.
static Output<string> RenderCloudInit(string domain, Output<string> apiKey, string hubImage)
{
    var dockerCompose = ReadTemplate("docker-compose.yml.tpl")
        .Replace("__HUB_IMAGE__", hubImage);
    var caddyfile = ReadTemplate("Caddyfile.tpl").Replace("__DOMAIN__", domain);

    return apiKey.Apply(key => ReadTemplate("cloud-init.yaml.tpl")
        .Replace("__DOCKER_COMPOSE__", Indent(dockerCompose, 6))
        .Replace("__CADDYFILE__", Indent(caddyfile, 6))
        .Replace("__HUB_API_KEY__", key));
}

static string ReadTemplate(string name)
{
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = $"{assembly.GetName().Name}.Templates.{name}";
    using var stream = assembly.GetManifestResourceStream(resourceName)
        ?? throw new FileNotFoundException($"Embedded template '{resourceName}' not found.");
    using var reader = new StreamReader(stream);
    // Normalize line endings: these files are written onto a Linux VM by cloud-init.
    return reader.ReadToEnd().Replace("\r\n", "\n").TrimEnd();
}

// Indents every non-empty line of the block so it nests inside YAML.
static string Indent(string text, int spaces)
{
    var pad = new string(' ', spaces);
    var lines = text.Split('\n');
    for (var i = 0; i < lines.Length; i++)
    {
        lines[i] = lines[i].Length == 0 ? lines[i] : pad + lines[i];
    }
    return string.Join('\n', lines);
}

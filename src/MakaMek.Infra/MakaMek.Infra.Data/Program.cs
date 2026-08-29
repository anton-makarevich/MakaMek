using Pulumi;
using Pulumi.Cloudflare;

return await Deployment.RunAsync(() =>
{
    var config = new Pulumi.Config();

    // Account id comes from stack config (pulumi config set accountId ...)
    // or falls back to the CLOUDFLARE_ACCOUNT_ID env injected by the
    // data-infra workflow / local shell.
    var accountId = config.Get("accountId")
        ?? Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID")
        ?? throw new InvalidOperationException(
            "accountId is required: run 'pulumi config set accountId <id>' or export CLOUDFLARE_ACCOUNT_ID.");
    var bucketName = config.Get("bucketName") ?? "makamek-data";
    // R2 location hint (jurisdiction): optional; valid values are
    // "apac", "enam", "wnam", "weur", "eeur". Unset lets Cloudflare pick.
    var locationHint = config.Get("locationHint");
    // Public custom domain under the makamek.nl zone that serves the bucket.
    var customDomain = config.Get("customDomain") ?? "data.makamek.nl";
    // The DNS stack (MakaMek.Infra.Dns) owns the makamek.nl zone and exposes
    // its zoneId as a stack output. We reference it cross-stack so the R2
    // custom domain can be provisioned here without duplicating zone state.
    var dnsStackName = config.Require("dnsStack");
    var dnsStack = new StackReference("makamek-dns-stack", new StackReferenceArgs
    {
        Name = $"{Deployment.Instance.OrganizationName}/makamek-infra-dns/{dnsStackName}",
    });
    var zoneId = dnsStack.RequireOutput("zoneId").Apply(v => (string)v);

    // ------------------------------------------------------------------
    // R2 bucket: flat mirror of the repository data/ folder at the last
    // released tag. Written only by the deploy-data-release workflow
    // (S3 API credentials), read publicly via the custom domain below.
    // ------------------------------------------------------------------
    var bucket = new R2Bucket("data-bucket", new R2BucketArgs
    {
        AccountId = accountId,
        Name = bucketName,
        Location = locationHint,
    });

    // The custom domain (data.makamek.nl) is created against a zone in the
    // same Cloudflare account (the Dns stack's zone). Cloudflare owns the
    // resulting CNAME record and TLS certificate for the hostname, so the
    // Dns stack must not define any conflicting record at data.makamek.nl.
    var r2CustomDomain = new R2CustomDomain("data-custom-domain", new R2CustomDomainArgs
    {
        AccountId = accountId,
        BucketName = bucket.Name,
        Domain = customDomain,
        Enabled = true,
        ZoneId = zoneId,
        Jurisdiction = locationHint,
    });

    return new Dictionary<string, object?>
    {
        ["bucketName"] = bucket.Name,
        ["customDomain"] = r2CustomDomain.Domain,
        // Public base URL for the deploy-data-release workflow; set the repo
        // variable vars.DATA_R2_BASE_URL to this value.
        ["baseUrl"] = Output.Format($"https://{r2CustomDomain.Domain}"),
        // S3 API endpoint used by the deploy-data-release workflow
        // (aws s3 sync --endpoint-url <s3Endpoint>).
        ["s3Endpoint"] = Output.Format($"https://{accountId}.r2.cloudflarestorage.com"),
        // After the first 'up': enable public access (dashboard -> bucket ->
        // Settings -> Public Access) and set the repo variable
        // vars.DATA_R2_BASE_URL to the baseUrl output above. Then create an R2
        // API token (S3 credentials) for the CI upload and store it as the repo
        // secrets R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY alongside
        // CLOUDFLARE_R2_BUCKET. The Cloudflare API does not expose S3 token
        // creation, so this pairing is set up manually once.
    };
});

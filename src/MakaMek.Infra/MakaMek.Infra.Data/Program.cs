using Pulumi;
using Pulumi.Cloudflare;
using Pulumi.Cloudflare.Inputs;

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
    // Email address that receives the R2 spend alert. The alert fires once per
    // billing cycle when Cloudflare's projected monthly R2 spend crosses the
    // configured threshold. Value lives in stack config (Pulumi.prod.yaml); it
    // is a public contact point, so keeping it plaintext is fine.
    var budgetAlertEmail = config.Get("budgetAlertEmail")
        ?? throw new InvalidOperationException(
            "budgetAlertEmail is required to create the R2 spend alert: " +
            "set makamek-infra-data:budgetAlertEmail in Pulumi.prod.yaml.");
    // Dollar threshold at which the spend alert fires. Default: $5.
    var budgetUsd = config.Get("budgetUsd") ?? "5";

    // ------------------------------------------------------------------
    // R2 bucket: flat mirror of the repository data/ folder at the last
    // released tag. Written only by the deploy-data-release workflow
    // (S3 API credentials), read publicly via a custom domain or r2.dev.
    // ------------------------------------------------------------------
    var bucket = new R2Bucket("data-bucket", new R2BucketArgs
    {
        AccountId = accountId,
        Name = bucketName,
        Location = locationHint??"weur",
    });

    // ------------------------------------------------------------------
    // R2 spend budget alert: Cloudflare "Usage Based Billing" alerts are
    // informational only - there is no API to enforce a hard spend cap.
    // Cloudflare emails the configured address when projected monthly R2
    // spend crosses the threshold (once per billing cycle). Requires the
    // API token used by the infra-data workflow to have the
    // "Notifications:Write" (Account Settings) permission.
    // ------------------------------------------------------------------
    _ = new NotificationPolicy("r2-spend-alert", new NotificationPolicyArgs
    {
        AccountId = accountId,
        Name = "MakaMek R2 spend budget alert",
        Description =
            "Emails when projected monthly R2 spend crosses the configured budget threshold.",
        AlertType = "billing_usage_alert",
        Enabled = true,
        Mechanisms = new NotificationPolicyMechanismsArgs
        {
            Emails =
            {
                new NotificationPolicyMechanismsEmailArgs { Id = budgetAlertEmail },
            },
        },
        Filters = new NotificationPolicyFiltersArgs
        {
            Products = { "r2" },
            Limits = { budgetUsd },
        },
    });

    return new Dictionary<string, object?>
    {
        ["bucketName"] = bucket.Name,
        // S3 API endpoint used by the deploy-data-release workflow
        // (aws s3 sync --endpoint-url <s3Endpoint>).
        ["s3Endpoint"] = Output.Format($"https://{accountId}.r2.cloudflarestorage.com"),
        // After the first 'up': enable public access (dashboard -> bucket ->
        // Settings -> Public Access, custom domain or r2.dev) and set the repo
        // variable vars.DATA_R2_BASE_URL to that public base URL. Then create an
        // R2 API token (S3 credentials) for the CI upload and store it as the
        // repo secrets R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY alongside
        // CLOUDFLARE_R2_BUCKET. The Cloudflare API does not expose S3 token
        // creation, so this pairing is set up manually once.
    };
});

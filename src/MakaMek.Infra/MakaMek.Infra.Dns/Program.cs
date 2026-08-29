using Pulumi;
using Pulumi.Cloudflare;

return await Deployment.RunAsync(() =>
{
    var config = new Pulumi.Config();

    var zoneName = config.Get("zoneName") ?? "makamek.nl";

    // The Cloudflare provider is configured solely by the CLOUDFLARE_API_TOKEN /
    // CLOUDFLARE_ACCOUNT_ID env vars (see infra-dns.yml workflow). The Zone is
    // created as a full/primary zone in the account that issued that token.
    var zone = new Zone("makamek-zone", new ZoneArgs
    {
        Name = zoneName,
        Type = "full",
    });

    // Each entry: { "name": "www.makamek.nl", "type": "A", "content": "64.29.17.1",
    //               "proxied": false, "ttl": 1 }
    // name is the fully-qualified record name (or @ for the apex). All records
    // are created DNS-only (grey cloud, proxied=false) so behaviour is identical
    // to the previous Vercel-hosted DNS while the zone is cut over. Flip proxied
    // to true per-record once ready to enable Cloudflare security/caching.
    var recordsConfig = config.GetObject<RecordDefinition[]>("records") ?? [];

    foreach (var (record, i) in recordsConfig.Select((r, i) => (r, i)))
    {
        var name = record.Name == "@" ? zoneName : $"{record.Name}.{zoneName}";
        new DnsRecord($"record-{i}", new DnsRecordArgs
        {
            ZoneId = zone.Id,
            Name = name,
            Type = record.Type,
            Content = record.Content,
            Proxied = record.Proxied,
            Ttl = record.Ttl ?? 1,
        });
    }

    return new Dictionary<string, object?>
    {
        ["zoneName"] = zoneName,
        ["zoneId"] = zone.Id,
        // The Cloudflare-assigned nameservers. Point the registrar (vimexx.nl)
        // at these to hand DNS custody to Cloudflare.
        ["nameServers"] = zone.NameServers,
    };
});

internal sealed record RecordDefinition
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Content { get; init; } = "";
    public bool Proxied { get; init; }
    public int? Ttl { get; init; }
}

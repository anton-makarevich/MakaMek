using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Sanet.MakaMek.Hub.Tests;

public sealed class HubApplicationFactory(int maxConcurrentRooms = 10) : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hub:ApiKey"] = ApiKey,
                ["Hub:MaxConcurrentRooms"] = maxConcurrentRooms.ToString()
            });
        });
    }
}

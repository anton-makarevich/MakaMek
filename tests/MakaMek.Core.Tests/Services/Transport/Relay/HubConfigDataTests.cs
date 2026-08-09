using Sanet.MakaMek.Core.Services.Transport.Relay;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

public class HubConfigDataTests
{
    [Fact]
    public void ToString_IncludesIdentityAndMasksApiKey()
    {
        var hub = new HubConfigData("custom-1", "My Hub", "http://my-hub.example", "secret-key", IsBuiltIn: false);

        var result = hub.ToString();

        result.ShouldContain("custom-1");
        result.ShouldContain("My Hub");
        result.ShouldContain("http://my-hub.example");
        result.ShouldContain("********");
        result.ShouldNotContain("secret-key");
    }
}

namespace MakaMek.Tools.BotContainer.Configuration;

public class BotConfiguration
{
    public string ServerUrl { get; set; } = "http://localhost:2439/makamekhub";
    public string BotName { get; set; } = "IntegrationBot";
    public string BotTeam { get; set; } = "#FF0000";
    public List<string> Units { get; set; } = [];
}

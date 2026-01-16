# MakaMek Integration Bot Container

This project hosts the integration bot logic as a standalone service. It connects to a MakaMek game server and runs a bot player using the existing decision engines.

## Prerequisites

- .NET 10 SDK
- Running MakaMek Game Server (usually started via `MakaMek Desktop` application, see AvaloniaUI implementation)

## Configuration

Configuration is managed via `appsettings.json`.

```json
{
  "BotConfiguration": {
    "ServerUrl": "http://localhost:2439/makamekhub",
    "BotName": "IntegrationBot_01",
    "BotTeam": "#00FF00",
    "Units": [
      {
        "Name": "WSP-1A.mtf"
      }
    ]
  }
}
```

- **ServerUrl**: The URL of the SignalR hub on the game server.
- **BotName**: The display name of the bot.
- **BotTeam**: Color tint for the bot's units.
- **Units**: List of units to deploy.

## Running the Bot

1. Ensure the game server is running.
2. Run the bot container:

```bash
dotnet run --project src/MakaMek.Tools/BotContainer/BotContainer.csproj
```

The bot will automatically connect to the server, join the game, and start playing when the game begins.

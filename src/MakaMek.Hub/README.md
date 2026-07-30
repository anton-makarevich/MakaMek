# MakaMek Hub

Cloud relay room-management service initially developed for MakaMek. Hosts a SignalR hub for real-time game relay and REST endpoints for room lifecycle management.

## Prerequisites

- .NET 10 SDK
- Docker (optional, for containerized run)

## Configuration

Configuration is managed via `appsettings.json` under the `Hub` section:

```json
{
  "Hub": {
    "ApiKey": "",
    "MaxConcurrentRooms": 100,
    "RoomTtlSeconds": 7200
  }
}
```

- **ApiKey**: Shared key required by REST callers (sent via `X-Api-Key` header). Must be set to a non-empty value — all `/api/*` requests are rejected with 401 if empty.
- **MaxConcurrentRooms**: Maximum number of active rooms at once.
- **RoomTtlSeconds**: Time-to-live for inactive rooms before garbage collection.

Full option list in `Configuration/HubOptions.cs`.

## Running Locally

### With .NET SDK

```bash
dotnet run --project src/MakaMek.Hub/MakaMek.Hub.csproj
```

The service starts on `http://localhost:5000` (ASP.NET default) with the `Development` environment profile.

Set the API key via environment variable or `appsettings.Development.json`:

```bash
$env:Hub__ApiKey="dev-key"   # PowerShell
export Hub__ApiKey="dev-key"  # bash
```

Or add `"ApiKey": "dev-key"` to the `Hub` section in `appsettings.Development.json`.

### With Docker

Build from the repo root:

```bash
docker build -t makamek-hub -f src/MakaMek.Hub/Dockerfile .
docker run -p 8080:8080 -e Hub__ApiKey="dev-key" makamek-hub
```

The container listens on `http://localhost:8080` in `Production` mode.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Health check (returns status, service name, version) |
| GET | `/api/rooms` | List active rooms (requires API key) |
| POST | `/api/rooms` | Create a room (requires API key) |
| POST | `/api/rooms/{roomId}/join` | Join a room by code (requires API key) |
| DELETE | `/api/rooms/{roomId}` | Close a room (requires API key) |
| WebSocket | `/relay` | SignalR hub for game relay (requires `apiKey` and `sessionToken` query parameters) |

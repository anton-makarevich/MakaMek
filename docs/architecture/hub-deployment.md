# Hub Deployment Guide

How to build, configure, and run the MakaMek relay hub as a containerised service.

## Building the Container

From the repository root:

```bash
docker build -f src/MakaMek.Hub/Dockerfile -t makamek-hub .
```

The build context is the repo root so the full solution tree is available for `dotnet restore` and `dotnet publish`. The resulting image is based on `mcr.microsoft.com/dotnet/aspnet:10.0` and exposes **port 8080** over plain HTTP (TLS termination is handled by a reverse proxy — see [TLS & Reverse Proxy](#tls--reverse-proxy) below).

## Running the Container

```bash
docker run -d \
  -p 8080:8080 \
  -e Hub__ApiKey="your-secret-api-key" \
  --name makamek-hub \
  makamek-hub
```

The container serves HTTP on port 8080. A health-check endpoint is available at `GET /health` and does not require authentication.

## Configurable Settings

All settings live under the `Hub` section and can be overridden via environment variables using the double-underscore separator (e.g. `Hub__ApiKey`). Defaults are safe for a single-instance deployment.

| Key | Default | Description |
|-----|---------|-------------|
| `Hub:ApiKey` | `""` (empty) | Shared API key required by REST endpoints and WebSocket connections. **Must be set via deployment configuration; never committed to the repo.** |
| `Hub:MaxConcurrentRooms` | `100` | Maximum number of rooms the relay accepts simultaneously. |
| `Hub:JoinRateLimitPerMinute` | `10` | Maximum join attempts per minute per IP address. |
| `Hub:RelayRateLimitPerMinute` | `120` | Maximum `Relay()` calls per minute per SignalR connection. |
| `Hub:MaxRelayPayloadBytes` | `262144` (256 KB) | Maximum relay message payload size in bytes. |
| `Hub:RoomTtlSeconds` | `7200` (2 hours) | Time-to-live for rooms. A room is garbage-collected after this duration of inactivity, regardless of state. |
| `Hub:DissolutionGracePeriodSeconds` | `30` | Grace period after the host disconnects before the room is permanently dissolved. Allows brief transport blips without destroying the session. |
| `Hub:TrustedProxies` | `[]` | CIDRs or IP addresses of trusted reverse proxies for `ForwardedHeaders`. |

### ASP.NET Core Settings

| Key | Default | Description |
|-----|---------|-------------|
| `ASPNETCORE_URLS` | `http://+:8080` | Listening URL. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment. |

### Logging

Request and URL logging are disabled by default to prevent credential leakage. See `appsettings.json` for the configured log levels.

## Secrets

The `Hub:ApiKey` is the only secret. It must **never** be committed to the repository or embedded in `appsettings.json`. Supply it via one of:

- Environment variable: `Hub__ApiKey=your-key`
- Docker/Kubernetes secret mounted as an environment variable
- Deployment platform secret store (e.g. Fly.io secrets, Hetzner Cloud-init)

The `appsettings.Development.json` ships with an empty `Hub:ApiKey` for local development; the key must be supplied via user-secrets or environment variables.

## TLS & Reverse Proxy

The hub serves plain HTTP. TLS **must** be terminated by a reverse proxy in front of the container. A typical production setup:

```text
Internet ──TLS──► Caddy / Nginx / Traefik ──HTTP──► Hub container:8080
```

A lightweight reverse proxy such as [Caddy](https://caddyserver.com/) handles automatic TLS certificate provisioning (Let's Encrypt) and WebSocket upgrades.

### Credential Redaction

WebSocket session tokens and the API key travel as query-string parameters (browsers cannot set custom WebSocket headers). The `X-Api-Key` header carries the key for REST calls. **All logging and tracing layers must redact:**

- Query-string parameters on connection URLs (especially `sessionToken` and `apiKey`)
- The `X-Api-Key` header value in REST access logs
- The `Session-Token` header value

Full connection URLs, including query strings, must never be logged at any layer. This applies to reverse proxies, load balancers, access logs, APM systems, and application-level diagnostics.

## Health Check

The hub exposes `GET /health` returning:

```json
{
  "status": "healthy",
  "service": "MakaMek.Hub",
  "version": "0.x.y"
}
```

This endpoint is **outside** the `/api/*` path and does not require authentication. It is suitable for container orchestrator probes (Kubernetes `livenessProbe`, Docker `HEALTHCHECK`).

## Reference

This document implements the operational and security requirements specified in:

- **Hosting & Cost** — [Network PRD §Hosting & Cost](network-prd.md#hosting--cost)
- **Trust & Authority** — [Network PRD §Trust & Authority](network-prd.md#trust--authority--resolved-per-1232)
- **Security** — [Network PRD §Security](network-prd.md#security-considerations--specified-per-1225-1232)

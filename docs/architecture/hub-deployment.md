# Hub Deployment Guide

How to deploy, configure, and operate the MakaMek relay hub (the SignalR hub service from the [Sanet.Transport](https://github.com/anton-makarevich/Sanet.Transport) repository) as a containerised service.

## Production Deployment (Oracle Cloud Always Free)

The hub is deployed as an always-on HTTPS/WSS service on an OCI **Always Free** ARM VM, provisioned with Pulumi:

- Infrastructure project: `src/MakaMek.Hub.Infra` (see its [SETUP.md](../../src/MakaMek.Hub.Infra/SETUP.md) for every value you must obtain from OCI and Pulumi).
- Runtime layout on the VM: Docker Compose stack — [Caddy](https://caddyserver.com/) terminates TLS (auto Let's Encrypt for `demohub.makamek.nl`, access logging disabled so query-string credentials are never recorded) and proxies to the hub container on port 8080.
- Ad-hoc deployments run through the `Hub Infra (Pulumi)` GitHub workflow (`workflow_dispatch` only — no automatic triggers), backed by the Pulumi Cloud state backend.
- A `$1/month` budget with an alert rule acts as a tripwire: all resources are Always Free, so it should never trigger.
- Shape is pinned to `VM.Standard.A1.Flex` 2 OCPU / 12 GB — the current Always Free allowance per tenancy (halved by Oracle in June 2026). Do not increase it.

Operational runbook (deploy, rollback, incident checks): see [SETUP.md §Troubleshooting](../../src/MakaMek.Hub.Infra/SETUP.md).

## Building the Container

CI in the Sanet.Transport repository publishes the image to GHCR; there is normally no need to build it manually. To build locally from the Sanet.Transport repository root:

```bash
docker build -f src/Sanet.Transport.SignalR.Hub/Dockerfile -t sanet-transport-hub .
```

The resulting image is based on `mcr.microsoft.com/dotnet/aspnet:10.0` and exposes **port 8080** over plain HTTP (TLS termination is handled by Caddy — see below).

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

The `Hub:ApiKey` is the only hub **runtime** secret. (Infrastructure and deployment credentials — `PULUMI_ACCESS_TOKEN`, OCI authentication material, and the Pulumi `apiKey` consumed by the infra project's `Program.cs` — are separate deployment-time secrets managed via GitHub/Pulumi secret stores.) The API key itself must **never** be committed to the repository or embedded in `appsettings.json`. Supply it via one of:

- Environment variable: `Hub__ApiKey=your-key` (this is what the deployment's `.env` file does)
- Docker: pass it via the existing `--env-file` mechanism (e.g. `docker run --env-file .env`, where `.env` sets `Hub__ApiKey`) — a standard Docker secret mount is not read automatically by the hub
- Deployment platform secret store (the OCI deployment keeps it as an encrypted Pulumi Cloud stack secret)

The `appsettings.Development.json` ships with an empty `Hub:ApiKey` for local development; the key must be supplied via user-secrets or environment variables.

## TLS & Reverse Proxy

The hub serves plain HTTP. TLS **must** be terminated by a reverse proxy in front of the container. The deployed setup:

```text
Internet ──TLS──► Caddy :443 ──HTTP──► Hub container:8080
```

Caddy handles automatic TLS certificate provisioning (Let's Encrypt) and WebSocket upgrades. Its access log is disabled (`log { output discard }`) — see the redaction requirements below for why nothing may ever be logged at this layer.

### CORS

Caddy handles CORS at the proxy layer so `OPTIONS` preflights never reach the hub (the hub itself returns `405` for preflights):

- The allowed origins are set with the `allowedOrigins` Pulumi config key (`makamek-hub-infra:allowedOrigins`) as a space-separated list. Currently: GitHub Pages (`https://anton-makarevich.github.io`), `https://makamek.online`, `https://makamek.pages.dev`, and `https://play.makamek.net`.
- Preflight requests (`OPTIONS` with an allowed `Origin`) are answered by Caddy with `204` and `Access-Control-Allow-Methods: GET, POST, DELETE, OPTIONS` and `Access-Control-Allow-Headers: Content-Type, x-api-key`.
- For non-preflight requests, `Access-Control-Allow-Origin` echoes the request origin only when it matches the allowlist (no wildcard), plus `Vary: Origin`.
- Credentials mode is not used — no cookies travel cross-origin, so no `Access-Control-Allow-Credentials` is emitted.

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

This endpoint is **outside** the `/api/*` path and does not require authentication. It is suitable for container orchestrator probes (Kubernetes `livenessProbe`, Docker `HEALTHCHECK` — the provided Dockerfile configures this automatically).

## Reference

This document implements the operational and security requirements specified in:

- **Hosting & Cost** — [Network PRD §Hosting & Cost](network-prd.md#hosting--cost)
- **Trust & Authority** — [Network PRD §Trust & Authority](network-prd.md#trust--authority--resolved-per-1232)
- **Security** — [Network PRD §Security](network-prd.md#security-considerations--specified-per-1225-1232)

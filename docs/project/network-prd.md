# Network Multiplayer (Relay Hub) - Product Requirements Document

**Date:** 2026-07-17
**Status:** Draft — #1225 resolved, remaining decisions in progress
**Wayfinder map:** [Network PRD: relay-hub multiplayer #1224](https://github.com/anton-makarevich/MakaMek/issues/1224)

## Executive Summary

MakaMek's networking is today **LAN-only peer-host**: the machine that starts a game runs an embedded SignalR hub (`SignalRHostManager`, port 2439) and other players dial *into* it. That model cannot support internet play (it needs inbound connectivity through NAT/firewalls) and cannot let a browser be the host (a WASM/browser client cannot accept inbound connections at all).

This PRD proposes moving to a **cloud relay-hub** model: the cloud holds only a *dumb* WebSocket message relay with **no game logic**. The authoritative `ServerGame` continues to run on a *participant's* machine; every party — the server-logic host and all clients alike — dials the relay **outbound**. The relay groups parties by room and fans every command out to the others in that room. Because everyone connects outbound, NAT traversal disappears and any platform (desktop, mobile, **and browser/WASM**) can be the host.

The design deliberately reuses the existing command-based, transport-agnostic architecture: it is largely a new `ITransportPublisher` plus a small cloud relay, not a rewrite of the game.

## Goals

- **Internet multiplayer across networks** — players behind different NATs can play without port-forwarding.
- **Web clients as first-class hosts** — a browser/WASM client can run the authoritative `ServerGame` and host a game.
- **Minimal cost** — a few $/month ceiling for a non-profit FOSS project; ideally scale-friendly or self-hostable.
- **Matchmaking / discovery** — a way for players to find and join a specific game.
- **Reconnection & state resync** — a dropped player can rejoin and be brought back to current state (tractable because the game is turn-based).
- **Preserve the architecture** — keep the command/transport abstraction; no game logic moves to the cloud.

## Non-Goals (Out of Scope)

- **Persistent / centralized accounts & authentication.** Identity is anonymous and lives only within a single game. Centralized identity is a later effort.
- **Server-side game persistence.** The relay holds no game state; a game does not survive the authoritative host going away (beyond in-session reconnect). Workarounds are acceptable for now.
- **Spectators.**
- **In-game chat.** Players integrate with Discord instead — permanently out of scope.

## Architecture Overview

### Current Architecture (LAN peer-host)

MakaMek uses a command-based client-server architecture where **all state changes travel as serialized command DTOs**:

- **`ServerGame`** — authoritative state; validates commands, drives phases, broadcasts server commands.
- **`ClientGame`** — mirrors state; submits client commands. Clients only process server-originated commands; the server only processes client commands (`GameOriginId` filtering).
- **`Sanet.Transport` abstraction** — the wire is hidden behind `ITransportPublisher`. `CommandTransportAdapter` serializes `IGameCommand` ↔ `TransportMessage` and multiplexes across any number of publishers.
  - `RxTransportPublisher` — in-process local play (hotseat, bots).
  - `Sanet.Transport.SignalR` — LAN network play.
- **Host vs. client wiring:**
  - `INetworkHostService` abstracts the embedded host. `SignalRHostService` (desktop) runs `SignalRHostManager` and exposes a `HubUrl`; `DummyNetworkHostService` (browser/mobile) returns `CanStart => false`, `HubUrl => null`.
  - `GameManager.InitializeLobby()` starts the host if `CanStart`, adds its publisher to the adapter, and creates the `ServerGame`.
  - `JoinGameViewModel` connects a client via `ITransportFactory.CreateAndStartClientPublisher(serverAddress)` and adds the resulting publisher to the adapter.

**The problem:** the *host* machine runs the hub, so peers must reach it inbound. That is LAN-only, and browsers can never host.

### Proposed Architecture (cloud relay hub)

The topology inverts: the hub moves to the cloud and becomes a **dumb relay**. The host machine stops being a server *endpoint* and becomes just another **outbound-connected party** — one that happens to run the authoritative `ServerGame`.

```text
                      ┌────────────────────────────┐
                      │      Cloud Relay Hub       │
                      │   (dumb WebSocket fan-out) │
                      │   rooms = connection groups│
                      │      NO game logic         │
                      └───────────▲────────────────┘
          outbound wss │          │ outbound wss     │ outbound wss
        ┌──────────────┘          │                  └──────────────┐
        │                         │                                 │
┌───────┴────────┐       ┌────────┴────────┐              ┌─────────┴──────┐
│  Host machine  │       │   Client (PC)   │              │ Client (Web/   │
│  (any platform)│       │                 │              │ Mobile)        │
│  ServerGame +  │       │   ClientGame    │              │   ClientGame   │
│  ClientGame    │       │                 │              │                │
└────────────────┘       └─────────────────┘              └────────────────┘

Every party dials the hub OUTBOUND. The party running ServerGame is the
authoritative "server"; all others are "clients". The hub never inspects
payloads — it only fans each message out to the other members of the room.
```

**Key properties**

- **Dumb relay:** the hub only groups connections into rooms and re-broadcasts messages to the other members. It runs no `ServerGame`, validates nothing, and holds no game state — only transient room membership.
- **Outbound-only:** every party (host included) makes an outbound `wss` connection, so NAT/firewalls are a non-issue and browsers can participate fully.
- **Symmetric hosting:** whoever "starts a server" runs `ServerGame` locally; the platform is irrelevant. **Web can host too** (see Decision Record below).
- **Role from logic, not topology:** a joined party decides whether it is acting as "server" or "client" from local state, reusing the existing `GameOriginId` command-filtering rules.

## Decision Record

The direction below is **settled**; the detailed decisions marked *Open* are tracked as child tickets of the wayfinder map and are resolved before implementation planning.

### Settled

| Decision | Outcome |
|---|---|
| Hosting model | Cloud **relay hub** (dumb fan-out); authoritative logic stays on a participant's machine. |
| Web host role | **Symmetric** — any platform, including WASM/browser, may run `ServerGame` and host. |
| Cost ceiling | A few $/month acceptable. |
| Identity | Anonymous; player IDs preserved only within a single game. Centralized identity later. |

### Resolved by research

| Question | Finding | Ticket |
|---|---|---|
| Can `ServerGame` run in a browser/WASM head? | **Yes, unchanged.** `ServerGame.Start()` is an idle keep-alive loop (`while(!disposed && !gameOver){ await Task.Delay(100); }`) that yields to the browser event loop; real work runs synchronously in `HandleCommand` off the transport subscription. Single-threaded browser-wasm is fine for turn-based command bursts. Do **not** enable `<WasmEnableThreads>`. | [#1228](https://github.com/anton-makarevich/MakaMek/issues/1228) |
| SignalR vs pure WebSockets for the relay? | **Recommend keeping SignalR** in "thin-relay" mode (`HttpTransportType.WebSockets` + `SkipNegotiation`). Groups map to rooms; reconnect, keepalive, backpressure, in-order delivery and a scale-out path come for free; a pure-WS relay would reinvent all of it. WASM client works over `wss` (query-string token). | [#1226](https://github.com/anton-makarevich/MakaMek/issues/1226) |
| Where to host the relay cheaply? | Budget easily met. **Coupled to the transport choice:** keep-SignalR ⇒ Fly.io (~$3–6/mo) or a small VPS (Hetzner ~€6/mo + Caddy TLS); the idle-optimal/cheapest option (Cloudflare Workers + Durable Objects, $0–5/mo) requires an edge JS/TS rewrite and cannot run SignalR. Avoid scale-to-zero for a persistent WS relay. Free fallback: Oracle Always-Free A1. | [#1227](https://github.com/anton-makarevich/MakaMek/issues/1227) |
| What is the relay hub contract? | Rooms identified by 6-char base32 codes (2 h TTL, in-memory). **Creator-is-server** role model. Opaque `TransportMessage` envelope + `HubMessage` for hub events. Hub API: `CreateRoom` / `JoinRoom` / `LeaveRoom` / `Relay`. Reconnection via full state snapshot. Host loss → graceful termination (no migration). | [#1225](https://github.com/anton-makarevich/MakaMek/issues/1225) |

### Open (decided before implementation)

| Decision | Ticket |
|---|---|
| Relay transport — SignalR-thin-relay on a .NET host vs. edge-WS rewrite | [#1229](https://github.com/anton-makarevich/MakaMek/issues/1229) |
| Matchmaking & game discovery — room codes vs. listable lobby | [#1230](https://github.com/anton-makarevich/MakaMek/issues/1230) |
| Connection resilience — reconnection, state resync, host-loss handling | [#1231](https://github.com/anton-makarevich/MakaMek/issues/1231) |
| Trust & authority model for a dumb relay | [#1232](https://github.com/anton-makarevich/MakaMek/issues/1232) |

## Component Design

> The C# below is **illustrative** of the proposed shape; some contracts are finalized (#1225, #1226, #1227, #1228) while others remain open in the decision tickets.

### 1. Cloud relay hub (thin SignalR hub) — *resolved per #1225*

A tiny ASP.NET Core SignalR hub with no game logic. Rooms are SignalR groups; the payload is opaque. The contract below is settled.

#### Room lifecycle & identity

**Room code format:** 6-character base32 string (e.g. `ABC234`), excluding ambiguous characters (0/O, 1/I/L). Generated cryptographically on the hub. ~1 billion combinations makes casual collision negligible.

**Room states:**

| State | Entry condition | Behaviour |
|---|---|---|
| **Created** | Host calls `CreateRoom()` | Room exists; host not yet running `ServerGame`. Other clients cannot join until host is ready. |
| **Active** | Host calls `SetHostReady()` | Accepting clients; game commands relayed between all members. |
| **Dissolved** | Host disconnects *or* all clients leave | Room is marked for garbage collection (30 s grace for brief host blips). |

**TTL:** Rooms expire after 2 hours of inactivity. In-memory only — no database.

#### Message envelope

Two distinct message types serve different layers:

```csharp
// Transport-level: opaque game command envelope relayed through the hub.
// The hub never inspects or deserializes Payload.
public record TransportMessage(
    string Type,           // Command type (e.g. "MoveUnit", "Attack")
    string Payload,        // JSON-serialized command DTO
    string SenderId,       // Connection ID of sender (hub-attached)
    long SequenceNumber,   // For ordering / replay
    DateTime Timestamp     // For timeout handling
);

// Hub-level: control messages between hub and participants.
public record HubMessage(
    HubMessageType Type,
    string? Data = null
);

public enum HubMessageType
{
    RoomCreated,
    RoomJoined,
    RoomLeft,
    PeerConnected,
    PeerDisconnected,
    HostReady,
    GameStarted,
    Error
}
```

#### Hub API contract

```csharp
public interface IRelayHub
{
    // Room management
    Task<RoomCreationResult> CreateRoom();
    Task<JoinResult> JoinRoom(string roomCode, string playerName);
    Task LeaveRoom(string roomCode);

    // Game messaging
    Task Relay(string roomCode, TransportMessage message);

    // Hub events (received by clients)
    Task OnRoomCreated(RoomCreationResult result);
    Task OnPeerConnected(string peerId);
    Task OnPeerDisconnected(string peerId);
    Task OnReceive(TransportMessage message);
    Task OnError(HubError error);
}

public record RoomCreationResult(
    string RoomCode,
    string HostId,
    DateTime ExpiresAt
);

public record JoinResult(
    bool Success,
    string? Role,       // "host" or "client"
    string? PlayerId,
    string? ErrorMessage
);

public record HubError(
    ErrorCode Code,
    string Message,
    string? RoomCode
);

public enum ErrorCode
{
    RoomNotFound,
    RoomFull,
    RoomExpired,
    HostNotReady,
    HostDisconnected,
    InvalidCode,
    ConnectionFailed
}
```

#### Hub implementation sketch

```csharp
public class RelayHub : Hub<IRelayHub>
{
    private readonly IRoomManager _roomManager;

    public async Task<RoomCreationResult> CreateRoom()
    {
        var room = await _roomManager.CreateRoomAsync();
        return new RoomCreationResult(
            room.Code,
            Context.ConnectionId,
            room.ExpiresAt);
    }

    public async Task<JoinResult> JoinRoom(string roomCode, string playerName)
    {
        var room = await _roomManager.GetRoomAsync(roomCode);
        if (room == null)
            return new JoinResult(false, null, null, "Room not found");

        if (room.HostId == null)
            return new JoinResult(false, null, null, "Host not ready");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

        var playerId = Guid.NewGuid().ToString();
        var role = room.HostId == Context.ConnectionId ? "host" : "client";

        await Clients.Client(room.HostId).OnPeerConnected(Context.ConnectionId);

        return new JoinResult(true, role, playerId, null);
    }

    public async Task Relay(string roomCode, TransportMessage message)
    {
        // Hub never inspects payload — just fans out.
        await Clients.OthersInGroup(roomCode).OnReceive(message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var rooms = await _roomManager.GetRoomsForConnectionAsync(Context.ConnectionId);
        foreach (var room in rooms)
        {
            if (room.HostId == Context.ConnectionId)
            {
                await Clients.Group(room.Code).OnError(new HubError(
                    ErrorCode.HostDisconnected,
                    "Host disconnected",
                    room.Code));
                await _roomManager.MarkRoomForDissolutionAsync(room.Code);
            }
            else
            {
                await Clients.Client(room.HostId!).OnPeerDisconnected(Context.ConnectionId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
```

Configuration (per research #1226): `HttpTransportType.WebSockets` + `SkipNegotiation` — a single `wss` upgrade, no fallback, no sticky-session requirement.

### 2. Relay transport publisher — *proposed*

A new `ITransportPublisher` that dials the relay outbound, used identically by host and clients. It slots into the existing `CommandTransportAdapter` with no changes to `ServerGame`/`ClientGame`.

```csharp
public class RelayClientPublisher : ITransportPublisher, IDisposable
{
    private readonly HubConnection _connection;
    private readonly string _roomCode;

    public RelayClientPublisher(string hubUrl, string roomCode)
    {
        _roomCode = roomCode;
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.SkipNegotiation = true,
                     o => o.Transports = HttpTransportType.WebSockets)
            .WithAutomaticReconnect(async _ =>
            {
                // Re-join the room after SignalR automatically reconnects.
                await _connection.InvokeAsync("JoinRoom", _roomCode);
            })
            .Build();
    }

    public async Task StartAsync()
    {
        _connection.On<TransportMessage>("Receive", msg => _onMessage?.Invoke(msg));
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinRoom", _roomCode);
    }

    public async Task PublishMessage(TransportMessage message)
    {
        if (_connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Relay is not connected.");

        try
        {
            await _connection.InvokeAsync("Relay", _roomCode, message);
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            // Log and rethrow so callers can react to relay failures
            // rather than silently swallowing them (fire-and-forget).
            throw;
        }
    }

    public void Subscribe(Action<TransportMessage> onMessage) => _onMessage = onMessage;
    private Action<TransportMessage>? _onMessage;
}
```

### 3. Host wiring — replace embedded host with an outbound relay connection

Today the host runs `INetworkHostService` (an embedded hub). Under the relay model there is **no embedded hub**; hosting means "run `ServerGame` locally *and* open a `RelayClientPublisher` to the cloud". This unifies the host and client transport paths.

- `SignalRHostService`/`SignalRHostManager` (embedded server) is retired for internet play (optionally retained for pure-LAN, see Migration).
- `DummyNetworkHostService` on the browser head is **replaced** by attaching a `RelayClientPublisher` — this is the single change that unlocks "web can host" (per #1228, no engine change is needed).
- `GameManager.InitializeLobby()` creates the `ServerGame` as today, but instead of `_networkHostService.Start()` it adds a `RelayClientPublisher` (for the chosen room) to the transport adapter.

### 4. Role establishment — *resolved per #1225*

Because the relay is symmetric, a joined party must know whether it is the authoritative server. The **creator-is-server** model is settled:

```text
 Host                          Hub                         Client
   │                            │                            │
   │ 1. CreateRoom()            │                            │
   ├───────────────────────────►│                            │
   │◄── RoomCreated(code, id)  │                            │
   │                            │                            │
   │ 2. Start ServerGame        │                            │
   │    SetHostReady(code)      │                            │
   ├───────────────────────────►│                            │
   │◄── HostReady              │                            │
   │                            │                            │
   │                            │ 3. JoinRoom(code, name)    │
   │                            │◄───────────────────────────┤
   │                            │                            │
   │  OnPeerConnected(clientId) │  RoomJoined(role=client)   │
   │◄───────────────────────────┤───────────────────────────►│
   │                            │                            │
   │ 4. Game commands flow      │                            │
   │    Client ──Relay()──► Hub ──fan-out──► Host             │
   │    Host ──Relay()──► Hub ──fan-out──► Client             │
```

**Role assignment rules:**

| Rule | Detail |
|---|---|
| Creator is server | The party that calls `CreateRoom()` runs `ServerGame` and becomes the host. |
| Joiners are clients | All parties joining via `JoinRoom()` run only `ClientGame`. |
| No election | Simple creator-is-server; no voting, no role migration. |
| Game origin ID | Existing `GameOriginId` filtering handles command directionality — host processes `GameOriginId.Client` commands; clients process `GameOriginId.Server` commands. |

Host-role migration is explicitly out of scope (see #1231).

### 5. Matchmaking & discovery — *proposed, pending #1230*

Proposed minimum: **shareable room codes**. Creating a game creates a room and yields a short code/link; joiners enter the code (fits the anonymous-identity constraint, needs no accounts, and requires no state beyond the hub's group membership). A listable public lobby is a possible later addition.

### 6. Connection resilience — *protocol specified per #1225; implementation details pending #1231*

Turn-based play makes resync tractable. The hub contract specifies the following flows:

**Client reconnection:**

```text
1. Client detects connection loss (SignalR auto-reconnect)
2. Client re-joins room with same PlayerId
3. Hub notifies host of reconnection (OnPeerConnected)
4. Host sends full state snapshot to reconnecting client
5. Client applies snapshot and resumes normal operation
```

**Host loss:**

```text
1. Hub detects host disconnection (OnDisconnectedAsync)
2. Hub notifies all clients in room (OnError with HostDisconnected)
3. Clients gracefully exit to main menu
4. Room marked for dissolution after 30 s grace period
5. No host migration — game ends (GameEndedCommand / shutdown lifecycle)
```

Detailed reconnection mechanisms (snapshot vs. command-log replay, stable identity format) are resolved in #1231.

### 7. Trust & authority — *proposed, pending #1232*

The relay is opaque and an ordinary client machine is authoritative, so a malicious peer could forge server-origin commands or impersonate a player. Proposed near-term posture: **accept-risk** for trusted-friends play (obscure room codes), with a note to consider **minimal hardening** later (hub-tagged sender identity so `GameOriginId`/server-origin can't be spoofed). Final call in #1232.

### 8. Security considerations — *specified per #1225*

Minimal hardening built into the hub contract:

| Measure | Detail |
|---|---|
| Sender verification | Hub attaches `SenderId` (connection ID) to every `TransportMessage`; receiving parties can verify origin. |
| Room code entropy | 6-char base32 ≈ ~1 billion combinations; sufficient against brute-force join. |
| Rate limiting | Max 10 join attempts per minute per IP. |
| Message size limit | 256 KB max per message (well above any game command payload). |
| Anonymous identity | `PlayerId` = GUID generated per game; `PlayerName` = user-entered, in-memory only. No persistent accounts or authentication. |

## Communication Flow

### Command flow (client action → all parties)

```text
Client action
   ↓  ClientGame.Send…Command()  (client command, GameOriginId = client)
CommandPublisher → CommandTransportAdapter (serialize)
   ↓  RelayClientPublisher.PublishMessage()
Cloud Relay Hub  ──(fan-out to OthersInGroup(room))──►  every other party
   ↓
Host's RelayClientPublisher → CommandTransportAdapter (deserialize)
   ↓  ServerGame.HandleCommand()  (validates & applies — authoritative)
ServerGame publishes server command (GameOriginId = server)
   ↓  RelayClientPublisher → Relay Hub → fan-out
Every ClientGame applies the server command (mirrors state)
```

### Transport layering (unchanged core, new leaf)

```text
        ServerGame / ClientGame        (no change)
                 │
          CommandPublisher             (no change)
                 │
        CommandTransportAdapter        (no change)
        ┌────────┴─────────┐
        ▼                  ▼
 RxTransportPublisher   RelayClientPublisher   ◄── NEW (replaces embedded
     (local play)        (internet play)            SignalR host path)
```

## Hosting & Cost

Per research (#1227), coupled to the transport decision (#1229):

- **If SignalR is kept (recommended path):** deploy the thin relay as a small ASP.NET Core container on **Fly.io** (~$3–6/mo, always-on, runs the existing Dockerfile) or a **Hetzner VPS** (~€6/mo + Caddy for automatic `wss` TLS). A trimmed/AOT relay image is ~25–30 MiB and needs well under 256 MB RAM.
- **Cheapest / idle-optimal alternative:** **Cloudflare Workers + Durable Objects** (WebSocket Hibernation), $0–5/mo — but requires rewriting the relay as edge JS/TS (no SignalR).
- **Free fallback:** Oracle Cloud Always-Free A1 ARM.
- **Avoid** scale-to-zero platforms for a persistent WS relay (cold starts drop/delay connects); note Azure SignalR Free hard-caps at 20 concurrent connections.

## Implementation Plan

> Sequenced after the open decisions (#1229, #1230, #1231, #1232) are locked. Hub contract (#1225) is resolved — room lifecycle, API, message envelope, role establishment, and security measures are specified above.

### Phase 1: Relay transport
- Implement `RelayClientPublisher : ITransportPublisher` (outbound `wss`, room join, opaque fan-out).
- Add relay wiring to `GameManager.InitializeLobby()` / `JoinGameViewModel` alongside the existing factory.
- Unit-test the publisher against `CommandTransportAdapter` round-tripping.

### Phase 2: Cloud relay hub
- Implement the thin SignalR `RelayHub` per the settled contract (#1225): `IRelayHub` interface, `IRoomManager`, room lifecycle (Created → Active → Dissolved), 6-char base32 codes, 2 h TTL.
- Implement `HubMessage` event flow (`PeerConnected`, `PeerDisconnected`, `OnError`, etc.).
- Containerize; deploy to the chosen host (#1229/#1227) with `wss`/TLS.

### Phase 3: Room lifecycle & matchmaking
- Room-code creation on "start server"; join-by-code in `JoinGameViewModel`.
- Creator-is-server role establishment (settled per #1225).
- Matchmaking discovery details (#1230) if listable lobby is in scope.

### Phase 4: Web host enablement
- Replace `DummyNetworkHostService` on the browser head with the relay-client attach path.
- Verify `ServerGame` hosts in the WASM head (per #1228 — no engine change expected).

### Phase 5: Resilience
- Client reconnect: re-join with same `PlayerId` → host sends full state snapshot → client resumes (protocol settled per #1225; replay mechanism in #1231).
- Stable within-game identity for seat reclamation.
- Host-loss: hub notifies clients (`HostDisconnected`) → clients exit to menu → room dissolved after 30 s (settled per #1225).

### Phase 6: Migration & polish
- Decide fate of the LAN `SignalRHostService` path (retain for offline-LAN vs. remove).
- Trust-model hardening if adopted (#1232).
- End-to-end tests across desktop ↔ web ↔ mobile through the relay.

## Open Questions

Tracked as decision tickets on the wayfinder map; each must be resolved before its dependent implementation phase:

1. **Transport (#1229):** SignalR-thin-relay + .NET host vs. edge-WS rewrite (drives hosting choice).
2. **Matchmaking (#1230):** room codes only, or a listable lobby.
3. **Resilience (#1231):** resync mechanism (snapshot vs. command-log replay) and host-loss policy.
4. **Trust (#1232):** accept-risk vs. minimal sender-identity hardening; which extensions are in-PRD vs. deferred.

## Success Criteria

- **Cross-network play:** two players on different NATs complete a full game with no port-forwarding.
- **Web hosting:** a browser/WASM client hosts a game (runs `ServerGame`) that desktop/mobile clients join.
- **Transport transparency:** `ServerGame`/`ClientGame` are unchanged; only a new `ITransportPublisher` and the cloud relay are added.
- **Cost:** running relay stays within a few $/month (or self-hosted).
- **Reconnection:** a dropped player rejoins and is resynced to current state without restarting the game.
- **Graceful host loss:** if the host disconnects, all clients are cleanly notified and returned to the menu.

## Summary

The relay-hub model unlocks internet and web multiplayer by **inverting the topology, not rewriting the game**: a dumb cloud WebSocket relay fans commands between parties that all connect outbound, while the authoritative `ServerGame` keeps running on a participant's machine. Research confirms the browser can host with no engine changes (#1228) and that a thin SignalR relay is the pragmatic transport (#1226), hostable within budget (#1227). The hub contract (#1225) is now resolved — room lifecycle, API, message envelope, role establishment, and security measures are specified. The remaining transport, matchmaking, resilience, and trust decisions are tracked on the wayfinder map and resolved before implementation begins.

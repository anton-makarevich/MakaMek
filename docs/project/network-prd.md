# Network Multiplayer (Relay Hub) - Product Requirements Document

**Date:** 2026-07-17
**Status:** Draft — all decision tickets resolved (#1225, #1229, #1230, #1231, #1232); ready for implementation planning
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
- **Reconnection (V1) / state resync (future)** — a dropped player can rejoin a game in progress and resume receiving live commands (V1). Full state resync — bringing the rejoined player back to current state — is deferred to a future effort pending a resource-consumption test (#1231).
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
| What is the relay hub contract? | Rooms identified by 6-char base32 codes (2 h TTL, in-memory). **Creator-is-server** role model. Opaque `RelayEnvelope` envelope + `HubMessage` for hub events. Hub API: `CreateRoom` / `JoinRoom` / `LeaveRoom` / `Relay`. Room tracks known player IDs; closed rooms reject new joins but allow known players to rejoin. Host loss → graceful termination (no migration). | [#1225](https://github.com/anton-makarevich/MakaMek/issues/1225) |
| Relay transport — SignalR-thin-relay vs. edge-WS rewrite? | **Confirmed: keep SignalR**, thin-relay mode (`HttpTransportType.WebSockets` + `SkipNegotiation`). New `RelayHub` (room-aware server) and `RelayClientPublisher` (client) are added as **new classes in `Sanet.Transport.SignalR`** — reusing the SignalR dependency, not extending `TransportHub`/`SignalRHostManager`/`SignalRClientPublisher`, which are single-tenant-per-process (one embedded Kestrel instance per LAN game, static-event dispatch, no room concept) and are not safe to multiplex for a shared cloud relay. Those existing classes are left untouched. **LAN SignalR path: retained unchanged**, offered as a separate "Host LAN" option alongside the new "Host Online" (relay) option — no migration/removal in this effort. | [#1229](https://github.com/anton-makarevich/MakaMek/issues/1229) |
| Matchmaking & game discovery — room codes vs. listable lobby? | **Confirmed: shareable room codes**, no listable lobby (reuses the #1225 `CreateRoom`/share-code/`JoinRoom` flow as-is). **"Room full"** = the game has already started, not a numeric player cap — enforced by a new `CloseRoom(roomCode)` hub method the host calls when leaving the lobby stage; `JoinRoom` rejects new players afterward, but allows known players (already in the room's roster) to rejoin for reconnection. **Hub-wide concurrent-games cap (N)** is a config-only value on the relay, not pinned in this PRD — tuned operationally once running on real infra. Exceeding it fails `CreateRoom()` with a new `HubAtCapacity` error carrying the current active-room count, rather than a separate status-query endpoint. | [#1230](https://github.com/anton-makarevich/MakaMek/issues/1230) |
| Connection resilience — resync mechanism, identity, host-loss? | **Deferred by design:** resync (command-log replay vs. snapshot) and persistence are **not built in this effort** — committing to an in-memory command log needs a resource-consumption test first, not a guess. What *is* settled now: `JoinRoom` uses the caller-supplied `IPlayer.Id` directly as the hub-level identity (no separate hub-invented ID), so future resync/persistence work needs no identity migration. The room tracks known `playerId`s, so a dropped client can rejoin a closed room (game already started) without being rejected as a new player — but no resync happens. Host-loss policy is unchanged from #1225 (no migration, ever — a permanent principle, not a v1 shortcut). In v1, a dropped connection has no resync: SignalR's automatic reconnect bridges brief transport blips, but messages missed during a gap are simply lost — an accepted, explicit limitation pending a future research + persistence effort. | [#1231](https://github.com/anton-makarevich/MakaMek/issues/1231) |
| Trust & authority model for a dumb relay? | **Now: anonymous, accept-risk** — no per-user authentication. Baseline v1 protections: room-code entropy + `JoinRoom` rate limit (#1225), a **new per-connection rate limit on `Relay()`** calls (config value, same treatment as the #1230 capacity cap — no number pinned without load data), message size limit (#1225), hub-wide capacity limit (#1230), and hub-tagged `SenderId` on every `RelayEnvelope` (#1225 — already gives anti-spoof "server origin can't be forged" hardening for free). Additionally, a **static shared API key** baked into official clients, required to connect to the hub at all, passed as a query-string parameter (browsers can't set custom WebSocket headers) and checked before the WS upgrade completes — explicitly **not real protection** (extractable from the client, no per-user identity), just a filter against low-effort automated traffic on a public endpoint. **Future:** real authentication via OAuth2/JWT against an external identity provider — not built now, and architecturally free to add later since SignalR/ASP.NET Core authenticate at the connection level, orthogonal to everything already decided. | [#1232](https://github.com/anton-makarevich/MakaMek/issues/1232) |

All decision tickets on the wayfinder map are now resolved.

## Component Design

> The C# below is **illustrative** of the proposed shape; all contracts are now finalized (#1225, #1226, #1227, #1228, #1229, #1230, #1231, #1232).

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

**Reconnection:** Once the room is closed (game started), new players are rejected with `RoomFull`. However, a player whose `playerId` is already in the room's roster may rejoin — this allows dropped clients to re-establish their connection during the game.

#### Message envelope

Two distinct message types serve different layers. **Naming note (found while resolving #1229):** `Sanet.Transport` already defines a `TransportMessage` record (`MessageType`, `SourceId: Guid`, `Payload`, `Timestamp`) — the type `ITransportPublisher.PublishMessage`/`Subscribe` actually use. The hub-fanned-out envelope below is a *different* type at a *different* layer (it wraps a serialized `Sanet.Transport.TransportMessage` as opaque `Payload`), so it's named `RelayEnvelope` to avoid colliding with the real one. Its `SenderId` (hub connection ID, for anti-spoof verification) is also distinct from the real `TransportMessage.SourceId` (a `Guid`, i.e. the existing `GameOriginId`) — the hub attaches the former without inspecting the latter.

```csharp
// Relay-level: opaque envelope relayed through the hub.
// The hub never inspects or deserializes Payload (a serialized Sanet.Transport.TransportMessage).
public record RelayEnvelope(
    string SenderId,       // Connection ID of sender (hub-attached)
    string Payload,        // JSON-serialized Sanet.Transport.TransportMessage
    long SequenceNumber,   // Reserved for future ordering / replay use; assigned but not consumed in V1
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
    Task<JoinResult> JoinRoom(string roomCode, string playerName, Guid playerId);
    Task LeaveRoom(string roomCode);
    Task CloseRoom(string roomCode); // host calls when leaving the lobby stage; room stops accepting joins

    // Game messaging
    Task Relay(string roomCode, RelayEnvelope message);

    // Hub events (received by clients)
    Task OnRoomCreated(RoomCreationResult result);
    Task OnPeerConnected(string peerId);
    Task OnPeerDisconnected(string peerId);
    Task OnReceive(RelayEnvelope message);
    Task OnError(HubError error);
}

public record RoomCreationResult(
    bool Success,
    string? RoomCode,
    string? HostId,
    DateTime? ExpiresAt,
    HubError? Error       // set when Success is false, e.g. HubAtCapacity
);

public record JoinResult(
    bool Success,
    string? Role,       // "host" or "client"
    string? PlayerId,   // echoes the caller-supplied IPlayer.Id (#1231) — never hub-invented
    string? HostId,     // Hub Connection ID of the host — for anti-spoof verification by clients
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
    RoomFull,          // game already started; see #1230
    RoomExpired,
    HostNotReady,
    HostDisconnected,
    InvalidCode,
    ConnectionFailed,
    HubAtCapacity      // hub-wide concurrent-room limit reached; see #1230
}
```

#### Hub implementation sketch

```csharp
public class RelayHub : Hub<IRelayHub>
{
    private readonly IRoomManager _roomManager;

    public async Task<RoomCreationResult> CreateRoom()
    {
        if (await _roomManager.IsAtCapacityAsync())
        {
            var activeCount = await _roomManager.GetActiveRoomCountAsync();
            return new RoomCreationResult(false, null, null, null,
                new HubError(ErrorCode.HubAtCapacity,
                    $"Relay is at capacity ({activeCount} active games)", null));
        }

        var room = await _roomManager.CreateRoomAsync();
        return new RoomCreationResult(true, room.Code, Context.ConnectionId, room.ExpiresAt, null);
    }

    public async Task<JoinResult> JoinRoom(string roomCode, string playerName, Guid playerId)
    {
        var room = await _roomManager.GetRoomAsync(roomCode);
        if (room == null)
            return new JoinResult(false, null, null, null, "Room not found");

        if (room.HostId == null)
            return new JoinResult(false, null, null, null, "Host not ready");

        if (room.IsClosed && !room.HasPlayer(playerId)) // game already started; only known players may rejoin
            return new JoinResult(false, null, null, null, "Game already in progress");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        room.AddPlayer(playerId); // track known players for reconnection

        var role = room.HostId == Context.ConnectionId ? "host" : "client";

        await Clients.Client(room.HostId).OnPeerConnected(Context.ConnectionId);

        // playerId is echoed as-is — it's the caller's own IPlayer.Id, never
        // generated by the hub, so a future reconnect can reuse it unchanged (#1231).
        // HostId is returned so clients can verify RelayEnvelope.SenderId against the host.
        return new JoinResult(true, role, playerId.ToString(), room.HostId, null);
    }

    public async Task CloseRoom(string roomCode)
    {
        // Host calls this when leaving the lobby stage. No further new JoinRoom
        // calls are accepted; known players (already in the room's roster) may
        // still rejoin for reconnection. In-room parties are unaffected.
        await _roomManager.CloseRoomAsync(roomCode, Context.ConnectionId);
    }

    public async Task Relay(string roomCode, RelayEnvelope message)
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

### 2. Relay transport publisher — *resolved per #1229*

A new `RelayClientPublisher : ITransportPublisher`, added as a **new class in `Sanet.Transport.SignalR.Client`** (sibling of the existing `SignalRClientPublisher`, which is untouched and keeps serving LAN play). It dials the relay outbound, used identically by host and clients, and slots into the existing `CommandTransportAdapter` with no changes to `ServerGame`/`ClientGame`.

It implements the *real* `ITransportPublisher.PublishMessage(TransportMessage message)` / `Subscribe(Action<TransportMessage>)` contract — `TransportMessage` here is the existing `Sanet.Transport.TransportMessage` (`MessageType`, `SourceId: Guid`, `Payload`, `Timestamp`), not the hub's `RelayEnvelope` (§1). Internally it serializes each `TransportMessage` into a `RelayEnvelope.Payload` for the trip through the hub, and deserializes back on receive — the hub itself never sees a `TransportMessage`, only the opaque envelope:

```csharp
public class RelayClientPublisher : ITransportPublisher, IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly string _roomCode;
    private readonly string _playerName;
    private readonly Guid _playerId; // the real IPlayer.Id — never hub-invented (#1231)
    private readonly List<Action<TransportMessage>> _subscribers = [];
    private long _sequenceNumber;

    public RelayClientPublisher(string hubUrl, string roomCode, string playerName, Guid playerId)
    {
        _roomCode = roomCode;
        _playerName = playerName;
        _playerId = playerId;
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o =>
            {
                o.SkipNegotiation = true;
                o.Transports = HttpTransportType.WebSockets;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.Reconnected += async _ =>
            // Re-join with the same IPlayer.Id after SignalR automatically reconnects.
            // The hub allows this because the playerId is already in the room's roster.
            // NOTE: no resync happens here in v1 (#1231) — any messages missed
            // during the gap are lost. This only re-establishes room membership.
            await _connection.InvokeAsync("JoinRoom", _roomCode, _playerName, _playerId);

        _connection.On<RelayEnvelope>("Receive", HandleEnvelopeReceived);
    }

    public async Task StartAsync()
    {
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinRoom", _roomCode, _playerName, _playerId);
    }

    public async Task PublishMessage(TransportMessage message)
    {
        if (_connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Relay is not connected.");

        var envelope = new RelayEnvelope(
            SenderId: _connection.ConnectionId ?? string.Empty,
            Payload: JsonSerializer.Serialize(message),
            SequenceNumber: Interlocked.Increment(ref _sequenceNumber),
            Timestamp: DateTime.UtcNow);

        await _connection.InvokeAsync("Relay", _roomCode, envelope);
    }

    public void Subscribe(Action<TransportMessage> onMessageReceived)
    {
        lock (_subscribers) _subscribers.Add(onMessageReceived);
    }

    private void HandleEnvelopeReceived(RelayEnvelope envelope)
    {
        var message = JsonSerializer.Deserialize<TransportMessage>(envelope.Payload)!;
        Action<TransportMessage>[] snapshot;
        lock (_subscribers) snapshot = _subscribers.ToArray();
        foreach (var subscriber in snapshot) subscriber(message);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
```

**Implementation note:** `HandleEnvelopeReceived` is invoked on SignalR's handler thread. The publisher should marshal subscriber callbacks to the application's `SynchronizationContext` (or dispatch via an `IObservable<T>` with a scheduler) to avoid cross-thread UI exceptions in Avalonia/WPF hosts.

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

### 5. Matchmaking & discovery — *resolved per #1230*

**Shareable room codes**, no listable lobby. Creating a game calls `CreateRoom()`, which creates a room and yields the short code from #1225; joiners enter the code via `JoinRoom()`. No accounts, no state beyond the hub's group membership — fits the anonymous-identity constraint. A listable public lobby is a possible later addition, not in this PRD.

**Room capacity has two independent limits**, both surfaced as hub errors rather than a proactive status query:

- **Per-room "full"** = the game has already started, not a player headcount. The host calls the new `CloseRoom(roomCode)` when leaving the lobby stage (deployment begins); `JoinRoom` afterward returns `ErrorCode.RoomFull` for new players. A player whose `playerId` is already in the room's roster may still rejoin (for reconnection). There is no numeric player cap — sizing a game is a rules concern, not a transport one.
- **Hub-wide capacity** caps the number of concurrent rooms the relay will run, to protect the minimal/cheap hosting chosen in #1229 from overload. It's a **config value on the relay** (e.g. `MaxConcurrentRooms` in `appsettings`), not a number pinned in this PRD — the right figure depends on the actual host's memory/CPU headroom under real traffic, which isn't known yet. `CreateRoom()` rejects with `ErrorCode.HubAtCapacity` (carrying the current active-room count) once the limit is reached; there's no separate "check capacity first" endpoint, keeping the hub's surface minimal.

### 6. Connection resilience — *resolved (deferred scope) per #1231*

**Resync and persistence are explicitly not built in this effort.** Committing to an in-memory command log (or any resync mechanism) needs a resource-consumption test first — how memory scales with room count and game length on the minimal/cheap hosting chosen in #1229 — rather than a guess baked into the protocol now. That research + the actual resync mechanism, stable-identity reconnection, and optional host-side persistence are bundled as a **future effort**.

What's settled now, so that future effort doesn't require an identity migration:

- `JoinRoom` takes the caller's own `IPlayer.Id` directly (`Task<JoinResult> JoinRoom(string roomCode, string playerName, Guid playerId)`) — the hub never invents its own player identity. Whatever reconnect/resync mechanism gets built later can key off this same ID unchanged.
- **No other player-identity concept is introduced.** `JoinResult.PlayerId` simply echoes what was passed in.

**Client reconnection (v1 behavior):**

```text
1. Client detects connection loss (SignalR auto-reconnect)
2. Client re-joins the room with the same IPlayer.Id
   - Hub allows this because the playerId is already in the room's roster
   - New players are still rejected (RoomFull) once the game has started
3. Hub notifies the host of reconnection (OnPeerConnected)
4. No resync — the client resumes receiving new live messages only;
   anything missed during the gap is lost (accepted v1 limitation)
```

**Host loss (unchanged from #1225):**

```text
1. Hub detects host disconnection (OnDisconnectedAsync)
2. Hub notifies all clients in room (OnError with HostDisconnected)
3. Clients gracefully exit to main menu
4. Room marked for dissolution after 30 s grace period
5. No host migration — game ends (GameEndedCommand / shutdown lifecycle).
   This is a permanent design principle, not a v1 shortcut: host migration
   would require ServerGame to rehydrate authoritative state (it owns the
   dice roller and generates resolutions, unlike the purely event-sourced
   ClientGame) — deliberately out of scope, always.
```

When the future resync effort is scoped, it should cover: command-log replay vs. snapshot (informed by the resource-consumption test), and optionally host-side log/state persistence so the *same* host can recover after a crash or restart — entirely host-local, no relay/hub changes required for that part.

### 7. Trust & authority — *resolved per #1232*

The relay is opaque and an ordinary client machine is authoritative, so a malicious peer could in principle forge server-origin commands or impersonate a player. **v1 posture: anonymous, accept-risk** — no per-user authentication. This is acceptable because the hub-tagged `SenderId` on every `RelayEnvelope` (§1) combined with the `HostId` returned in `JoinResult` provides anti-spoof hardening: receiving clients verify that `RelayEnvelope.SenderId` matches the known `HostId` before treating a message as server-origin. Messages claiming server origin but arriving from a different `SenderId` are dropped. The layered infra protections below cover the remaining realistic abuse surface for a small, cheap relay:

- Room-code entropy + `JoinRoom` rate limit (10/min/IP, #1225).
- **New:** per-connection rate limit on `Relay()` calls — a config value (no number pinned here, same reasoning as the #1230 capacity cap: real limits need load data, not a guess).
- Message size limit (256 KB, #1225) and hub-wide concurrent-room capacity (#1230).
- **New: static shared API key.** A single key baked into official clients, required to connect to the hub at all. Since the browser client can't set custom WebSocket headers, it travels as a query-string parameter on the hub URL and is checked by connection middleware before the WS upgrade completes. This is **explicitly not real protection** — it's extractable from any client build and doesn't identify individual users — its only job is filtering out low-effort automated/opportunistic traffic hitting a public endpoint.

**Future extension (named, not built now): OAuth2/JWT.** Players authenticate against an external identity provider and connect with a bearer token. Nothing decided here blocks this — SignalR/ASP.NET Core authenticate at the connection level (`[Authorize]` + JWT bearer middleware), which is orthogonal to the `RelayEnvelope`/room contract.

### 8. Security considerations — *specified per #1225, #1232*

Minimal hardening built into the hub contract:

| Measure | Detail |
|---|---|
| Sender verification | Hub attaches `SenderId` (connection ID) to every `RelayEnvelope`; `JoinResult` returns the host's `HostId`. Clients verify `SenderId == HostId` before treating a message as server-origin. |
| Room code entropy | 6-char base32 ≈ ~1 billion combinations; sufficient against brute-force join. |
| Rate limiting (join) | Max 10 join attempts per minute per IP. |
| Rate limiting (in-room) | Per-connection cap on `Relay()` calls (config value, #1232) — prevents a hostile or malfunctioning client from flooding the hub/peers once inside a room. |
| Capacity limit | Hub-wide concurrent-room cap (config value, #1230) protects the relay from resource exhaustion; `CreateRoom()` rejects with `HubAtCapacity` once reached. |
| Connection gate | Static shared API key, passed as a query-string parameter on the hub URL, checked before the WS upgrade completes (#1232). **Not real protection** — filters low-effort automated traffic only; superseded by OAuth2/JWT when that's built. |
| Message size limit | 256 KB max per message (well above any game command payload). |
| Anonymous identity | `PlayerId` = GUID generated per game; `PlayerName` = user-entered, in-memory only. Duplicate names are permitted — command routing uses `PlayerId`, not the name. The lobby UI should handle duplicates gracefully (e.g. disambiguate visually). No persistent accounts or authentication. |

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

Per research (#1227), on the SignalR path settled in #1229:

- **Chosen:** deploy the thin relay as a small ASP.NET Core container on **Fly.io** (~$3–6/mo, always-on, runs the existing Dockerfile) or a **Hetzner VPS** (~€6/mo + Caddy for automatic `wss` TLS). A trimmed/AOT relay image is ~25–30 MiB and needs well under 256 MB RAM.
- **Not pursued:** Cloudflare Workers + Durable Objects (WebSocket Hibernation), $0–5/mo — ruled out because it would require rewriting the relay as edge JS/TS (no SignalR support), which #1229 decided against.
- **Free fallback:** Oracle Cloud Always-Free A1 ARM.
- **Avoid** scale-to-zero platforms for a persistent WS relay (cold starts drop/delay connects); note Azure SignalR Free hard-caps at 20 concurrent connections.

## Implementation Plan

> All decision tickets (#1225, #1229, #1230, #1231, #1232) are now resolved — room lifecycle, API, message envelope, role establishment, transport choice, matchmaking, resilience scope, and trust/security measures are specified above. Implementation can proceed in the phases below.

### Phase 1: Relay transport
- Implement `RelayClientPublisher : ITransportPublisher` (outbound `wss`, room join, opaque fan-out).
- Add relay wiring to `GameManager.InitializeLobby()` / `JoinGameViewModel` alongside the existing factory.
- Unit-test the publisher against `CommandTransportAdapter` round-tripping.

### Phase 2: Cloud relay hub
- Implement the thin SignalR `RelayHub` per the settled contract (#1225): `IRelayHub` interface, `IRoomManager`, room lifecycle (Created → Active → Dissolved), 6-char base32 codes, 2 h TTL.
- Implement `HubMessage` event flow (`PeerConnected`, `PeerDisconnected`, `OnError`, etc.).
- Static API key check (query-string, before WS upgrade) and per-connection `Relay()` rate limit, alongside the existing join-rate-limit (settled per #1232).
- Containerize; deploy to the chosen host (#1229/#1227) with `wss`/TLS.

### Phase 3: Room lifecycle & matchmaking
- Room-code creation on "start server"; join-by-code in `JoinGameViewModel`.
- Creator-is-server role establishment (settled per #1225).
- Hub-wide `MaxConcurrentRooms` config value; `CreateRoom()` returns `HubAtCapacity` with active-room count once reached (#1230).
- `CloseRoom(roomCode)` called by the host on leaving the lobby stage; `JoinRoom` rejects with `RoomFull` for new players afterward, but allows known players to rejoin for reconnection (#1230).

### Phase 4: Web host enablement
- Replace `DummyNetworkHostService` on the browser head with the relay-client attach path.
- Verify `ServerGame` hosts in the WASM head (per #1228 — no engine change expected).

### Phase 5: Resilience
- `JoinRoom` accepts the caller's own `IPlayer.Id`; the room tracks known players so a closed room still allows known players to rejoin (settled per #1231).
- No resync in this phase: a reconnect re-joins the room but does not recover missed state — explicitly deferred pending a resource-consumption test (#1231).
- Host-loss: hub notifies clients (`HostDisconnected`) → clients exit to menu → room dissolved after 30 s (settled per #1225); no host migration, ever.

### Phase 6: Migration & polish
- LAN `SignalRHostService` path retained unchanged as a separate "Host LAN" option, alongside the new "Host Online" (relay) option (settled per #1229) — no removal/migration work in this effort.
- OAuth2/JWT authentication is a named future extension (#1232), out of scope for this PRD.
- End-to-end tests across desktop ↔ web ↔ mobile through the relay.

## Open Questions

None — all decision tickets (#1225, #1229, #1230, #1231, #1232) are resolved. Deliberately deferred items (not open questions, but future work called out above): resync/persistence (#1231) pending a resource-consumption test, and OAuth2/JWT authentication (#1232).

## Success Criteria

- **Cross-network play:** two players on different NATs complete a full game with no port-forwarding.
- **Web hosting:** a browser/WASM client hosts a game (runs `ServerGame`) that desktop/mobile clients join.
- **Transport transparency:** `ServerGame`/`ClientGame` are unchanged; only a new `ITransportPublisher` and the cloud relay are added.
- **Cost:** running relay stays within a few $/month (or self-hosted).
- **Reconnection:** a dropped player can rejoin a game in progress and resume receiving live commands (without resync of missed state).
- **Graceful host loss:** if the host disconnects, all clients are cleanly notified and returned to the menu.

## Summary

The relay-hub model unlocks internet and web multiplayer by **inverting the topology, not rewriting the game**: a dumb cloud WebSocket relay fans commands between parties that all connect outbound, while the authoritative `ServerGame` keeps running on a participant's machine. Research confirms the browser can host with no engine changes (#1228) and that a thin SignalR relay is the pragmatic transport (#1226), hostable within budget (#1227). All five decision tickets are now resolved: the hub contract (#1225), relay transport (#1229 — keep SignalR; new `RelayHub`/`RelayClientPublisher` classes in `Sanet.Transport.SignalR`; LAN path retained unchanged), matchmaking (#1230 — room codes; "full" = game started but known players may rejoin; config-driven hub capacity cap), connection resilience (#1231 — resync and persistence deliberately deferred pending a resource-consumption test; `IPlayer.Id` used as the hub identity from day one; room tracks known players for reconnection; no host migration, ever), and trust & authority (#1232 — anonymous access plus layered infra protections, `HostId` in `JoinResult` for anti-spoof verification, and a static API key now; OAuth2/JWT named as the future extension). Implementation can proceed in the phases above.
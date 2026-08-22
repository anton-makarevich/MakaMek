#Requires -Version 7
<#
.SYNOPSIS
    End-to-end smoke test for the deployed MakaMek relay hub.

.DESCRIPTION
    Verifies the acceptance criteria of issue #1243:
      1. Health endpoint responds over valid TLS.
      2. Room creation + join via authenticated REST API.
      3. Two peers connect to the SignalR relay over WSS using relay tickets.
      4. An opaque message is relayed from host to joiner.

.PARAMETER BaseUrl
    Base URL of the deployed hub, e.g. https://demohub.makamek.nl

.PARAMETER ApiKey
    The shared API key (Hub__ApiKey configured at deployment).

.EXAMPLE
    $key = Read-Host -AsSecureString
    ./scripts/hub-smoke-test.ps1 -BaseUrl https://demohub.makamek.nl -ApiKey $key
#>
param(
    [Parameter(Mandatory)] [Uri] $BaseUrl,
    [Parameter(Mandatory)] [securestring] $ApiKey
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------ helpers

function Assert-Step {
    param([string] $Name, [bool] $Condition, [string] $Details = '')
    if ($Condition) {
        Write-Host "[PASS] $Name"
    }
    else {
        Write-Host "[FAIL] $Name $Details" -ForegroundColor Red
        exit 1
    }
}

function Send-Frame {
    param([System.Net.WebSockets.WebSocket] $Ws, [string] $Text)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $cts = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(15))
    $Ws.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()
}

function Receive-UntilTarget {
    <#
        Reads WebSocket frames until a SignalR record with the given target arrives.
        Pass '__handshake__' to capture the first non-ping record (the handshake reply).
        Returns $null on close or timeout.
    #>
    param(
        [System.Net.WebSockets.WebSocket] $Ws,
        [string] $Target,
        [int] $TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $buffer = [byte[]]::new(64 * 1024)
    $message = ''

    while ([DateTime]::UtcNow -lt $deadline) {
        $segmentEnd = $false
        while (-not $segmentEnd) {
            $count = $Ws.ReceiveAsync([ArraySegment[byte]]::new($buffer), [Threading.CancellationToken]::None).GetAwaiter().GetResult()
            if ($count.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                return $null
            }
            $message += [Text.Encoding]::UTF8.GetString($buffer, 0, $count.Count)
            $segmentEnd = $count.EndOfMessage
        }

        # A single receive may contain multiple 0x1E-terminated SignalR records.
        foreach ($record in $message.Split([char]0x1E)) {
            if (-not $record) { continue }
            $parsed = $record | ConvertFrom-Json
            if ($Target -eq '__handshake__') {
                if ($parsed.type -eq 6) { continue } # ignore pings while waiting
                return $parsed
            }
            if ($parsed.target -eq $Target) {
                return $parsed
            }
        }
        $message = ''
    }
    return $null
}

function Get-RelayTicket {
    param([string] $Token)
    $h = $headers + @{ 'Session-Token' = $Token }
    (Invoke-RestMethod -Uri "$base/api/rooms/$roomCode/relay-ticket" -Method Post -Headers $h).ticket
}

function ConvertTo-WsUri {
    param([string] $ConnectionToken, [string] $Ticket)
    $builder = [UriBuilder]::new($BaseUrl)
    $builder.Scheme = if ($builder.Scheme -eq 'https') { 'wss' } else { 'ws' }
    $builder.Path = "$($builder.Path.TrimEnd('/'))/hubs/relay"
    $builder.Query =
        "id=$([Uri]::EscapeDataString($ConnectionToken))&ticket=$([Uri]::EscapeDataString($Ticket))"
    $builder.Uri
}

function Connect-ClientWebSocket {
    param([System.Net.WebSockets.ClientWebSocket] $Ws, [Uri] $Uri)
    $cts = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(30))
    $Ws.ConnectAsync($Uri, $cts.Token).GetAwaiter().GetResult()
}

function Complete-SignalRHandshake {
    param([System.Net.WebSockets.ClientWebSocket] $Ws, [string] $HandshakeRecord)
    Send-Frame $Ws $HandshakeRecord
    $reply = Receive-UntilTarget -WebSocket $Ws -Target '__handshake__' -TimeoutSeconds 15
    if (-not $reply) { throw 'SignalR handshake failed.' }
}

function Close-Peer {
    param([System.Net.WebSockets.WebSocket] $Ws)
    try {
        $cts = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(5))
        $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'done', $cts.Token).GetAwaiter().GetResult()
    }
    finally {
        $Ws.Dispose()
    }
}

# --------------------------------------------------------------------- setup

$plainApiKey = ([System.Net.NetworkCredential]::new('', $ApiKey)).Password
$headers = @{ 'X-Api-Key' = $plainApiKey }
$base = $BaseUrl.AbsoluteUri.TrimEnd('/')
$handshakeRecord = '{"protocol":"json","version":1}' + [char]0x1E

# ------------------------------------------------------------- 1. Health check
Write-Host "==> Health check: $base/health"
$health = Invoke-RestMethod -Uri "$base/health" -Method Get -TimeoutSec 30
Assert-Step 'GET /health returns healthy status' ($health.status -eq 'healthy') `
    "(got: $($health | ConvertTo-Json -Compress))"

# ------------------------------------------------------------ 2. Create a room
Write-Host '==> Creating room'
$create = Invoke-RestMethod -Uri "$base/api/rooms" -Method Post -Headers $headers `
    -ContentType 'application/json' -Body (@{ gameId = [Guid]::NewGuid().ToString() } | ConvertTo-Json)
Assert-Step 'POST /api/rooms succeeds' ($create.success -and $create.roomCode) `
    "(got: $($create | ConvertTo-Json -Compress))"
$roomCode = $create.roomCode
$hostToken = $create.sessionToken

# --------------------------------------------------------------- 3. Mark ready
Write-Host "==> Marking room $roomCode ready"
$readyHeaders = $headers + @{ 'Session-Token' = $hostToken }
$ready = Invoke-RestMethod -Uri "$base/api/rooms/$roomCode/ready" -Method Post -Headers $readyHeaders
Assert-Step 'POST /api/rooms/{code}/ready succeeds' $ready.success

# ----------------------------------------------------------------------- 4. Join
Write-Host "==> Joining room $roomCode as second peer"
$join = Invoke-RestMethod -Uri "$base/api/rooms/$roomCode/join" -Method Post -Headers $headers
Assert-Step 'POST /api/rooms/{code}/join succeeds' ($join.success -and $join.sessionToken)
$clientToken = $join.sessionToken

# -------------------------------------------------------------- 5. Relay tickets
Write-Host '==> Requesting relay tickets'
$hostTicket = Get-RelayTicket $hostToken
$clientTicket = Get-RelayTicket $clientToken
Assert-Step 'Relay tickets issued for both peers' ($hostTicket -and $clientTicket)

# ---------------------------------------------- 6. SignalR connections over WSS
Write-Host '==> Connecting both peers over WSS'

$wsHost = [System.Net.WebSockets.ClientWebSocket]::new()
$wsClient = [System.Net.WebSockets.ClientWebSocket]::new()

try {
    # Connect host
    $negotiateHost = Invoke-RestMethod `
        -Uri "$base/hubs/relay/negotiate?negotiateVersion=1&ticket=$([Uri]::EscapeDataString($hostTicket))" `
        -Method Post -Headers $headers
    $hostWsUri = ConvertTo-WsUri -ConnectionToken $negotiateHost.connectionToken -Ticket $hostTicket
    Connect-ClientWebSocket $wsHost $hostWsUri
    Complete-SignalRHandshake $wsHost $handshakeRecord

    # Connect client
    $negotiateClient = Invoke-RestMethod `
        -Uri "$base/hubs/relay/negotiate?negotiateVersion=1&ticket=$([Uri]::EscapeDataString($clientTicket))" `
        -Method Post -Headers $headers
    $clientWsUri = ConvertTo-WsUri -ConnectionToken $negotiateClient.connectionToken -Ticket $clientTicket
    Connect-ClientWebSocket $wsClient $clientWsUri
    Complete-SignalRHandshake $wsClient $handshakeRecord

    Write-Host '[PASS] Both peers completed SignalR handshake over WSS'

    # --------------------------------------------------------- 7. Relay message
    $opaquePayload = @{ kind = 'smoke-test'; stamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() } |
        ConvertTo-Json -Compress
    Write-Host '==> Host relays an opaque message'

    $invocation = @{
        target    = 'Relay'
        type      = 1
        arguments = @($roomCode, @{ payload = $opaquePayload; sequenceNumber = 1; senderId = $null })
    } | ConvertTo-Json -Depth 5 -Compress
    Send-Frame $wsHost ($invocation + [char]0x1E)

    $received = Receive-UntilTarget -WebSocket $wsClient -Target 'OnReceive' -TimeoutSeconds 30
    Assert-Step 'Joiner received OnReceive with matching payload' (
        $null -ne $received -and $received.arguments[0].payload -eq $opaquePayload)

    Write-Host ''
    Write-Host 'SMOKE TEST PASSED' -ForegroundColor Green
}
finally {
    foreach ($ws in @($wsHost, $wsClient)) {
        if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Closed) {
            Close-Peer $ws
        }
        else {
            $ws.Dispose()
        }
    }
}

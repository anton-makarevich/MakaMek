using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// HTTP implementation of <see cref="IRelayRoomClient"/> against the Hub REST room API.
/// </summary>
public sealed class RelayRoomClient : IRelayRoomClient
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string SessionTokenHeaderName = "Session-Token";
    private const string HostRole = "Host";

    private readonly HttpClient _httpClient;
    private readonly RelayClientOptions _options;
    private readonly ILogger<RelayRoomClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RelayRoomClient(
        HttpClient httpClient,
        IOptions<RelayClientOptions> options,
        ILogger<RelayRoomClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        _jsonOptions.Converters.Add(new TolerantHubErrorCodeConverter());
    }

    public async Task<RoomCreateResult> CreateAsync(
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating relay room for player {PlayerId}",
                playerId);

            using var request = CreateRequest(
                HttpMethod.Post,
                "api/rooms",
                sessionToken: null);
            request.Content = JsonContent.Create(
                new CreateRoomRequest(playerName, playerId),
                options: _jsonOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (TryMapSpecialStatus(response.StatusCode, body, out var specialError))
            {
                return RoomCreateResult.Failed(specialError);
            }

            var payload = DeserializeOrNull<CreateRoomResponse>(body);
            if (payload is null)
            {
                return RoomCreateResult.Failed(DeserializationError());
            }

            if (response.IsSuccessStatusCode && payload.Success
                && !string.IsNullOrEmpty(payload.RoomCode)
                && !string.IsNullOrEmpty(payload.SessionToken)
                && payload.HostId is { } hostId)
            {
                _logger.LogInformation(
                    "Created relay room {RoomCode} for player {PlayerId}",
                    payload.RoomCode,
                    playerId);

                return RoomCreateResult.Succeeded(
                    payload.RoomCode,
                    payload.SessionToken,
                    HostRole,
                    playerId,
                    hostId);
            }

            return RoomCreateResult.Failed(MapHubError(payload.Error, response.StatusCode));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Relay create room request timed out for player {PlayerId}", playerId);
            return RoomCreateResult.Failed(TimeoutError());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Relay create room network error for player {PlayerId}", playerId);
            return RoomCreateResult.Failed(NetworkError());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Relay create room deserialization error for player {PlayerId}", playerId);
            return RoomCreateResult.Failed(DeserializationError());
        }
    }

    public async Task<RoomJoinResult> JoinAsync(
        string roomCode,
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Joining relay room {RoomCode} as player {PlayerId}",
                roomCode,
                playerId);

            using var request = CreateRequest(
                HttpMethod.Post,
                $"api/rooms/{Uri.EscapeDataString(roomCode)}/join",
                sessionToken: null);
            request.Content = JsonContent.Create(
                new JoinRequest(playerName, playerId),
                options: _jsonOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (TryMapSpecialStatus(response.StatusCode, body, out var specialError))
            {
                return RoomJoinResult.Failed(specialError);
            }

            var payload = DeserializeOrNull<JoinResponse>(body);
            if (payload is null)
            {
                return RoomJoinResult.Failed(DeserializationError());
            }

            if (response.IsSuccessStatusCode && payload.Success
                && !string.IsNullOrEmpty(payload.SessionToken)
                && !string.IsNullOrEmpty(payload.Role)
                && payload.PlayerId is { } joinedPlayerId
                && payload.HostId is { } hostId)
            {
                _logger.LogInformation(
                    "Joined relay room {RoomCode} as player {PlayerId} with role {Role}",
                    roomCode,
                    joinedPlayerId,
                    payload.Role);

                return RoomJoinResult.Succeeded(
                    roomCode,
                    payload.SessionToken,
                    payload.Role,
                    joinedPlayerId,
                    hostId);
            }

            return RoomJoinResult.Failed(MapHubError(payload.Error, response.StatusCode));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Relay join room request timed out for room {RoomCode}", roomCode);
            return RoomJoinResult.Failed(TimeoutError());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Relay join room network error for room {RoomCode}", roomCode);
            return RoomJoinResult.Failed(NetworkError());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Relay join room deserialization error for room {RoomCode}", roomCode);
            return RoomJoinResult.Failed(DeserializationError());
        }
    }

    public Task<RoomOperationResult> ReadyAsync(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default) =>
        SendAckAsync(
            HttpMethod.Post,
            $"api/rooms/{Uri.EscapeDataString(roomCode)}/ready",
            roomCode,
            sessionToken,
            cancellationToken);

    public Task<RoomOperationResult> CloseAsync(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default) =>
        SendAckAsync(
            HttpMethod.Post,
            $"api/rooms/{Uri.EscapeDataString(roomCode)}/close",
            roomCode,
            sessionToken,
            cancellationToken);

    public async Task<RoomOperationResult> RemoveMemberAsync(
        string roomCode,
        string sessionToken,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Removing member {PlayerId} from relay room {RoomCode}",
                playerId,
                roomCode);

            using var request = CreateRequest(
                HttpMethod.Delete,
                $"api/rooms/{Uri.EscapeDataString(roomCode)}/members/{playerId:D}",
                sessionToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // RemoveMember returns a bare 401 (no HubError body) when the session token is missing.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return RoomOperationResult.Failed(UnauthorizedError());
            }

            if (TryMapSpecialStatus(response.StatusCode, body, out var specialError))
            {
                return RoomOperationResult.Failed(specialError);
            }

            var payload = DeserializeOrNull<RemoveMemberResponse>(body);
            if (payload is null)
            {
                return RoomOperationResult.Failed(DeserializationError());
            }

            if (response.IsSuccessStatusCode && payload.Success)
            {
                _logger.LogInformation(
                    "Removed member {PlayerId} from relay room {RoomCode}",
                    playerId,
                    roomCode);
                return RoomOperationResult.Succeeded();
            }

            return RoomOperationResult.Failed(MapHubError(payload.Error, response.StatusCode));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Relay remove-member request timed out for room {RoomCode}", roomCode);
            return RoomOperationResult.Failed(TimeoutError());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Relay remove-member network error for room {RoomCode}", roomCode);
            return RoomOperationResult.Failed(NetworkError());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Relay remove-member deserialization error for room {RoomCode}", roomCode);
            return RoomOperationResult.Failed(DeserializationError());
        }
    }

    private async Task<RoomOperationResult> SendAckAsync(
        HttpMethod method,
        string relativePath,
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Sending {Method} for relay room {RoomCode}",
                method.Method,
                roomCode);

            using var request = CreateRequest(method, relativePath, sessionToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (TryMapSpecialStatus(response.StatusCode, body, out var specialError))
            {
                return RoomOperationResult.Failed(specialError);
            }

            // Ready and Close share the same Success/Error shape.
            var payload = DeserializeOrNull<ReadyResponse>(body);
            if (payload is null)
            {
                return RoomOperationResult.Failed(DeserializationError());
            }

            if (response.IsSuccessStatusCode && payload.Success)
            {
                _logger.LogInformation(
                    "Relay room {RoomCode} {Method} succeeded",
                    roomCode,
                    method.Method);
                return RoomOperationResult.Succeeded();
            }

            return RoomOperationResult.Failed(MapHubError(payload.Error, response.StatusCode));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Relay {Method} request timed out for room {RoomCode}", method.Method, roomCode);
            return RoomOperationResult.Failed(TimeoutError());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Relay {Method} network error for room {RoomCode}", method.Method, roomCode);
            return RoomOperationResult.Failed(NetworkError());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Relay {Method} deserialization error for room {RoomCode}", method.Method, roomCode);
            return RoomOperationResult.Failed(DeserializationError());
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string? sessionToken)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var uri = string.IsNullOrEmpty(baseUrl)
            ? new Uri(relativePath, UriKind.Relative)
            : new Uri($"{baseUrl}/{relativePath}", UriKind.Absolute);

        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, _options.ApiKey);

        if (!string.IsNullOrEmpty(sessionToken))
        {
            request.Headers.TryAddWithoutValidation(SessionTokenHeaderName, sessionToken);
        }

        return request;
    }

    private T? DeserializeOrNull<T>(string body) where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(body, _jsonOptions);
    }

    private static bool TryMapSpecialStatus(
        HttpStatusCode statusCode,
        string body,
        out RelayClientError error)
    {
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            error = UnauthorizedError();
            return true;
        }

        if (statusCode == HttpStatusCode.BadRequest)
        {
            error = new RelayClientError(
                RelayClientErrorCode.ValidationError,
                ExtractValidationMessage(body));
            return true;
        }

        error = null!;
        return false;
    }

    private static string ExtractValidationMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "The request failed validation.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message.
        }

        return "The request failed validation.";
    }

    private RelayClientError MapHubError(HubError? hubError, HttpStatusCode statusCode)
    {
        if (hubError is null)
        {
            _logger.LogWarning(
                "Relay room request failed with status {StatusCode} and no HubError body",
                (int)statusCode);
            return new RelayClientError(
                RelayClientErrorCode.Unknown,
                $"The relay returned HTTP {(int)statusCode}.");
        }

        var code = MapHubErrorCode(hubError.Code);
        _logger.LogWarning(
            "Relay room request failed with status {StatusCode} and error {ErrorCode}",
            (int)statusCode,
            code);

        // Prefer the Hub's public message — it never contains credentials.
        var message = string.IsNullOrWhiteSpace(hubError.Message)
            ? $"Relay error: {code}."
            : hubError.Message;

        return new RelayClientError(code, message);
    }

    private static RelayClientErrorCode MapHubErrorCode(HubErrorCode code) =>
        code switch
        {
            HubErrorCode.HubAtCapacity => RelayClientErrorCode.HubAtCapacity,
            HubErrorCode.RoomNotFound => RelayClientErrorCode.RoomNotFound,
            HubErrorCode.RoomExpired => RelayClientErrorCode.RoomExpired,
            HubErrorCode.HostNotReady => RelayClientErrorCode.HostNotReady,
            HubErrorCode.NotHost => RelayClientErrorCode.NotHost,
            HubErrorCode.RateLimited => RelayClientErrorCode.RateLimited,
            HubErrorCode.MessageTooLarge => RelayClientErrorCode.MessageTooLarge,
            HubErrorCode.HostPlayerIdConflict => RelayClientErrorCode.HostPlayerIdConflict,
            HubErrorCode.RoomFull => RelayClientErrorCode.RoomFull,
            HubErrorCode.InvalidRoomState => RelayClientErrorCode.InvalidRoomState,
            HubErrorCode.MemberNotFound => RelayClientErrorCode.MemberNotFound,
            HubErrorCode.CannotRemoveHost => RelayClientErrorCode.CannotRemoveHost,
            HubErrorCode.HostDisconnected => RelayClientErrorCode.HostDisconnected,
            HubErrorCode.ConnectionSuperseded => RelayClientErrorCode.ConnectionSuperseded,
            _ => RelayClientErrorCode.Unknown
        };

    private static RelayClientError UnauthorizedError() =>
        new(RelayClientErrorCode.Unauthorized, "The relay rejected the request as unauthorized.");

    private static RelayClientError TimeoutError() =>
        new(RelayClientErrorCode.Timeout, "The relay request timed out.");

    private static RelayClientError NetworkError() =>
        new(RelayClientErrorCode.NetworkError, "A network error occurred while contacting the relay.");

    private static RelayClientError DeserializationError() =>
        new(RelayClientErrorCode.DeserializationError, "The relay response could not be read.");

    /// <summary>
    /// Converts <see cref="HubErrorCode"/> from JSON strings, mapping unrecognized values
    /// to a sentinel so <see cref="MapHubErrorCode"/>'s <c>_ => Unknown</c> fallback fires.
    /// </summary>
    private sealed class TolerantHubErrorCodeConverter : JsonConverter<HubErrorCode>
    {
        public override HubErrorCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (Enum.TryParse<HubErrorCode>(value, ignoreCase: false, out var result))
                    return result;
                return (HubErrorCode)int.MaxValue;
            }
            if (reader.TokenType == JsonTokenType.Number)
                return (HubErrorCode)reader.GetInt32();
            return (HubErrorCode)int.MaxValue;
        }

        public override void Write(Utf8JsonWriter writer, HubErrorCode value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}

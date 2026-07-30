using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

public class RecordingHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = string.Empty;
    public string? ContentType { get; set; } = "application/json";
    public Exception? ThrowException { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        if (ThrowException is not null)
        {
            throw ThrowException;
        }

        var response = new HttpResponseMessage(StatusCode);
        if (ContentType is not null)
        {
            response.Content = new StringContent(ResponseContent, Encoding.UTF8, ContentType);
        }

        return response;
    }
}

public class RelayRoomClientTests
{
    private const string BaseUrl = "https://hub.example.test";
    private const string ApiKey = "test-api-key-secret-value";
    private const string SessionToken = "test-session-token-secret-value";

    private readonly RecordingHttpMessageHandler _handler = new();
    private readonly ILogger<RelayRoomClient> _logger = Substitute.For<ILogger<RelayRoomClient>>();
    private readonly RelayRoomClient _sut;

    public RelayRoomClientTests()
    {
        var httpClient = new HttpClient(_handler);
        var options = Options.Create(new RelayClientOptions
        {
            BaseUrl = BaseUrl,
            ApiKey = ApiKey
        });
        _sut = new RelayRoomClient(httpClient, options, _logger);
    }

    [Fact]
    public async Task CreateAsync_Success_PreservesRoomIdentityAndSendsApiKey()
    {
        var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var hostId = playerId;
        _handler.StatusCode = HttpStatusCode.Created;
        _handler.ResponseContent = """
            {
              "success": true,
              "roomCode": "ABCDEF",
              "hostId": "11111111-1111-1111-1111-111111111111",
              "sessionToken": "test-session-token-secret-value",
              "expiresAt": "2026-07-30T22:00:00Z",
              "error": null
            }
            """;

        var result = await _sut.CreateAsync(playerId, "HostPlayer");

        result.Success.ShouldBeTrue();
        result.RoomCode.ShouldBe("ABCDEF");
        result.SessionToken.ShouldBe(SessionToken);
        result.Role.ShouldBe("Host");
        result.PlayerId.ShouldBe(playerId);
        result.HostId.ShouldBe(hostId);
        result.Error.ShouldBeNull();

        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.ToString().ShouldBe($"{BaseUrl}/api/rooms");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe(ApiKey);
        _handler.LastRequest.Headers.Contains("Session-Token").ShouldBeFalse();
        _handler.LastRequestBody.ShouldNotBeNull();
        using (var doc = JsonDocument.Parse(_handler.LastRequestBody!))
        {
            doc.RootElement.GetProperty("playerId").GetGuid().ShouldBe(playerId);
            doc.RootElement.GetProperty("playerName").GetString().ShouldBe("HostPlayer");
        }

        AssertNoSecretsLeaked(result.Error?.Message);
    }

    [Fact]
    public async Task JoinAsync_Success_PreservesRoomIdentityAndSendsApiKey()
    {
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var hostId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = """
            {
              "success": true,
              "role": "Client",
              "playerId": "22222222-2222-2222-2222-222222222222",
              "hostId": "11111111-1111-1111-1111-111111111111",
              "sessionToken": "test-session-token-secret-value",
              "error": null
            }
            """;

        var result = await _sut.JoinAsync("ABCDEF", playerId, "Guest");

        result.Success.ShouldBeTrue();
        result.RoomCode.ShouldBe("ABCDEF");
        result.SessionToken.ShouldBe(SessionToken);
        result.Role.ShouldBe("Client");
        result.PlayerId.ShouldBe(playerId);
        result.HostId.ShouldBe(hostId);
        result.Error.ShouldBeNull();

        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.ToString().ShouldBe($"{BaseUrl}/api/rooms/ABCDEF/join");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe(ApiKey);
        _handler.LastRequestBody.ShouldNotBeNull();
        using (var doc = JsonDocument.Parse(_handler.LastRequestBody!))
        {
            doc.RootElement.GetProperty("playerId").GetGuid().ShouldBe(playerId);
            doc.RootElement.GetProperty("playerName").GetString().ShouldBe("Guest");
        }

        AssertNoSecretsLeaked(result.Error?.Message);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("close")]
    public async Task ReadyAndClose_Success_SendsSessionTokenHeader(string operation)
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = """{ "success": true, "error": null }""";

        var result = operation == "ready"
            ? await _sut.ReadyAsync("ABCDEF", SessionToken)
            : await _sut.CloseAsync("ABCDEF", SessionToken);

        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.ToString()
            .ShouldBe($"{BaseUrl}/api/rooms/ABCDEF/{operation}");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe(ApiKey);
        _handler.LastRequest.Headers.GetValues("Session-Token").Single().ShouldBe(SessionToken);
        AssertNoSecretsLeaked(result.Error?.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_Success_SendsDeleteWithHeadersAndPlayerId()
    {
        var memberId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = """{ "success": true, "error": null }""";

        var result = await _sut.RemoveMemberAsync("ABCDEF", SessionToken, memberId);

        result.Success.ShouldBeTrue();
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Delete);
        _handler.LastRequest.RequestUri!.ToString()
            .ShouldBe($"{BaseUrl}/api/rooms/ABCDEF/members/{memberId:D}");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe(ApiKey);
        _handler.LastRequest.Headers.GetValues("Session-Token").Single().ShouldBe(SessionToken);
        AssertNoSecretsLeaked(result.Error?.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "RoomNotFound", RelayClientErrorCode.RoomNotFound)]
    [InlineData(HttpStatusCode.Conflict, "HostNotReady", RelayClientErrorCode.HostNotReady)]
    [InlineData(HttpStatusCode.Conflict, "RoomFull", RelayClientErrorCode.RoomFull)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "HubAtCapacity", RelayClientErrorCode.HubAtCapacity)]
    [InlineData(HttpStatusCode.TooManyRequests, "RateLimited", RelayClientErrorCode.RateLimited)]
    [InlineData(HttpStatusCode.Conflict, "RoomExpired", RelayClientErrorCode.RoomExpired)]
    [InlineData(HttpStatusCode.Conflict, "NotHost", RelayClientErrorCode.NotHost)]
    [InlineData(HttpStatusCode.Conflict, "HostPlayerIdConflict", RelayClientErrorCode.HostPlayerIdConflict)]
    [InlineData(HttpStatusCode.Conflict, "InvalidRoomState", RelayClientErrorCode.InvalidRoomState)]
    [InlineData(HttpStatusCode.NotFound, "MemberNotFound", RelayClientErrorCode.MemberNotFound)]
    [InlineData(HttpStatusCode.Conflict, "CannotRemoveHost", RelayClientErrorCode.CannotRemoveHost)]
    public async Task JoinAsync_HubErrorBody_MapsToClientError(
        HttpStatusCode statusCode,
        string hubCode,
        RelayClientErrorCode expected)
    {
        _handler.StatusCode = statusCode;
        _handler.ResponseContent = $$"""
            {
              "success": false,
              "role": null,
              "playerId": null,
              "hostId": null,
              "sessionToken": null,
              "error": { "code": "{{hubCode}}", "message": "Hub says {{hubCode}}." }
            }
            """;

        var result = await _sut.JoinAsync("ABCDEF", Guid.NewGuid(), "Guest");

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(expected);
        result.Error.Message.ShouldContain(hubCode);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_HubAtCapacity_MapsError()
    {
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseContent = """
            {
              "success": false,
              "roomCode": null,
              "hostId": null,
              "sessionToken": null,
              "expiresAt": null,
              "error": {
                "code": "HubAtCapacity",
                "message": "The relay has reached its concurrent room capacity.",
                "activeRoomCount": 100
              }
            }
            """;

        var result = await _sut.CreateAsync(Guid.NewGuid(), "Host");

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.HubAtCapacity);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task AnyOperation_Unauthorized_MapsToUnauthorized()
    {
        _handler.StatusCode = HttpStatusCode.Unauthorized;
        _handler.ResponseContent = string.Empty;
        _handler.ContentType = "text/plain";

        var result = await _sut.CreateAsync(Guid.NewGuid(), "Host");

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unauthorized);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_BareUnauthorized_MapsToUnauthorized()
    {
        _handler.StatusCode = HttpStatusCode.Unauthorized;
        _handler.ResponseContent = string.Empty;
        _handler.ContentType = "text/plain";

        var result = await _sut.RemoveMemberAsync("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unauthorized);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_ValidationProblem_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = """
            {
              "title": "One or more validation errors occurred.",
              "errors": { "Session-Token": ["Session-Token header is required."] }
            }
            """;

        var result = await _sut.ReadyAsync("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("One or more validation errors occurred.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_NetworkFailure_MapsToNetworkError()
    {
        _handler.ThrowException = new HttpRequestException("connection refused");

        var result = await _sut.CreateAsync(Guid.NewGuid(), "Host");

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_Timeout_MapsToTimeout()
    {
        _handler.ThrowException = new TaskCanceledException("timed out");

        var result = await _sut.CreateAsync(Guid.NewGuid(), "Host");

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Timeout);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_InvalidJson_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.Created;
        _handler.ResponseContent = "{ not-json";

        var result = await _sut.CreateAsync(Guid.NewGuid(), "Host");

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_HubError_MapsCode()
    {
        _handler.StatusCode = HttpStatusCode.NotFound;
        _handler.ResponseContent = """
            {
              "success": false,
              "error": { "code": "RoomNotFound", "message": "The specified room was not found." }
            }
            """;

        var result = await _sut.ReadyAsync("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.RoomNotFound);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    private void AssertNoSecretsLeaked(string? errorMessage)
    {
        if (errorMessage is not null)
        {
            errorMessage.ShouldNotContain(ApiKey);
            errorMessage.ShouldNotContain(SessionToken);
        }

        foreach (var call in _logger.ReceivedCalls())
        {
            var formatted = FormatLogCall(call);
            formatted.ShouldNotContain(ApiKey);
            formatted.ShouldNotContain(SessionToken);
        }
    }

    private static string FormatLogCall(NSubstitute.Core.ICall call)
    {
        var args = call.GetArguments();
        if (args.Length < 5 || args[2] is null || args[4] is not Delegate formatter)
        {
            return string.Join(" ", args.Select(a => a?.ToString() ?? string.Empty));
        }

        try
        {
            return formatter.DynamicInvoke(args[2], args[3]) as string
                   ?? string.Join(" ", args.Select(a => a?.ToString() ?? string.Empty));
        }
        catch
        {
            return string.Join(" ", args.Select(a => a?.ToString() ?? string.Empty));
        }
    }
}

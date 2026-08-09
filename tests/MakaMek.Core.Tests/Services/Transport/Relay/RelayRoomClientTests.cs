using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Core.Services.Transport.Relay.Contracts;
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
        cancellationToken.ThrowIfCancellationRequested();
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
    private static readonly Guid HostDeviceSessionId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ClientDeviceSessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly RecordingHttpMessageHandler _handler = new();
    private readonly ILogger<RelayRoomClient> _logger = Substitute.For<ILogger<RelayRoomClient>>();
    private readonly RelayRoomClient _sut;

    public RelayRoomClientTests()
    {
        var httpClient = new HttpClient(_handler);
        var hubConfigurationProvider = Substitute.For<IRelayHubConfigurationProvider>();
        hubConfigurationProvider.GetActiveOptions().Returns(Task.FromResult(new RelayClientOptions
        {
            BaseUrl = BaseUrl,
            ApiKey = ApiKey
        }));
        _sut = new RelayRoomClient(httpClient, hubConfigurationProvider, _logger);
    }

    [Fact]
    public async Task CreateAsync_Success_PreservesRoomIdentityAndSendsApiKey()
    {
        var hostGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _handler.StatusCode = HttpStatusCode.Created;
        _handler.ResponseContent = """
            {
              "success": true,
              "roomCode": "ABCDEF",
              "deviceSessionId": "99999999-9999-9999-9999-999999999999",
              "hostGameId": "11111111-1111-1111-1111-111111111111",
              "sessionToken": "test-session-token-secret-value",
              "expiresAt": "2026-07-30T22:00:00Z",
              "error": null
            }
            """;

        var result = await _sut.Create(hostGameId);

        result.Success.ShouldBeTrue();
        result.RoomCode.ShouldBe("ABCDEF");
        result.SessionToken.ShouldBe(SessionToken);
        result.Role.ShouldBe("Host");
        result.DeviceSessionId.ShouldBe(HostDeviceSessionId);
        result.HostGameId.ShouldBe(hostGameId);
        result.Error.ShouldBeNull();

        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.ToString().ShouldBe($"{BaseUrl}/api/rooms");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe(ApiKey);
        _handler.LastRequest.Headers.Contains("Session-Token").ShouldBeFalse();
        _handler.LastRequestBody.ShouldNotBeNull();
        using (var doc = JsonDocument.Parse(_handler.LastRequestBody!))
        {
            doc.RootElement.GetProperty("gameId").GetGuid().ShouldBe(hostGameId);
        }

        AssertNoSecretsLeaked(result.Error?.Message);
    }

    [Fact]
    public async Task JoinAsync_Success_PreservesRoomIdentityAndSendsApiKey()
    {
        var hostGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = """
            {
              "success": true,
              "role": "Client",
              "deviceSessionId": "22222222-2222-2222-2222-222222222222",
              "hostGameId": "11111111-1111-1111-1111-111111111111",
              "sessionToken": "test-session-token-secret-value",
              "error": null
            }
            """;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeTrue();
        result.RoomCode.ShouldBe("ABCDEF");
        result.SessionToken.ShouldBe(SessionToken);
        result.Role.ShouldBe("Client");
        result.DeviceSessionId.ShouldBe(ClientDeviceSessionId);
        result.HostGameId.ShouldBe(hostGameId);
        result.Error.ShouldBeNull();

        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        _handler.LastRequest.RequestUri!.ToString().ShouldBe($"{BaseUrl}/api/rooms/ABCDEF/join");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe(ApiKey);
        _handler.LastRequestBody.ShouldBeNull();

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
            ? await _sut.Ready("ABCDEF", SessionToken)
            : await _sut.Close("ABCDEF", SessionToken);

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
    public async Task ReadyAsync_UsesConfigurationValueActiveAtCallTime()
    {
        // Arrange
        var provider = Substitute.For<IRelayHubConfigurationProvider>();
        provider.GetActiveOptions().Returns(Task.FromResult(new RelayClientOptions
        {
            BaseUrl = "https://first.example",
            ApiKey = "first-key"
        }));
        var client = new RelayRoomClient(new HttpClient(_handler), provider, _logger);
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = """{ "success": true, "error": null }""";

        // Act - first call uses the initial configuration
        var firstResult = await client.Ready("ABCDEF", SessionToken);

        // Assert
        firstResult.Success.ShouldBeTrue();
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://first.example/api/rooms/ABCDEF/ready");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe("first-key");

        // Update the configuration to different successive values
        provider.GetActiveOptions().Returns(Task.FromResult(new RelayClientOptions
        {
            BaseUrl = "https://second.example",
            ApiKey = "second-key"
        }));

        // Act - second call must use the updated configuration
        var secondResult = await client.Ready("ABCDEF", SessionToken);

        // Assert
        secondResult.Success.ShouldBeTrue();
        _handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://second.example/api/rooms/ABCDEF/ready");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().ShouldBe("second-key");
    }

    [Fact]
    public async Task RemoveMemberAsync_Success_SendsDeleteWithHeadersAndDeviceSessionId()
    {
        var deviceSessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = """{ "success": true, "error": null }""";

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, deviceSessionId);

        result.Success.ShouldBeTrue();
        _handler.LastRequest.ShouldNotBeNull();
        _handler.LastRequest!.Method.ShouldBe(HttpMethod.Delete);
        _handler.LastRequest.RequestUri!.ToString()
            .ShouldBe($"{BaseUrl}/api/rooms/ABCDEF/members/{deviceSessionId:D}");
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
              "deviceSessionId": null,
              "hostGameId": null,
              "sessionToken": null,
              "error": { "code": "{{hubCode}}", "message": "Hub says {{hubCode}}." }
            }
            """;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

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
              "deviceSessionId": null,
              "hostGameId": null,
              "sessionToken": null,
              "expiresAt": null,
              "error": {
                "code": "HubAtCapacity",
                "message": "The relay has reached its concurrent room capacity.",
                "activeRoomCount": 100
              }
            }
            """;

        var result = await _sut.Create(Guid.NewGuid());

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

        var result = await _sut.Create(Guid.NewGuid());

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

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

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

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("One or more validation errors occurred.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_ValidationProblem_NoTitle_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = "{}";

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("The request failed validation.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_ValidationProblem_TitleNotString_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = """{ "title": 42 }""";

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("The request failed validation.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_ValidationProblem_EmptyBody_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = string.Empty;

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("The request failed validation.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_ValidationProblem_NonJsonBody_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = "not-valid-json";

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("The request failed validation.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_NetworkFailure_MapsToNetworkError()
    {
        _handler.ThrowException = new HttpRequestException("connection refused");

        var result = await _sut.Create(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_Timeout_MapsToTimeout()
    {
        _handler.ThrowException = new TaskCanceledException("timed out");

        var result = await _sut.Create(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Timeout);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_InvalidJson_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.Created;
        _handler.ResponseContent = "{ not-json";

        var result = await _sut.Create(Guid.NewGuid());

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

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.RoomNotFound);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task JoinAsync_NetworkFailure_MapsToNetworkError()
    {
        _handler.ThrowException = new HttpRequestException("connection refused");

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task JoinAsync_Timeout_MapsToTimeout()
    {
        _handler.ThrowException = new TaskCanceledException("timed out");

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Timeout);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task JoinAsync_InvalidJson_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = "{ not-json";

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task JoinAsync_EmptyBody_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = string.Empty;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_HubErrorBody_MapsToClientError()
    {
        var memberId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        _handler.StatusCode = HttpStatusCode.NotFound;
        _handler.ResponseContent = """
            {
              "success": false,
              "error": { "code": "MemberNotFound", "message": "Member not found." }
            }
            """;

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, memberId);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.MemberNotFound);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_ValidationProblem_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = """
            {
              "title": "One or more validation errors occurred.",
              "errors": { "Session-Token": ["Session-Token header is required."] }
            }
            """;

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task JoinAsync_ValidationProblem_EmptyBody_MapsToValidationError()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;
        _handler.ResponseContent = string.Empty;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.ValidationError);
        result.Error.Message.ShouldBe("The request failed validation.");
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_NetworkFailure_MapsToNetworkError()
    {
        _handler.ThrowException = new HttpRequestException("connection refused");

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_Timeout_MapsToTimeout()
    {
        _handler.ThrowException = new TaskCanceledException("timed out");

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Timeout);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_InvalidJson_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = "{ not-json";

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

[Fact]
    public async Task RemoveMemberAsync_EmptyBody_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = string.Empty;

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_EmptyBody_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = string.Empty;

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_NetworkFailure_MapsToNetworkError()
    {
        _handler.ThrowException = new HttpRequestException("connection refused");

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_Timeout_MapsToTimeout()
    {
        _handler.ThrowException = new TaskCanceledException("timed out");

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Timeout);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_InvalidJson_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = "{ not-json";

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CloseAsync_NetworkFailure_MapsToNetworkError()
    {
        _handler.ThrowException = new HttpRequestException("connection refused");

        var result = await _sut.Close("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CloseAsync_Timeout_MapsToTimeout()
    {
        _handler.ThrowException = new TaskCanceledException("timed out");

        var result = await _sut.Close("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Timeout);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CloseAsync_InvalidJson_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.OK;
        _handler.ResponseContent = "{ not-json";

        var result = await _sut.Close("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Theory]
    [InlineData("MessageTooLarge", RelayClientErrorCode.MessageTooLarge)]
    [InlineData("HostDisconnected", RelayClientErrorCode.HostDisconnected)]
    [InlineData("ConnectionSuperseded", RelayClientErrorCode.ConnectionSuperseded)]
    public async Task AnyOperation_HubErrorCode_RareCodesMapped(
        string hubCode,
        RelayClientErrorCode expected)
    {
        _handler.StatusCode = HttpStatusCode.Conflict;
        _handler.ResponseContent = $$"""
            {
              "success": false,
              "error": { "code": "{{hubCode}}", "message": "Hub says {{hubCode}}." }
            }
            """;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(expected);
        AssertNoSecretsLeaked(result.Error.Message);
    }

[Fact]
    public async Task AnyOperation_HubErrorCode_NumericCode_MapsCorrectly()
    {
        _handler.StatusCode = HttpStatusCode.Conflict;
        _handler.ResponseContent = """
            {
              "success": false,
              "error": { "code": 7, "message": "Hub says 7." }
            }
            """;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.RoomFull);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task AnyOperation_HubErrorCode_NonStringNonNumber_MapsToUnknown()
    {
        _handler.StatusCode = HttpStatusCode.Conflict;
        _handler.ResponseContent = """
            {
              "success": false,
              "error": { "code": true, "message": "Hub says true." }
            }
            """;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task AnyOperation_UnknownHubErrorCode_MapsToUnknown()
    {
        _handler.StatusCode = HttpStatusCode.Conflict;
        _handler.ResponseContent = """{ "success": false, "error": { "code": "TotallyUnknown", "message": "?" } }""";

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Theory]
    [InlineData("hubatcapacity", RelayClientErrorCode.HubAtCapacity)]
    [InlineData("roomnotfound", RelayClientErrorCode.RoomNotFound)]
    [InlineData("HOSTNOTREADY", RelayClientErrorCode.HostNotReady)]
    public async Task AnyOperation_HubErrorCode_CasingVariant_MapsCorrectly(
        string hubCode,
        RelayClientErrorCode expected)
    {
        _handler.StatusCode = HttpStatusCode.Conflict;
        _handler.ResponseContent = $$"""
            {
              "success": false,
              "error": { "code": "{{hubCode}}", "message": "Hub says {{hubCode}}." }
            }
            """;

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(expected);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_EmptyBody_MapsToDeserializationError()
    {
        _handler.StatusCode = HttpStatusCode.Created;
        _handler.ResponseContent = string.Empty;

        var result = await _sut.Create(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.DeserializationError);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_HubErrorNull_MapsToUnknown()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseContent = """{ "success": false, "roomCode": null, "hostId": null, "sessionToken": null, "expiresAt": null, "error": null }""";

        var result = await _sut.Create(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task JoinAsync_HubErrorNull_MapsToUnknown()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseContent = """{ "success": false, "role": null, "playerId": null, "hostId": null, "sessionToken": null, "error": null }""";

        var result = await _sut.Join("ABCDEF", sessionToken: null);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_HubErrorNull_MapsToUnknown()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseContent = """{ "success": false, "error": null }""";

        var result = await _sut.Ready("ABCDEF", SessionToken);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task RemoveMemberAsync_HubErrorNull_MapsToUnknown()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseContent = """{ "success": false, "error": null }""";

        var result = await _sut.RemoveMember("ABCDEF", SessionToken, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        AssertNoSecretsLeaked(result.Error.Message);
    }

    [Fact]
    public async Task ReadyAsync_CloseAsync_OperationCanceled_Rethrows()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.Ready("ABCDEF", SessionToken, cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.Close("ABCDEF", SessionToken, cts.Token));
    }

    [Fact]
    public async Task CreateJoinRemove_OperationCanceled_Rethrows()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var deviceSessionId = Guid.NewGuid();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.Create(deviceSessionId, cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.Join("ABCDEF", sessionToken: null, cts.Token));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.RemoveMember("ABCDEF", SessionToken, deviceSessionId, cts.Token));
    }

    [Fact]
    public void DeserializeCloseResponse_FromJson_ParsesSuccessfully()
    {
        const string json = """{ "success": true, "error": null }""";
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<CloseResponse>(json, opts);

        response.ShouldNotBeNull();
        response.Success.ShouldBeTrue();
        response.Error.ShouldBeNull();
    }

    [Fact]
    public void DeserializeCloseResponse_WithError_ParsesError()
    {
        const string json = """{ "success": false, "error": { "code": "RoomNotFound", "message": "not found" } }""";
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new JsonStringEnumConverter());
        var response = JsonSerializer.Deserialize<CloseResponse>(json, opts);

        response.ShouldNotBeNull();
        response.Success.ShouldBeFalse();
        response.Error.ShouldNotBeNull();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public void CreateRoomResponse_Deserialization_ParsesExpiresAt()
    {
        const string json = """{ "success": true, "roomCode": "ABCDEF", "hostId": "11111111-1111-1111-1111-111111111111", "sessionToken": "tok", "expiresAt": "2026-07-30T22:00:00Z", "error": null }""";
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        opts.Converters.Add(new JsonStringEnumConverter());
        var response = JsonSerializer.Deserialize<CreateRoomResponse>(json, opts);

        response.ShouldNotBeNull();
        response.ExpiresAt.ShouldNotBeNull();
        response.ExpiresAt.Value.UtcDateTime.ShouldBe(new DateTime(2026, 7, 30, 22, 0, 0, DateTimeKind.Utc));
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

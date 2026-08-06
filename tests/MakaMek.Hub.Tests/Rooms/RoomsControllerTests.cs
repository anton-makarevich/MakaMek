using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Hub.Controllers;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Tests.TestLoggers;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class RoomsControllerTests
{
    private readonly IRoomManager _roomManager = Substitute.For<IRoomManager>();
    private readonly CapturingLogger<RoomsController> _logger = new();
    private readonly RoomsController _sut;

    private static readonly Guid HostGameId = Guid.NewGuid();
    private static readonly Guid DeviceSessionId = Guid.NewGuid();
    private const string SessionToken = "test-session-token";
    private const string RoomCode = "ABC123";

    public RoomsControllerTests()
    {
        _sut = new RoomsController(_roomManager, _logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region JoinRoom

    [Fact]
    public void JoinRoom_ValidRequest_ReturnsOk()
    {
        var room = CreateRoom();
        var session = new RoomSession(SessionToken, RoomCode, DeviceSessionId, RoomRole.Client, DateTimeOffset.UtcNow);
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.Joined(room, session));

        var result = _sut.JoinRoom(RoomCode);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeTrue();
        response.SessionToken.ShouldBe(SessionToken);
        response.DeviceSessionId.ShouldBe(DeviceSessionId);
        response.HostGameId.ShouldBe(HostGameId);
    }

    [Fact]
    public void JoinRoom_RoomNotFound_ReturnsNotFound()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.NotFound());

        var result = _sut.JoinRoom(RoomCode);

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        var response = notFound.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public void JoinRoom_RoomExpired_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.Expired());

        var result = _sut.JoinRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomExpired);
    }

    [Fact]
    public void JoinRoom_HostNotReady_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.NotReady());

        var result = _sut.JoinRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.HostNotReady);
    }

    [Fact]
    public void JoinRoom_RoomFull_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.Full());

        var result = _sut.JoinRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomFull);
    }

    [Fact]
    public void JoinRoom_Forbidden_ReturnsForbidden()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.Forbidden());

        var result = _sut.JoinRoom(RoomCode);

        var forbidden = result.Result.ShouldBeOfType<ObjectResult>();
        forbidden.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        var response = forbidden.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.NotHost);
    }

    [Fact]
    public void JoinRoom_WithSessionToken_ForwardsTokenToRoomManager()
    {
        var room = CreateRoom();
        var session = new RoomSession(SessionToken, RoomCode, DeviceSessionId, RoomRole.Client, DateTimeOffset.UtcNow);
        _roomManager.JoinRoom(RoomCode, SessionToken)
            .Returns(RoomJoinResult.Joined(room, session));
        SetSessionTokenHeader(SessionToken);

        var result = _sut.JoinRoom(RoomCode);

        _roomManager.Received(1).JoinRoom(RoomCode, SessionToken);
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeTrue();
    }

    #endregion

    #region CloseRoom

    [Fact]
    public void CloseRoom_ValidRequest_ReturnsOk()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(RoomCloseResult.Closed());

        var result = _sut.CloseRoom(RoomCode);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<CloseResponse>();
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public void CloseRoom_MissingSessionToken_ReturnsValidationProblem()
    {
        var result = _sut.CloseRoom(RoomCode);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void CloseRoom_RoomNotFound_ReturnsNotFound()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(RoomCloseResult.NotFound());

        var result = _sut.CloseRoom(RoomCode);

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        var response = notFound.Value.ShouldBeOfType<CloseResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public void CloseRoom_RoomExpired_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(RoomCloseResult.Expired());

        var result = _sut.CloseRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<CloseResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomExpired);
    }

    [Fact]
    public void CloseRoom_NotHost_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(RoomCloseResult.NotHost());

        var result = _sut.CloseRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<CloseResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.NotHost);
    }

    [Fact]
    public void CloseRoom_InvalidRoomState_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(RoomCloseResult.InvalidState());

        var result = _sut.CloseRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<CloseResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.InvalidRoomState);
    }

    #endregion

    #region MarkRoomReady

    [Fact]
    public void MarkRoomReady_MissingSessionToken_ReturnsValidationProblem()
    {
        var result = _sut.MarkRoomReady(RoomCode);

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void MarkRoomReady_ValidRequest_ReturnsOk()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(RoomReadyResult.Ready());

        var result = _sut.MarkRoomReady(RoomCode);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadyResponse>();
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public void MarkRoomReady_RoomNotFound_ReturnsNotFound()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(RoomReadyResult.NotFound());

        var result = _sut.MarkRoomReady(RoomCode);

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        var response = notFound.Value.ShouldBeOfType<ReadyResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public void MarkRoomReady_RoomExpired_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(RoomReadyResult.Expired());

        var result = _sut.MarkRoomReady(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<ReadyResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomExpired);
    }

    [Fact]
    public void MarkRoomReady_NotHost_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(RoomReadyResult.NotHost());

        var result = _sut.MarkRoomReady(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<ReadyResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.NotHost);
    }

    [Fact]
    public void MarkRoomReady_InvalidRoomState_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(RoomReadyResult.InvalidState());

        var result = _sut.MarkRoomReady(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<ReadyResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.InvalidRoomState);
    }

    #endregion

    #region RemoveMember

    [Fact]
    public void RemoveMember_MissingAuthorization_ReturnsUnauthorized()
    {
        var targetId = Guid.NewGuid();

        var result = _sut.RemoveMember(RoomCode, targetId);

        result.Result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void RemoveMember_ValidRequest_ReturnsOk()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(RoomRemoveMemberResult.Removed());

        var result = _sut.RemoveMember(RoomCode, targetId);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public void RemoveMember_RoomNotFound_ReturnsNotFound()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(RoomRemoveMemberResult.NotFound());

        var result = _sut.RemoveMember(RoomCode, targetId);

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        var response = notFound.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public void RemoveMember_MemberNotFound_ReturnsNotFound()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(RoomRemoveMemberResult.MemberNotFound());

        var result = _sut.RemoveMember(RoomCode, targetId);

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        var response = notFound.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.MemberNotFound);
    }

    [Fact]
    public void RemoveMember_RoomExpired_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(RoomRemoveMemberResult.Expired());

        var result = _sut.RemoveMember(RoomCode, targetId);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomExpired);
    }

    [Fact]
    public void RemoveMember_NotHost_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(RoomRemoveMemberResult.NotHost());

        var result = _sut.RemoveMember(RoomCode, targetId);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.NotHost);
    }

    [Fact]
    public void RemoveMember_CannotRemoveHost_ReturnsConflict()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(RoomRemoveMemberResult.CannotRemoveHost());

        var result = _sut.RemoveMember(RoomCode, targetId);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.CannotRemoveHost);
    }

    #endregion

    #region Unhandled outcomes

    [Fact]
    public void JoinRoom_UnhandledOutcome_ThrowsInvalidOperationException()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(new RoomJoinResult((RoomJoinOutcome)999, null, null));

        var exception = Should.Throw<InvalidOperationException>(
            () => _sut.JoinRoom(RoomCode));

        exception.Message.ShouldContain("Unhandled join outcome", Case.Sensitive);
    }

    [Fact]
    public void MarkRoomReady_UnhandledOutcome_ReturnsConflictWithDefaultError()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(new RoomReadyResult((RoomReadyOutcome)999));

        var result = _sut.MarkRoomReady(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<ReadyResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.InvalidRoomState);
    }

    [Fact]
    public void CloseRoom_UnhandledOutcome_ReturnsConflictWithDefaultError()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(new RoomCloseResult((RoomCloseOutcome)999));

        var result = _sut.CloseRoom(RoomCode);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<CloseResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.InvalidRoomState);
    }

    [Fact]
    public void RemoveMember_UnhandledOutcome_ReturnsConflictWithDefaultError()
    {
        SetSessionTokenHeader(SessionToken);
        var targetId = Guid.NewGuid();
        _roomManager.RemoveMember(RoomCode, SessionToken, targetId)
            .Returns(new RoomRemoveMemberResult((RoomRemoveMemberOutcome)999));

        var result = _sut.RemoveMember(RoomCode, targetId);

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<RemoveMemberResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.MemberNotFound);
    }

    #endregion

    #region Logging

    [Fact]
    public void JoinRoom_ValidRequest_LogsInformation()
    {
        var room = CreateRoom();
        var session = new RoomSession(SessionToken, RoomCode, DeviceSessionId, RoomRole.Client, DateTimeOffset.UtcNow);
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.Joined(room, session));

        _sut.JoinRoom(RoomCode);

        _logger.GetMessages(LogLevel.Information).ShouldContain(
            message => message.Contains("succeeded with role", StringComparison.Ordinal));
    }

    [Fact]
    public void JoinRoom_RoomNotFound_LogsWarning()
    {
        _roomManager.JoinRoom(RoomCode, Arg.Any<string?>())
            .Returns(RoomJoinResult.NotFound());

        _sut.JoinRoom(RoomCode);

        _logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("failed: RoomNotFound", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkRoomReady_ValidRequest_LogsInformation()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.MarkRoomReady(RoomCode, SessionToken)
            .Returns(RoomReadyResult.Ready());

        _sut.MarkRoomReady(RoomCode);

        _logger.GetMessages(LogLevel.Information).ShouldContain(
            message => message.Contains("marked ready", StringComparison.Ordinal));
    }

    [Fact]
    public void CloseRoom_NotHost_LogsWarning()
    {
        SetSessionTokenHeader(SessionToken);
        _roomManager.CloseRoom(RoomCode, SessionToken)
            .Returns(RoomCloseResult.NotHost());

        _sut.CloseRoom(RoomCode);

        _logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("failed: NotHost", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateRoom_ValidRequest_LogsInformation()
    {
        var room = CreateRoom();
        var session = new RoomSession(SessionToken, RoomCode, DeviceSessionId, RoomRole.Host, DateTimeOffset.UtcNow.AddHours(2));
        _roomManager.CreateRoom(HostGameId)
            .Returns(RoomCreationResult.Created(room, session, 1));

        _sut.CreateRoom(new CreateRoomRequest(HostGameId));

        _logger.GetMessages(LogLevel.Information).ShouldContain(
            message => message.Contains("succeeded: room", StringComparison.Ordinal));
    }

    #endregion

    private void SetSessionTokenHeader(string token)
    {
        _sut.ControllerContext.HttpContext.Request.Headers["Session-Token"] = token;
    }

    private static Room CreateRoom()
    {
        var hostDeviceSessionId = Guid.NewGuid();
        var hostMember = new RoomMember(hostDeviceSessionId, RoomRole.Host, DateTimeOffset.UtcNow);
        var hostSession = new RoomSession("host-token", RoomCode, hostDeviceSessionId, RoomRole.Host, DateTimeOffset.UtcNow.AddHours(2));
        return new Room(RoomCode, HostGameId, hostMember, hostSession, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2));
    }
}

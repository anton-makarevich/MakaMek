using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sanet.MakaMek.Hub.Controllers;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Rooms;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class RoomsControllerTests
{
    private readonly IRoomManager _roomManager = Substitute.For<IRoomManager>();
    private readonly RoomsController _sut;

    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string SessionToken = "test-session-token";
    private const string RoomCode = "ABC123";

    public RoomsControllerTests()
    {
        _sut = new RoomsController(_roomManager)
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
        var session = new RoomSession(SessionToken, RoomCode, PlayerId, RoomRole.Client, DateTimeOffset.UtcNow);
        _roomManager.JoinRoom(RoomCode, "Grace", PlayerId)
            .Returns(RoomJoinResult.Joined(room, session));

        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", PlayerId));

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeTrue();
        response.SessionToken.ShouldBe(SessionToken);
    }

    [Fact]
    public void JoinRoom_RoomNotFound_ReturnsNotFound()
    {
        _roomManager.JoinRoom(RoomCode, "Grace", PlayerId)
            .Returns(RoomJoinResult.NotFound());

        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", PlayerId));

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        var response = notFound.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public void JoinRoom_RoomExpired_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, "Grace", PlayerId)
            .Returns(RoomJoinResult.Expired());

        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", PlayerId));

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomExpired);
    }

    [Fact]
    public void JoinRoom_HostNotReady_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, "Grace", PlayerId)
            .Returns(RoomJoinResult.NotReady());

        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", PlayerId));

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.HostNotReady);
    }

    [Fact]
    public void JoinRoom_HostPlayerIdConflict_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, "Grace", PlayerId)
            .Returns(RoomJoinResult.HostPlayerIdConflict());

        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", PlayerId));

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.HostPlayerIdConflict);
    }

    [Fact]
    public void JoinRoom_RoomFull_ReturnsConflict()
    {
        _roomManager.JoinRoom(RoomCode, "Grace", PlayerId)
            .Returns(RoomJoinResult.Full());

        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", PlayerId));

        var conflict = result.Result.ShouldBeOfType<ConflictObjectResult>();
        var response = conflict.Value.ShouldBeOfType<JoinResponse>();
        response.Success.ShouldBeFalse();
        response.Error!.Code.ShouldBe(HubErrorCode.RoomFull);
    }

    [Fact]
    public void JoinRoom_EmptyPlayerName_ReturnsValidationProblem()
    {
        var result = _sut.JoinRoom(RoomCode, new JoinRequest("", PlayerId));

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void JoinRoom_EmptyPlayerId_ReturnsValidationProblem()
    {
        var result = _sut.JoinRoom(RoomCode, new JoinRequest("Grace", Guid.Empty));

        result.Result.ShouldBeOfType<BadRequestObjectResult>();
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

    private void SetSessionTokenHeader(string token)
    {
        _sut.ControllerContext.HttpContext.Request.Headers["Session-Token"] = token;
    }

    private static Room CreateRoom()
    {
        var hostId = Guid.NewGuid();
        var hostMember = new RoomMember(hostId, "Host", RoomRole.Host, DateTimeOffset.UtcNow);
        var hostSession = new RoomSession("host-token", RoomCode, hostId, RoomRole.Host, DateTimeOffset.UtcNow.AddHours(2));
        return new Room(RoomCode, hostMember, hostSession, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2));
    }
}

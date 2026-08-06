using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Rooms;

namespace Sanet.MakaMek.Hub.Controllers;

/// <summary>
/// Owns the REST room lifecycle. The relay transport is deliberately not involved here.
/// Members are Hub-minted device sessions; no player identity crosses this boundary.
/// </summary>
[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(
    IRoomManager roomManager,
    ILogger<RoomsController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateRoomResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CreateRoomResponse>(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<CreateRoomResponse> CreateRoom([FromBody] CreateRoomRequest request)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (request.GameId == Guid.Empty)
        {
            validationErrors[nameof(request.GameId)] = ["GameId must be a non-empty GUID."];
        }

        if (validationErrors.Count > 0)
        {
            logger.LogWarning(
                "Create-room request rejected: validation failed ({FieldCount} field(s))",
                validationErrors.Count);
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var creation = roomManager.CreateRoom(request.GameId);

        if (creation.Outcome == RoomCreationOutcome.HubAtCapacity)
        {
            logger.LogWarning(
                "Create-room request for game {GameId} rejected: relay at capacity",
                request.GameId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new CreateRoomResponse(
                    Success: false,
                    RoomCode: null,
                    DeviceSessionId: null,
                    HostGameId: null,
                    SessionToken: null,
                    ExpiresAt: null,
                    Error: new HubError(
                        HubErrorCode.HubAtCapacity,
                        "The relay has reached its concurrent room capacity.",
                        creation.ActiveRoomCount)));
        }

        var room = creation.Room!;
        var session = creation.Session!;

        logger.LogInformation(
            "Create-room request for game {GameId} succeeded: room {RoomCode}",
            request.GameId,
            room.RoomCode);

        return Created(
            $"/api/rooms/{room.RoomCode}",
            new CreateRoomResponse(
                Success: true,
                RoomCode: room.RoomCode,
                DeviceSessionId: session.DeviceSessionId,
                HostGameId: room.HostGameId,
                SessionToken: session.Token,
                ExpiresAt: room.ExpiresAt,
                Error: null));
    }

    [HttpPost("{roomCode}/join")]
    [EnableRateLimiting("JoinRateLimit")]
    [ProducesResponseType<JoinResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<JoinResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<JoinResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<JoinResponse> JoinRoom(string roomCode, [FromBody] JoinRequest request)
    {
        var result = roomManager.JoinRoom(roomCode, request.SessionToken);

        return result.Outcome switch
        {
            RoomJoinOutcome.Joined => Ok(LogJoinSuccess(result, roomCode)),
            RoomJoinOutcome.RoomNotFound => NotFound(LogJoinFailure(result.Outcome, roomCode)),
            RoomJoinOutcome.RoomExpired => Conflict(LogJoinFailure(result.Outcome, roomCode)),
            RoomJoinOutcome.HostNotReady => Conflict(LogJoinFailure(result.Outcome, roomCode)),
            RoomJoinOutcome.RoomFull => Conflict(LogJoinFailure(result.Outcome, roomCode)),
            RoomJoinOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, LogJoinFailure(result.Outcome, roomCode)),
            _ => throw new InvalidOperationException($"Unhandled join outcome: {result.Outcome}")
        };
    }

    private JoinResponse LogJoinSuccess(RoomJoinResult result, string roomCode)
    {
        logger.LogInformation(
            "Join request for room {RoomCode} by device session {DeviceSessionId} succeeded with role {Role}",
            roomCode,
            result.Session!.DeviceSessionId,
            result.Session.Role);
        return new JoinResponse(
            Success: true,
            Role: result.Session!.Role.ToString(),
            DeviceSessionId: result.Session.DeviceSessionId,
            HostGameId: result.Room!.HostGameId,
            SessionToken: result.Session.Token,
            Error: null);
    }

    private JoinResponse LogJoinFailure(RoomJoinOutcome outcome, string roomCode)
    {
        logger.LogWarning(
            "Join request for room {RoomCode} failed: {Outcome}",
            roomCode,
            outcome);
        var (errorCode, message) = outcome switch
        {
            RoomJoinOutcome.RoomNotFound => (HubErrorCode.RoomNotFound, "The specified room was not found."),
            RoomJoinOutcome.RoomExpired => (HubErrorCode.RoomExpired, "The specified room has expired."),
            RoomJoinOutcome.HostNotReady => (HubErrorCode.HostNotReady, "The room host is not ready to accept joiners."),
            RoomJoinOutcome.RoomFull => (HubErrorCode.RoomFull, "The room is closed and is not accepting new devices."),
            RoomJoinOutcome.Forbidden => (HubErrorCode.NotHost, "The session token does not authorize this operation."),
            _ => (HubErrorCode.RoomNotFound, "The specified room was not found.")
        };
        return new JoinResponse(
            Success: false,
            Role: null,
            DeviceSessionId: null,
            HostGameId: null,
            SessionToken: null,
            Error: new HubError(errorCode, message));
    }

    [HttpPost("{roomCode}/ready")]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<ReadyResponse> MarkRoomReady(string roomCode)
    {
        if (!TryGetSessionToken(out var sessionToken))
        {
            logger.LogWarning(
                "Mark-ready request for room {RoomCode} rejected: Session-Token header is required",
                roomCode);
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["Session-Token"] = ["Session-Token header is required."]
                }));
        }

        var result = roomManager.MarkRoomReady(roomCode, sessionToken);

        return result.Outcome switch
        {
            RoomReadyOutcome.Ready => Ok(LogReadySuccess(roomCode)),
            RoomReadyOutcome.RoomNotFound => NotFound(LogReadyFailure(result.Outcome, roomCode)),
            _ => Conflict(LogReadyFailure(result.Outcome, roomCode))
        };
    }

    private ReadyResponse LogReadySuccess(string roomCode)
    {
        logger.LogInformation("Room {RoomCode} marked ready", roomCode);
        return new ReadyResponse(Success: true, Error: null);
    }

    private ReadyResponse LogReadyFailure(RoomReadyOutcome outcome, string roomCode)
    {
        logger.LogWarning("Mark-ready request for room {RoomCode} failed: {Outcome}", roomCode, outcome);
        var (errorCode, message) = outcome switch
        {
            RoomReadyOutcome.RoomNotFound => (HubErrorCode.RoomNotFound, "The specified room was not found."),
            RoomReadyOutcome.RoomExpired => (HubErrorCode.RoomExpired, "The specified room has expired."),
            RoomReadyOutcome.NotHost => (HubErrorCode.NotHost, "Only the host can mark a room as ready."),
            RoomReadyOutcome.InvalidRoomState => (HubErrorCode.InvalidRoomState, "The room is not in a state that can be marked ready."),
            _ => (HubErrorCode.InvalidRoomState, "The room is not in a state that can be marked ready.")
        };
        return new ReadyResponse(Success: false, Error: new HubError(errorCode, message));
    }

    [HttpPost("{roomCode}/close")]
    [ProducesResponseType<CloseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CloseResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<CloseResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<CloseResponse> CloseRoom(string roomCode)
    {
        if (!TryGetSessionToken(out var sessionToken))
        {
            logger.LogWarning(
                "Close request for room {RoomCode} rejected: Session-Token header is required",
                roomCode);
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["Session-Token"] = ["Session-Token header is required."]
                }));
        }

        var result = roomManager.CloseRoom(roomCode, sessionToken);

        return result.Outcome switch
        {
            RoomCloseOutcome.Closed => Ok(LogCloseSuccess(roomCode)),
            RoomCloseOutcome.RoomNotFound => NotFound(LogCloseFailure(result.Outcome, roomCode)),
            _ => Conflict(LogCloseFailure(result.Outcome, roomCode))
        };
    }

    private CloseResponse LogCloseSuccess(string roomCode)
    {
        logger.LogInformation("Room {RoomCode} closed", roomCode);
        return new CloseResponse(Success: true, Error: null);
    }

    private CloseResponse LogCloseFailure(RoomCloseOutcome outcome, string roomCode)
    {
        logger.LogWarning("Close request for room {RoomCode} failed: {Outcome}", roomCode, outcome);
        var (errorCode, message) = outcome switch
        {
            RoomCloseOutcome.RoomNotFound => (HubErrorCode.RoomNotFound, "The specified room was not found."),
            RoomCloseOutcome.RoomExpired => (HubErrorCode.RoomExpired, "The specified room has expired."),
            RoomCloseOutcome.NotHost => (HubErrorCode.NotHost, "Only the host can close a room."),
            RoomCloseOutcome.InvalidRoomState => (HubErrorCode.InvalidRoomState, "The room is not in a state that can be closed."),
            _ => (HubErrorCode.InvalidRoomState, "The room is not in a state that can be closed.")
        };
        return new CloseResponse(Success: false, Error: new HubError(errorCode, message));
    }

    [HttpDelete("{roomCode}/members/{deviceSessionId:guid}")]
    [ProducesResponseType<RemoveMemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<RemoveMemberResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<RemoveMemberResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<RemoveMemberResponse> RemoveMember(string roomCode, Guid deviceSessionId)
    {
        if (!TryGetSessionToken(out var sessionToken))
        {
            logger.LogWarning(
                "Remove-member request for room {RoomCode} rejected: Session-Token header is required",
                roomCode);
            return Unauthorized();
        }

        var result = roomManager.RemoveMember(roomCode, sessionToken, deviceSessionId);

        return result.Outcome switch
        {
            RoomRemoveMemberOutcome.Removed => Ok(LogRemoveSuccess(roomCode, deviceSessionId)),
            RoomRemoveMemberOutcome.RoomNotFound => NotFound(LogRemoveFailure(result.Outcome, roomCode, deviceSessionId)),
            RoomRemoveMemberOutcome.MemberNotFound => NotFound(LogRemoveFailure(result.Outcome, roomCode, deviceSessionId)),
            _ => Conflict(LogRemoveFailure(result.Outcome, roomCode, deviceSessionId))
        };
    }

    private RemoveMemberResponse LogRemoveSuccess(string roomCode, Guid deviceSessionId)
    {
        logger.LogInformation("Device session {DeviceSessionId} removed from room {RoomCode}", deviceSessionId, roomCode);
        return new RemoveMemberResponse(Success: true, Error: null);
    }

    private RemoveMemberResponse LogRemoveFailure(RoomRemoveMemberOutcome outcome, string roomCode, Guid deviceSessionId)
    {
        logger.LogWarning(
            "Remove-member request for room {RoomCode} (device session {DeviceSessionId}) failed: {Outcome}",
            roomCode,
            deviceSessionId,
            outcome);
        var (errorCode, message) = outcome switch
        {
            RoomRemoveMemberOutcome.RoomNotFound => (HubErrorCode.RoomNotFound, "The specified room was not found."),
            RoomRemoveMemberOutcome.MemberNotFound => (HubErrorCode.MemberNotFound, "The specified member was not found in the room."),
            RoomRemoveMemberOutcome.RoomExpired => (HubErrorCode.RoomExpired, "The specified room has expired."),
            RoomRemoveMemberOutcome.NotHost => (HubErrorCode.NotHost, "Only the host can remove a room member."),
            RoomRemoveMemberOutcome.CannotRemoveHost => (HubErrorCode.CannotRemoveHost, "The host cannot be removed from the room."),
            _ => (HubErrorCode.MemberNotFound, "The specified member was not found in the room.")
        };
        return new RemoveMemberResponse(Success: false, Error: new HubError(errorCode, message));
    }

    private bool TryGetSessionToken(out string sessionToken)
    {
        sessionToken = string.Empty;
        if (!Request.Headers.TryGetValue("Session-Token", out var values))
        {
            return false;
        }

        sessionToken = values.ToString().Trim();
        return !string.IsNullOrWhiteSpace(sessionToken);
    }
}

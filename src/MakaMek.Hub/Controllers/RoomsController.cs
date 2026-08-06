using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Rooms;

namespace Sanet.MakaMek.Hub.Controllers;

/// <summary>
/// Owns the REST room lifecycle. The relay transport is deliberately not involved here.
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

        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            validationErrors[nameof(request.PlayerName)] = ["PlayerName is required."];
        }

        if (request.PlayerId == Guid.Empty)
        {
            validationErrors[nameof(request.PlayerId)] = ["PlayerId must be a non-empty GUID."];
        }

        if (validationErrors.Count > 0)
        {
            logger.LogWarning(
                "Create-room request rejected: validation failed ({FieldCount} field(s))",
                validationErrors.Count);
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var creation = roomManager.CreateRoom(request.PlayerName.Trim(), request.PlayerId);

        if (creation.Outcome == RoomCreationOutcome.HubAtCapacity)
        {
            logger.LogWarning(
                "Create-room request by player {PlayerId} rejected: relay at capacity",
                request.PlayerId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new CreateRoomResponse(
                    Success: false,
                    RoomCode: null,
                    HostId: null,
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
            "Create-room request by player {PlayerId} succeeded: room {RoomCode}",
            request.PlayerId,
            room.RoomCode);

        return Created(
            $"/api/rooms/{room.RoomCode}",
            new CreateRoomResponse(
                Success: true,
                RoomCode: room.RoomCode,
                HostId: room.HostPlayerId,
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
        var validationErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            validationErrors[nameof(request.PlayerName)] = ["PlayerName is required."];
        }

        if (request.PlayerId == Guid.Empty)
        {
            validationErrors[nameof(request.PlayerId)] = ["PlayerId must be a non-empty GUID."];
        }

        if (validationErrors.Count > 0)
        {
            logger.LogWarning(
                "Join request for room {RoomCode} rejected: validation failed ({FieldCount} field(s))",
                roomCode,
                validationErrors.Count);
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var result = roomManager.JoinRoom(roomCode, request.PlayerName.Trim(), request.PlayerId);

        return result.Outcome switch
        {
            RoomJoinOutcome.Joined => Ok(LogJoinSuccess(result, roomCode, request)),
            RoomJoinOutcome.RoomNotFound => NotFound(LogJoinFailure(result.Outcome, roomCode, request)),
            RoomJoinOutcome.RoomExpired => Conflict(LogJoinFailure(result.Outcome, roomCode, request)),
            RoomJoinOutcome.HostNotReady => Conflict(LogJoinFailure(result.Outcome, roomCode, request)),
            RoomJoinOutcome.HostPlayerIdConflict => Conflict(LogJoinFailure(result.Outcome, roomCode, request)),
            RoomJoinOutcome.RoomFull => Conflict(LogJoinFailure(result.Outcome, roomCode, request)),
            _ => throw new InvalidOperationException($"Unhandled join outcome: {result.Outcome}")
        };
    }

    private JoinResponse LogJoinSuccess(RoomJoinResult result, string roomCode, JoinRequest request)
    {
        logger.LogInformation(
            "Join request for room {RoomCode} by player {PlayerId} ({PlayerName}) succeeded with role {Role}",
            roomCode,
            request.PlayerId,
            request.PlayerName,
            result.Session!.Role);
        return new JoinResponse(
            Success: true,
            Role: result.Session!.Role.ToString(),
            PlayerId: result.Session.PlayerId,
            HostId: result.Room!.HostPlayerId,
            SessionToken: result.Session.Token,
            Error: null);
    }

    private JoinResponse LogJoinFailure(RoomJoinOutcome outcome, string roomCode, JoinRequest request)
    {
        logger.LogWarning(
            "Join request for room {RoomCode} by player {PlayerId} ({PlayerName}) failed: {Outcome}",
            roomCode,
            request.PlayerId,
            request.PlayerName,
            outcome);
        var (errorCode, message) = outcome switch
        {
            RoomJoinOutcome.RoomNotFound => (HubErrorCode.RoomNotFound, "The specified room was not found."),
            RoomJoinOutcome.RoomExpired => (HubErrorCode.RoomExpired, "The specified room has expired."),
            RoomJoinOutcome.HostNotReady => (HubErrorCode.HostNotReady, "The room host is not ready to accept joiners."),
            RoomJoinOutcome.HostPlayerIdConflict => (HubErrorCode.HostPlayerIdConflict, "The supplied PlayerId matches the host."),
            RoomJoinOutcome.RoomFull => (HubErrorCode.RoomFull, "The room is closed and is not accepting new players."),
            _ => (HubErrorCode.RoomNotFound, "The specified room was not found.")
        };
        return new JoinResponse(
            Success: false,
            Role: null,
            PlayerId: null,
            HostId: null,
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

    [HttpDelete("{roomCode}/members/{playerId:guid}")]
    [ProducesResponseType<RemoveMemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<RemoveMemberResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<RemoveMemberResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<RemoveMemberResponse> RemoveMember(string roomCode, Guid playerId)
    {
        if (!TryGetSessionToken(out var sessionToken))
        {
            logger.LogWarning(
                "Remove-member request for room {RoomCode} rejected: Session-Token header is required",
                roomCode);
            return Unauthorized();
        }

        var result = roomManager.RemoveMember(roomCode, sessionToken, playerId);

        return result.Outcome switch
        {
            RoomRemoveMemberOutcome.Removed => Ok(LogRemoveSuccess(roomCode, playerId)),
            RoomRemoveMemberOutcome.RoomNotFound => NotFound(LogRemoveFailure(result.Outcome, roomCode, playerId)),
            RoomRemoveMemberOutcome.MemberNotFound => NotFound(LogRemoveFailure(result.Outcome, roomCode, playerId)),
            _ => Conflict(LogRemoveFailure(result.Outcome, roomCode, playerId))
        };
    }

    private RemoveMemberResponse LogRemoveSuccess(string roomCode, Guid playerId)
    {
        logger.LogInformation("Member {PlayerId} removed from room {RoomCode}", playerId, roomCode);
        return new RemoveMemberResponse(Success: true, Error: null);
    }

    private RemoveMemberResponse LogRemoveFailure(RoomRemoveMemberOutcome outcome, string roomCode, Guid playerId)
    {
        logger.LogWarning(
            "Remove-member request for room {RoomCode} (player {PlayerId}) failed: {Outcome}",
            roomCode,
            playerId,
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

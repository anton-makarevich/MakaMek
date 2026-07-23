using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Rooms;

namespace Sanet.MakaMek.Hub.Controllers;

/// <summary>
/// Owns the REST room lifecycle. The relay transport is deliberately not involved here.
/// </summary>
[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(IRoomManager roomManager) : ControllerBase
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
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var creation = roomManager.CreateRoom(request.PlayerName.Trim(), request.PlayerId);

        if (creation.Outcome == RoomCreationOutcome.HubAtCapacity)
        {
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
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var result = roomManager.JoinRoom(roomCode, request.PlayerName.Trim(), request.PlayerId);

        return result.Outcome switch
        {
            RoomJoinOutcome.Joined => Ok(new JoinResponse(
                Success: true,
                Role: result.Session!.Role.ToString(),
                PlayerId: result.Session.PlayerId,
                HostId: result.Room!.HostPlayerId,
                SessionToken: result.Session.Token,
                Error: null)),
            RoomJoinOutcome.RoomNotFound => NotFound(new JoinResponse(
                Success: false,
                Role: null,
                PlayerId: null,
                HostId: null,
                SessionToken: null,
                Error: new HubError(HubErrorCode.RoomNotFound, "The specified room was not found."))),
            RoomJoinOutcome.RoomExpired => Conflict(new JoinResponse(
                Success: false,
                Role: null,
                PlayerId: null,
                HostId: null,
                SessionToken: null,
                Error: new HubError(HubErrorCode.RoomExpired, "The specified room has expired."))),
            RoomJoinOutcome.HostNotReady => Conflict(new JoinResponse(
                Success: false,
                Role: null,
                PlayerId: null,
                HostId: null,
                SessionToken: null,
                Error: new HubError(HubErrorCode.HostNotReady, "The room host is not ready to accept joiners."))),
            RoomJoinOutcome.HostPlayerIdConflict => Conflict(new JoinResponse(
                Success: false,
                Role: null,
                PlayerId: null,
                HostId: null,
                SessionToken: null,
                Error: new HubError(HubErrorCode.HostPlayerIdConflict, "The supplied PlayerId matches the host."))),
            RoomJoinOutcome.RoomFull => Conflict(new JoinResponse(
                Success: false,
                Role: null,
                PlayerId: null,
                HostId: null,
                SessionToken: null,
                Error: new HubError(HubErrorCode.RoomFull, "The room is closed and is not accepting new players."))),
            _ => throw new InvalidOperationException($"Unhandled join outcome: {result.Outcome}")
        };
    }

    [HttpPost("{roomCode}/ready")]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<ReadyResponse> MarkRoomReady(string roomCode)
    {
        if (!TryGetSessionToken(out var sessionToken))
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["Session-Token"] = ["Session-Token header is required."]
                }));
        }

        var result = roomManager.MarkRoomReady(roomCode, sessionToken);

        return result.Outcome switch
        {
            RoomReadyOutcome.Ready => Ok(new ReadyResponse(Success: true, Error: null)),
            RoomReadyOutcome.RoomNotFound => NotFound(new ReadyResponse(
                Success: false,
                Error: new HubError(HubErrorCode.RoomNotFound, "The specified room was not found."))),
            RoomReadyOutcome.RoomExpired => Conflict(new ReadyResponse(
                Success: false,
                Error: new HubError(HubErrorCode.RoomExpired, "The specified room has expired."))),
            RoomReadyOutcome.NotHost => Conflict(new ReadyResponse(
                Success: false,
                Error: new HubError(HubErrorCode.NotHost, "Only the host can mark a room as ready."))),
            RoomReadyOutcome.InvalidRoomState => Conflict(new ReadyResponse(
                Success: false,
                Error: new HubError(HubErrorCode.InvalidRoomState, "The room is not in a state that can be marked ready."))),
            _ => throw new InvalidOperationException($"Unhandled ready outcome: {result.Outcome}")
        };
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
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["Session-Token"] = ["Session-Token header is required."]
                }));
        }

        var result = roomManager.CloseRoom(roomCode, sessionToken);

        return result.Outcome switch
        {
            RoomCloseOutcome.Closed => Ok(new CloseResponse(Success: true, Error: null)),
            RoomCloseOutcome.RoomNotFound => NotFound(new CloseResponse(
                Success: false,
                Error: new HubError(HubErrorCode.RoomNotFound, "The specified room was not found."))),
            RoomCloseOutcome.RoomExpired => Conflict(new CloseResponse(
                Success: false,
                Error: new HubError(HubErrorCode.RoomExpired, "The specified room has expired."))),
            RoomCloseOutcome.NotHost => Conflict(new CloseResponse(
                Success: false,
                Error: new HubError(HubErrorCode.NotHost, "Only the host can close a room."))),
            RoomCloseOutcome.InvalidRoomState => Conflict(new CloseResponse(
                Success: false,
                Error: new HubError(HubErrorCode.InvalidRoomState, "The room is not in a state that can be closed."))),
            _ => throw new InvalidOperationException($"Unhandled close outcome: {result.Outcome}")
        };
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
            return Unauthorized();
        }

        var result = roomManager.RemoveMember(roomCode, sessionToken, playerId);

        return result.Outcome switch
        {
            RoomRemoveMemberOutcome.Removed => Ok(new RemoveMemberResponse(Success: true, Error: null)),
            RoomRemoveMemberOutcome.RoomNotFound => NotFound(new RemoveMemberResponse(
                Success: false,
                Error: new HubError(HubErrorCode.RoomNotFound, "The specified room was not found."))),
            RoomRemoveMemberOutcome.MemberNotFound => NotFound(new RemoveMemberResponse(
                Success: false,
                Error: new HubError(HubErrorCode.MemberNotFound, "The specified member was not found in the room."))),
            RoomRemoveMemberOutcome.RoomExpired => Conflict(new RemoveMemberResponse(
                Success: false,
                Error: new HubError(HubErrorCode.RoomExpired, "The specified room has expired."))),
            RoomRemoveMemberOutcome.NotHost => Conflict(new RemoveMemberResponse(
                Success: false,
                Error: new HubError(HubErrorCode.NotHost, "Only the host can remove a room member."))),
            RoomRemoveMemberOutcome.CannotRemoveHost => Conflict(new RemoveMemberResponse(
                Success: false,
                Error: new HubError(HubErrorCode.CannotRemoveHost, "The host cannot be removed from the room."))),
            _ => throw new InvalidOperationException($"Unhandled remove-member outcome: {result.Outcome}")
        };
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

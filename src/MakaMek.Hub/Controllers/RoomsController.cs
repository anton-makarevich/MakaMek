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
            _ => throw new InvalidOperationException($"Unhandled join outcome: {result.Outcome}")
        };
    }

    [HttpPost("{roomCode}/ready")]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ReadyResponse>(StatusCodes.Status409Conflict)]
    public ActionResult<ReadyResponse> MarkRoomReady(string roomCode, [FromBody] ReadyRequest request)
    {
        if (request.PlayerId == Guid.Empty)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(request.PlayerId)] = ["PlayerId must be a non-empty GUID."]
                }));
        }

        var result = roomManager.MarkRoomReady(roomCode, request.PlayerId);

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
            _ => throw new InvalidOperationException($"Unhandled ready outcome: {result.Outcome}")
        };
    }
}

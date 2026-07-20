using Microsoft.AspNetCore.Mvc;
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
}

using BotAgent.Models;
using BotAgent.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace BotAgent.Controllers;

/// <summary>
/// API controller for tactical decision requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DecisionController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<DecisionController> _logger;

    public DecisionController(
        AgentOrchestrator orchestrator,
        ILogger<DecisionController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Request a tactical decision from the LLM Agent.
    /// </summary>
    /// <param name="request">Decision request from Integration Bot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decision response with command or error.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DecisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DecisionResponse>> PostDecision(
        [FromBody] DecisionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received decision request for player {PlayerId} in phase {Phase}",
            request.PlayerId,
            request.Phase);

        try
        {
            // Validate request
            if (request.PlayerId == Guid.Empty)
            {
                _logger.LogWarning("Invalid request: PlayerId is empty");
                return BadRequest(new DecisionResponse(
                    Success: false,
                    CommandType: null,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "INVALID_REQUEST",
                    ErrorMessage: "PlayerId cannot be empty",
                    FallbackRequired: true
                ));
            }

            if (string.IsNullOrWhiteSpace(request.Phase))
            {
                _logger.LogWarning("Invalid request: Phase is empty");
                return BadRequest(new DecisionResponse(
                    Success: false,
                    CommandType: null,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "INVALID_REQUEST",
                    ErrorMessage: "Phase cannot be empty",
                    FallbackRequired: true
                ));
            }

            if (string.IsNullOrWhiteSpace(request.McpServerUrl))
            {
                _logger.LogWarning("Invalid request: McpServerUrl is empty");
                return BadRequest(new DecisionResponse(
                    Success: false,
                    CommandType: null,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "INVALID_REQUEST",
                    ErrorMessage: "McpServerUrl cannot be empty",
                    FallbackRequired: true
                ));
            }

            // Process decision
            var response = await _orchestrator.ProcessDecisionAsync(request, cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing decision request");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new DecisionResponse(
                    Success: false,
                    CommandType: null,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "INTERNAL_ERROR",
                    ErrorMessage: "Internal server error",
                    FallbackRequired: true
                ));
        }
    }
}

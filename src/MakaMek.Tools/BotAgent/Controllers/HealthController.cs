using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Sanet.MakaMek.Tools.BotAgent.Controllers;

/// <summary>
/// Health check controller.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    /// <returns>Health status information.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        _logger.LogDebug("Health check requested");

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "unknown";

        return Ok(new
        {
            status = "healthy",
            service = "BotAgent",
            version = version,
            timestamp = DateTime.UtcNow
        });
    }
}

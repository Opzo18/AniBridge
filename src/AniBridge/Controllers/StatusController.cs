using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AniBridge.Controllers;

/// <summary>
/// Setup checklist for the settings pages: what is configured, what is missing.
/// </summary>
[ApiController]
[Route("AniBridge")]
[Authorize]
public sealed class StatusController : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<PluginStatus> GetStatus() =>
        Ok(StatusEvaluator.Evaluate(Plugin.Instance?.Configuration));
}

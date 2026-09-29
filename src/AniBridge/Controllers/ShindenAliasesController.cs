using AniBridge.Providers.Shinden;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AniBridge.Controllers;

/// <summary>
/// Debug helper for the settings page: shows which alternate titles AniBridge
/// extracts from a Shinden title page (used for AniList retry queries).
/// </summary>
[ApiController]
[Route("AniBridge")]
[Authorize]
public sealed class ShindenAliasesController : ControllerBase
{
    private readonly ShindenListService _lists;

    public ShindenAliasesController(ShindenListService lists)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
    }

    [HttpGet("shinden/aliases")]
    public async Task<ActionResult<ShindenAliasesResult>> Get(
        [FromQuery] string titleUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(titleUrl))
        {
            return BadRequest(new ShindenAliasesResult(titleUrl ?? string.Empty, []));
        }

        var aliases = await _lists.GetTitleAliasesAsync(titleUrl, cancellationToken).ConfigureAwait(false);
        return new ShindenAliasesResult(titleUrl, aliases);
    }
}

public sealed record ShindenAliasesResult(string TitleUrl, IReadOnlyList<string> Aliases);

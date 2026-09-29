using AniBridge.Providers.Shinden;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AniBridge.Controllers;

/// <summary>
/// Validates Shinden credentials + list ID without running a full sync.
/// Uses a throwaway client so the shared session is not disturbed.
/// </summary>
[ApiController]
[Route("AniBridge")]
[Authorize]
public sealed class ShindenTestController : ControllerBase
{
    private readonly ILogger<ShindenClient> _clientLogger;
    private readonly ILogger<ShindenListService> _listsLogger;

    public ShindenTestController(
        ILogger<ShindenClient> clientLogger, ILogger<ShindenListService> listsLogger)
    {
        _clientLogger = clientLogger;
        _listsLogger = listsLogger;
    }

    [HttpPost("shinden/test")]
    public async Task<ActionResult<ShindenTestResult>> Test(
        [FromBody] ShindenTestRequest request, CancellationToken cancellationToken)
    {
        var username = request?.Username?.Trim() ?? string.Empty;
        var password = request?.Password ?? string.Empty;
        var userId = request?.UserId?.Trim() ?? string.Empty;

        if (username.Length == 0 || password.Length == 0)
        {
            return new ShindenTestResult(false, "Enter login and password first.", null);
        }

        if (userId.Length == 0)
        {
            return new ShindenTestResult(false, "Enter the list ID, e.g. 420984-opzo.", null);
        }

        string normalized;
        try
        {
            normalized = ShindenListService.NormalizeUserId(userId);
        }
        catch (Exception)
        {
            return new ShindenTestResult(false, "List ID looks invalid. Use e.g. 420984-opzo or a full shinden.pl URL.", null);
        }

        if (normalized.Length == 0)
        {
            return new ShindenTestResult(false, "List ID looks invalid. Use e.g. 420984-opzo or a full shinden.pl URL.", null);
        }

        using var client = ShindenClient.CreateDefault(_clientLogger);
        bool loggedIn;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            loggedIn = await client.LoginAsync(username, password, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ShindenTestResult(false, "Timed out talking to Shinden.pl — try again.", null);
        }
        catch (HttpRequestException ex)
        {
            return new ShindenTestResult(false, "Could not reach Shinden.pl (" + ex.Message + ").", null);
        }
        catch (Exception ex)
        {
            return new ShindenTestResult(false, "Login failed: " + ex.Message, null);
        }

        if (!loggedIn)
        {
            return new ShindenTestResult(false, "Login failed — check login/password.", null);
        }

        var lists = new ShindenListService(client, _listsLogger);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var entries = await lists.GetAnimeListAsync(normalized, timeout.Token).ConfigureAwait(false);
            return new ShindenTestResult(true, "OK — list is public and readable.", entries.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ShindenTestResult(
                true, "Logged in, but fetching the list timed out. Check that the list is public.", null);
        }
        catch (Exception ex)
        {
            return new ShindenTestResult(
                true, "Logged in, but the list could not be read (" + ex.Message + "). Is it public?", null);
        }
    }
}

public sealed record ShindenTestRequest(string? Username, string? Password, string? UserId);

public sealed record ShindenTestResult(bool Ok, string Message, int? Entries);

namespace AniBridge.Providers.Models;

/// <summary>
/// Shared user-list title status, independent of the service.
/// Each provider maps its own status names to these values (Stage 4: Shinden).
/// </summary>
public enum AnimeStatus
{
    Planned,
    Watching,
    Completed,
    Dropped,
    OnHold,
}

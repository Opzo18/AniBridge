using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AniBridge.Sync;

public interface ISyncReportStore
{
    void Save(SyncReport report);

    /// <summary>
    /// Interim snapshot while the sync is running: updates the last-sync file
    /// without appending to history. Best-effort; default is a no-op for old fakes.
    /// </summary>
    void SaveProgress(SyncReport report)
    {
    }

    SyncReport? Load();

    /// <summary>
    /// Newest first, capped by the store (10). Default keeps old fakes compiling.
    /// </summary>
    IReadOnlyList<SyncReport> LoadHistory() => Load() is { } single ? [single] : [];
}

/// <summary>
/// Stores the last sync report as JSON plus a short history (newest first, max 10).
/// Corrupt files read as "no report".
/// </summary>
public sealed class FileSyncReportStore : ISyncReportStore
{
    public const string FileName = "anibridge-last-sync.json";

    public const string HistoryFileName = "anibridge-sync-history.json";

    public const int MaxHistory = 10;

    private static readonly object Gate = new();

    private readonly string _filePath;
    private readonly string _historyPath;
    private readonly ILogger<FileSyncReportStore> _logger;

    public FileSyncReportStore(string directory, ILogger<FileSyncReportStore> logger)
    {
        _filePath = Path.Combine(directory ?? throw new ArgumentNullException(nameof(directory)), FileName);
        _historyPath = Path.Combine(directory, HistoryFileName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Save(SyncReport report)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(report));
            SaveHistoryLocked(report);
        }
    }

    public void SaveProgress(SyncReport report)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllText(_filePath, JsonSerializer.Serialize(report));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AniBridge: could not save sync progress snapshot.");
            }
        }
    }

    public SyncReport? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SyncReport>(File.ReadAllText(_filePath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AniBridge: could not read last sync report.");
            return null;
        }
    }

    public IReadOnlyList<SyncReport> LoadHistory()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(_historyPath))
                {
                    var single = Load();
                    return single is null ? [] : [single];
                }

                return JsonSerializer.Deserialize<List<SyncReport>>(File.ReadAllText(_historyPath))
                    ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AniBridge: could not read sync history.");
                return [];
            }
        }
    }

    private void SaveHistoryLocked(SyncReport report)
    {
        try
        {
            List<SyncReport> history = [];
            if (File.Exists(_historyPath))
            {
                history = JsonSerializer.Deserialize<List<SyncReport>>(File.ReadAllText(_historyPath))
                    ?? [];
            }

            history.Insert(0, report);
            if (history.Count > MaxHistory)
            {
                history.RemoveRange(MaxHistory, history.Count - MaxHistory);
            }

            File.WriteAllText(_historyPath, JsonSerializer.Serialize(history));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AniBridge: could not save sync history.");
        }
    }
}

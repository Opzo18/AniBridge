using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AniBridge.Sync;

public interface ISyncReportStore
{
    void Save(SyncReport report);

    SyncReport? Load();
}

/// <summary>
/// Stores the last sync report as JSON. Corrupt files read as "no report".
/// </summary>
public sealed class FileSyncReportStore : ISyncReportStore
{
    public const string FileName = "anibridge-last-sync.json";

    private readonly string _filePath;
    private readonly ILogger<FileSyncReportStore> _logger;

    public FileSyncReportStore(string directory, ILogger<FileSyncReportStore> logger)
    {
        _filePath = Path.Combine(directory ?? throw new ArgumentNullException(nameof(directory)), FileName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Save(SyncReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(report));
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
}

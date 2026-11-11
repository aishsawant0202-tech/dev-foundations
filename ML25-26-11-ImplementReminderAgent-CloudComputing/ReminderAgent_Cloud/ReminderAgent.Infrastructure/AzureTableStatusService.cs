using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using ReminderAgent.Common;

namespace ReminderAgent.Infrastructure;

/// <summary>
/// Azure Table Storage-backed implementation of <see cref="ITableStatusService"/> — bulk-upload
/// job status (BulkUploadStatus table) and general tool-call activity logging (ActivityLog table).
/// </summary>
public class AzureTableStatusService : ITableStatusService
{
    #region Fields

    private readonly TableClient _jobStatusTable;
    private readonly TableClient _activityLogTable;

    #endregion

    #region Constructor

    public AzureTableStatusService(IConfiguration configuration, MyConfig? config = null)
    {
        config ??= new MyConfig();

        var conn = configuration.GetConnectionString("TableStorage")
            ?? throw new InvalidOperationException(
                "Connection string 'TableStorage' not found in appsettings.json");

        _jobStatusTable = new TableClient(conn, config.BulkUploadStatusTable);
        _jobStatusTable.CreateIfNotExists();

        _activityLogTable = new TableClient(conn, config.ActivityLogTable);
        _activityLogTable.CreateIfNotExists();
    }

    #endregion

    #region Bulk-Upload Job Tracking

    /// <summary>Writes one row's outcome within a bulk-upload job.</summary>
    public async Task WriteRowStatusAsync(string jobId, int rowNumber, bool success, string assetName, string? error,
        bool skipped = false)
    {
        var entity = new TableEntity(jobId, $"row-{rowNumber:D5}")
        {
            { "AssetName", assetName },
            { "Success", success },
            { "Error", error ?? "" },
            { "Skipped", skipped }
        };
        await _jobStatusTable.UpsertEntityAsync(entity);
    }

    /// <summary>Writes the final summary for a completed bulk-upload job.</summary>
    public async Task WriteJobSummaryAsync(string jobId, int total, int succeeded, int failed, int skipped = 0)
    {
        var entity = new TableEntity(jobId, "summary")
        {
            { "Total", total },
            { "Succeeded", succeeded },
            { "Failed", failed },
            { "Skipped", skipped },
            { "CompletedAt", DateTime.UtcNow }
        };
        await _jobStatusTable.UpsertEntityAsync(entity);
    }

    /// <summary>Reads a bulk-upload job's summary, or null if not finished/found yet.</summary>
    public async Task<Dictionary<string, string>?> GetJobStatusAsync(string jobId)
    {
        try
        {
            var entity = await _jobStatusTable.GetEntityAsync<TableEntity>(jobId, "summary");
            return entity.Value.Select(kv => new { kv.Key, Value = kv.Value?.ToString() ?? "" })
                .ToDictionary(x => x.Key, x => x.Value);
        }
        catch (Azure.RequestFailedException) { return null; }
    }

    #endregion

    #region Activity Logging

    /// <summary>Writes one activity-log entry for a tool call.</summary>
    public async Task LogActivityAsync(string toolName, string detail, bool success)
    {
        var now = DateTime.UtcNow;
        var partitionKey = now.ToString("yyyy-MM-dd");
        var rowKey = $"{(DateTime.MaxValue.Ticks - now.Ticks):D19}-{Guid.NewGuid():N}";

        var entity = new TableEntity(partitionKey, rowKey)
        {
            { "ToolName", toolName },
            { "Detail", detail },
            { "Success", success },
            { "Timestamp", now }
        };
        await _activityLogTable.UpsertEntityAsync(entity);
    }

    /// <summary>Reads the most recent activity entries, newest first.</summary>
    public async Task<List<Dictionary<string, string>>> GetRecentActivityAsync(int count = 20)
    {
        var results = new List<Dictionary<string, string>>();
        // Query today's partition first (most common case); falls back to yesterday
        // if today doesn't have `count` rows yet, so results don't look empty
        // right after midnight.
        foreach (var partitionKey in new[]
        {
            DateTime.UtcNow.ToString("yyyy-MM-dd"),
            DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")
        })
        {
            await foreach (var entity in _activityLogTable.QueryAsync<TableEntity>(
                e => e.PartitionKey == partitionKey))
            {
                results.Add(entity.Select(kv => new { kv.Key, Value = kv.Value?.ToString() ?? "" })
                    .ToDictionary(x => x.Key, x => x.Value));
                if (results.Count >= count) return results;
            }
        }
        return results;
    }

    #endregion
}
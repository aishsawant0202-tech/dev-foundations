using System;
using System.Collections.Generic;
using System.Text;

namespace ReminderAgent.Common;

/// <summary>Azure Table Storage status tracking: bulk-upload job progress and tool activity.</summary>
public interface ITableStatusService
{
    #region Bulk-Upload Job Tracking

    /// <summary>Writes one row's outcome within a bulk-upload job.</summary>
    /// <param name="skipped">True if the row was a duplicate and deliberately not inserted.</param>
    Task WriteRowStatusAsync(string jobId, int rowNumber, bool success, string assetName, string? error,
        bool skipped = false);

    /// <summary>Writes the final summary for a completed bulk-upload job.</summary>
    Task WriteJobSummaryAsync(string jobId, int total, int succeeded, int failed, int skipped = 0);

    /// <summary>Reads a bulk-upload job's summary, or null if not finished/found yet.</summary>
    Task<Dictionary<string, string>?> GetJobStatusAsync(string jobId);

    #endregion

    #region Activity Logging

    /// <summary>Logs one tool-call activity entry.</summary>
    Task LogActivityAsync(string toolName, string detail, bool success);

    /// <summary>Reads the most recent activity entries, newest first.</summary>
    Task<List<Dictionary<string, string>>> GetRecentActivityAsync(int count = 20);

    #endregion
}

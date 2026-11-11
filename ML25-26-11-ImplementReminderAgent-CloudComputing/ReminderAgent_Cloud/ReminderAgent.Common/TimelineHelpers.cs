using System.Globalization;

namespace ReminderAgent.Common;

/// <summary>Event-date parsing and timeline classification.</summary>
/// <remarks>
/// Shared by <c>ReminderTools.CreateAsset</c> and <c>BulkUploadWorker</c> so both
/// front doors handle dates the same way instead of two drifting copies.
/// </remarks>
public static class TimelineHelpers
{
    #region Date Parsing

    /// <summary>Parses dd-MM-yyyy first, then falls back to a general parse.</summary>
    /// <returns>Null for a blank or unparseable input — not an error, just "no date".</returns>
    public static DateTime? ParseEventDate(string? eventDate)
    {
        if (string.IsNullOrWhiteSpace(eventDate)) return null;

        if (DateTime.TryParseExact(eventDate.Trim(), "dd-MM-yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
            return exact;

        if (DateTime.TryParse(eventDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var general))
            return general;

        return null;
    }

    #endregion

    #region Timeline Classification

    /// <summary>Classifies a date as Past/Present/Future relative to today (UTC).</summary>
    /// <returns>"Present" for a null date, matching the Asset model's default.</returns>
    public static string ComputeTimelineState(DateTime? eventDate)
    {
        if (eventDate is null) return "Present";
        var today = DateTime.UtcNow.Date;
        if (eventDate.Value.Date < today) return "Past";
        if (eventDate.Value.Date > today) return "Future";
        return "Present";
    }

    #endregion
}

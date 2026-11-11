namespace ReminderAgent.Common;

/// <summary>One row parsed from a bulk-upload CSV/JSON/ZIP file.</summary>
public record BulkAssetRow(string Name, string Category, string? City,
    string? Country, string? Tags, string? EventDate, string? UserExperience);

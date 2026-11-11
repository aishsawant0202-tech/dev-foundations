namespace ReminderAgent.Common;

/// <summary>Duplicate-detection for saving/uploading a reminder.</summary>
/// <remarks>
/// Shared by <c>ReminderTools.CreateAsset</c> and <c>BulkUploadWorker</c>. Match key is
/// trimmed, case-insensitive Name + Category. Same EventDate = exact duplicate (skip);
/// different EventDate = a new occurrence (update the existing row instead of a new one).
/// </remarks>
public static class DuplicateAssetHelpers
{
    #region Matching

    /// <summary>Finds an existing Name+Category match, or null.</summary>
    public static Asset? FindMatch(IEnumerable<Asset> existingAssets, string name, string category)
    {
        var trimmedName = name.Trim();
        var trimmedCategory = category.Trim();
        return existingAssets.FirstOrDefault(a =>
            a.Name.Trim().Equals(trimmedName, StringComparison.OrdinalIgnoreCase) &&
            (a.Category ?? string.Empty).Trim().Equals(trimmedCategory, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when a <see cref="FindMatch"/> result also has the same EventDate.</summary>
    public static bool IsExactDuplicate(Asset existingMatch, DateTime? newEventDate) =>
        existingMatch.EventDate == newEventDate;

    #endregion

    #region Merging

    /// <summary>Prepares <paramref name="incoming"/> for an update-in-place save.</summary>
    /// <remarks>
    /// Takes Id/CreatedAt from <paramref name="existing"/>, and backfills any blank
    /// optional field from it too — otherwise a sparser new save (e.g. no tags this
    /// time) would silently null out fields the original save had.
    /// </remarks>
    public static Asset MergeForUpdate(Asset incoming, Asset existing)
    {
        incoming.Id = existing.Id;
        incoming.CreatedAt = existing.CreatedAt;
        incoming.Tags = string.IsNullOrWhiteSpace(incoming.Tags) ? existing.Tags : incoming.Tags;
        incoming.City = string.IsNullOrWhiteSpace(incoming.City) ? existing.City : incoming.City;
        incoming.Country = string.IsNullOrWhiteSpace(incoming.Country) ? existing.Country : incoming.Country;
        incoming.Region = string.IsNullOrWhiteSpace(incoming.Region) ? existing.Region : incoming.Region;
        incoming.UserExperience = string.IsNullOrWhiteSpace(incoming.UserExperience) ? existing.UserExperience : incoming.UserExperience;
        incoming.UserInput = string.IsNullOrWhiteSpace(incoming.UserInput) ? existing.UserInput : incoming.UserInput;
        return incoming;
    }

    #endregion
}

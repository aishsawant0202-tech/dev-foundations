using ReminderAgent.Domain;

namespace ReminderAgent.Interfaces
{
    /// <summary>
    /// Defines a contract for persisting, retrieving, and updating asset data.
    /// </summary>
    public interface IStorageProvider
    {

        /// <summary>
        /// Saves a new asset to the storage.
        /// </summary>
        /// <param name="asset">The asset to be saved.</param>
        /// <returns>
        /// A task containing true if the asset was successfully saved; otherwise, false.
        /// </returns>
        Task<bool> SaveAssetAsync(ReminderAgent.Domain.Asset asset);

        /// <summary>
        /// Retrieves assets from storage with optional filtering criteria.
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <param name="tag">Optional tag filter.</param>
        /// <param name="timelineState">Optional timeline state filter.</param>
        /// <returns>
        /// A task containing a collection of assets matching the specified filters.
        /// </returns>
        Task<IEnumerable<Asset>> GetAssetsAsync(
            string? category = null,
            string? tag = null,
            string? timelineState = null);

        /// <summary>
        /// Updates an existing asset in the storage.
        /// </summary>
        /// <param name="asset">The asset with updated values.</param>
        /// <returns>
        /// A task containing true if the update was successful; otherwise, false.
        /// </returns>
        Task<bool> UpdateAssetAsync(Asset asset);
    }
}

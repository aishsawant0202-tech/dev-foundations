#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReminderAgent.Common
{
    /// <summary>
    /// Defines CRUD and query operations for Assets, backed by Azure SQL Database
    /// (the dbo.Assets table). Country/Region/Embedding are plain columns on Assets in
    /// this schema, so no separate lookup or vector tables are involved. Photo upload
    /// and deletion are handled separately by IPhotoStorageProvider; this layer only
    /// reads photo metadata that has already been linked to an asset.
    /// </summary>
    public interface IDataAccessLayer
    {
        #region Asset CRUD

        /// <summary>Inserts a new Asset row and returns the generated Id.</summary>
        Task<Guid> CreateAssetAsync(Asset asset);

        /// <summary>Loads a single Asset by its Id, or null if no row exists.</summary>
        Task<Asset?> GetAssetByIdAsync(Guid assetId);

        /// <summary>
        /// Loads every Asset row. Used for full-listing tool scenarios. Does not include
        /// each asset's embedding vector — use GetAllEmbeddingsAsync for that, so a plain
        /// listing call stays lightweight.
        /// </summary>
        Task<IEnumerable<Asset>> GetAllAssetsAsync();

        /// <summary>
        /// Filters Assets by category and/or tag, matching the existing SearchAssets tool
        /// behaviour. Pass null to skip a filter.
        /// </summary>
        Task<IEnumerable<Asset>> SearchAssetsAsync(string? category, string? tag);

        /// <summary>
        /// Updates an existing Asset row in place (excluding the embedding column — use
        /// SaveEmbeddingAsync for that).
        /// </summary>
        Task UpdateAssetAsync(Asset asset);

        /// <summary>Deletes an Asset by Id. Its Photos rows are removed automatically via ON DELETE CASCADE.</summary>
        Task DeleteAssetAsync(Guid assetId);

        #endregion

        #region Embeddings

        /// <summary>
        /// Updates just the Embedding column for an Asset. Embedding is a native
        /// VECTOR(1536) column — SqlVector&lt;float&gt; serialisation happens inside the
        /// implementation, not here.
        /// </summary>
        Task SaveEmbeddingAsync(Guid assetId, float[] vector);

        /// <summary>Loads the embedding vector for a single Asset, or null if none has been saved yet.</summary>
        Task<float[]?> GetEmbeddingAsync(Guid assetId);

        /// <summary>
        /// Loads every (AssetId, Vector) pair where Embedding is not null, for in-memory
        /// cosine similarity scoring, matching SimilarityService's existing contract.
        /// </summary>
        Task<IDictionary<Guid, float[]>> GetAllEmbeddingsAsync();

        #endregion

        #region Photos

        /// <summary>
        /// Loads BlobUrl/FileName/ContentType metadata for every photo linked to an Asset,
        /// used by GetAssetDetails. Uploading and deleting photos themselves is handled by
        /// IPhotoStorageProvider, not this method.
        /// </summary>
        Task<IEnumerable<AssetPhotoRef>> GetPhotoRefsForAssetAsync(Guid assetId);

        /// <summary>
        /// Returns the distinct set of AssetIds that have at least one row in Photos, in a
        /// single query. Used by GetAssetDetails to answer "which reminders have images
        /// attached?" without an N+1 lookup per asset.
        /// </summary>
        Task<HashSet<Guid>> GetAssetIdsWithPhotosAsync();

        /// <summary>Deletes all photos for an asset by name.</summary>
        Task<int> DeletePhotosByAssetNameAsync(
            string assetName,
            IPhotoStorageProvider photoStorage);

        /// <summary>Deletes a photo from Blob Storage and removes its row from the Photos SQL table.</summary>
        Task DeletePhotoAsync(Guid photoId, IPhotoStorageProvider photoStorage);

        /// <summary>Inserts a photo reference into the Photos SQL table.</summary>
        Task InsertPhotoAsync(Guid photoId, Guid assetId, string blobName,
            string blobUrl, string accessKey, string fileName);

        #endregion
    }
}

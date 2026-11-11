using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Microsoft.Extensions.Configuration;
using ReminderAgent.Common;

namespace ReminderAgent.Infrastructure
{
    public class SqlDataAccessLayer : IDataAccessLayer
    {
        #region Fields

        /// <summary>
        /// Azure SQL connection string, read from the "SqlServer" entry under
        /// ConnectionStrings in appsettings.json / appsettings.dev.json.
        /// </summary>
        private readonly string _connectionString;

        #endregion

        #region Constructor

        public SqlDataAccessLayer(IConfiguration configuration)
        {
            _connectionString = configuration
                .GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException(
                    "Connection string 'SqlServer' not found in appsettings.json");
        }

        #endregion

        #region Asset CRUD

        /// <summary>
        /// Inserts a new row into dbo.Assets, generating a new <see cref="Guid"/> if
        /// <paramref name="asset"/>.Id is empty.
        /// </summary>
        /// <returns>The asset's Id, so the caller knows what was actually inserted.</returns>
        public async Task<Guid> CreateAssetAsync(Asset asset)
        {
            if (asset.Id == Guid.Empty)
                asset.Id = Guid.NewGuid();

            const string sql = @"
                INSERT INTO dbo.Assets
                    (Id, Name, Category, Tags, TimelineState, EventDate,
                     CreatedAt, UserInput, UserExperience,
                     City, Country, Region, Embedding)
                VALUES
                    (@Id, @Name, @Category, @Tags, @TimelineState, @EventDate,
                     @CreatedAt, @UserInput, @UserExperience,
                     @City, @Country, @Region, @Embedding)";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id",             asset.Id);
            cmd.Parameters.AddWithValue("@Name",           asset.Name);
            cmd.Parameters.AddWithValue("@Category",       (object?)asset.Category       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tags",           (object?)asset.Tags           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TimelineState",  (object?)asset.TimelineState  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EventDate",      (object?)asset.EventDate      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt",      asset.CreatedAt);
            cmd.Parameters.AddWithValue("@UserInput",      (object?)asset.UserInput      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserExperience", (object?)asset.UserExperience ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City",           (object?)asset.City           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Country",        (object?)asset.Country        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Region",         (object?)asset.Region         ?? DBNull.Value);
            // Embedding column is a native VECTOR(1536) — SqlVector<float> round-trips it.
            cmd.Parameters.AddWithValue("@Embedding",
                asset.Embedding != null
                    ? (object)new SqlVector<float>(asset.Embedding)
                    : DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return asset.Id;
        }

        /// <summary>
        /// Loads a single Asset row by its <see cref="Guid"/>.
        /// </summary>
        /// <returns>The asset, or <see langword="null"/> if no matching row exists.</returns>
        public async Task<Asset?> GetAssetByIdAsync(Guid assetId)
        {
            const string sql = @"
                SELECT Id, Name, Category, Tags, TimelineState, EventDate,
                       CreatedAt, UserInput, UserExperience,
                       City, Country, Region, Embedding
                FROM dbo.Assets
                WHERE Id = @Id";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", assetId);

            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapAsset(reader) : null;
        }

        /// <summary>
        /// Loads every Asset row, newest first. Embedding is included — SimilarityService
        /// needs it for search.
        /// </summary>
        public async Task<IEnumerable<Asset>> GetAllAssetsAsync()
        {
            const string sql = @"
                SELECT Id, Name, Category, Tags, TimelineState, EventDate,
                       CreatedAt, UserInput, UserExperience,
                       City, Country, Region, Embedding
                FROM dbo.Assets
                ORDER BY CreatedAt DESC";

            var assets = new List<Asset>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd    = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                assets.Add(MapAsset(reader));

            return assets;
        }

        /// <summary>
        /// Filters assets by category and/or tag. Pass <see langword="null"/> for either
        /// parameter to skip that filter.
        /// </summary>
        /// <remarks>
        /// Tags uses <c>LIKE %tag%</c> because tags are stored as semicolon-delimited strings.
        /// </remarks>
        public async Task<IEnumerable<Asset>> SearchAssetsAsync(
            string? category, string? tag)
        {
            var where = new List<string>();
            if (category != null) where.Add("Category = @Category");
            if (tag      != null) where.Add("Tags LIKE @Tag");

            var sql = @"
                SELECT Id, Name, Category, Tags, TimelineState, EventDate,
                       CreatedAt, UserInput, UserExperience,
                       City, Country, Region, Embedding
                FROM dbo.Assets";

            if (where.Count > 0)
                sql += " WHERE " + string.Join(" AND ", where);

            sql += " ORDER BY CreatedAt DESC";

            var assets = new List<Asset>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);

            if (category != null)
                cmd.Parameters.AddWithValue("@Category", category);
            if (tag != null)
                cmd.Parameters.AddWithValue("@Tag", $"%{tag}%");

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                assets.Add(MapAsset(reader));

            return assets;
        }

        /// <summary>
        /// Updates all columns of an Asset row EXCEPT Embedding.
        /// </summary>
        /// <remarks>
        /// Use <see cref="SaveEmbeddingAsync"/> separately to update the embedding.
        /// </remarks>
        public async Task UpdateAssetAsync(Asset asset)
        {
            const string sql = @"
                UPDATE dbo.Assets SET
                    Name           = @Name,
                    Category       = @Category,
                    Tags           = @Tags,
                    TimelineState  = @TimelineState,
                    EventDate      = @EventDate,
                    UserInput      = @UserInput,
                    UserExperience = @UserExperience,
                    City           = @City,
                    Country        = @Country,
                    Region         = @Region
                WHERE Id = @Id";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id",             asset.Id);
            cmd.Parameters.AddWithValue("@Name",           asset.Name);
            cmd.Parameters.AddWithValue("@Category",       (object?)asset.Category       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tags",           (object?)asset.Tags           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TimelineState",  (object?)asset.TimelineState  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EventDate",      (object?)asset.EventDate      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserInput",      (object?)asset.UserInput      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserExperience", (object?)asset.UserExperience ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City",           (object?)asset.City           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Country",        (object?)asset.Country        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Region",         (object?)asset.Region         ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes the asset row. dbo.Photos rows are removed automatically by SQL Server
        /// via ON DELETE CASCADE on FK_Photos_Assets.
        /// </summary>
        public async Task DeleteAssetAsync(Guid assetId)
        {
            const string sql = "DELETE FROM dbo.Assets WHERE Id = @Id";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", assetId);
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Embeddings

        /// <summary>
        /// Writes an asset's embedding vector. Called separately after
        /// <see cref="CreateAssetAsync"/>, once the embedding has been generated by
        /// EmbeddingService.
        /// </summary>
        /// <remarks>
        /// Embedding column is a native VECTOR(1536) — <see cref="SqlVector{T}"/> round-trips it.
        /// </remarks>
        public async Task SaveEmbeddingAsync(Guid assetId, float[] vector)
        {
            var sqlVector = new SqlVector<float>(vector);

            const string sql = @"
                UPDATE dbo.Assets
                SET Embedding = @Embedding
                WHERE Id = @Id";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id",        assetId);
            cmd.Parameters.AddWithValue("@Embedding", sqlVector);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Loads the embedding for a single asset.
        /// </summary>
        /// <returns>The embedding vector, or <see langword="null"/> if none has been saved yet.</returns>
        public async Task<float[]?> GetEmbeddingAsync(Guid assetId)
        {
            const string sql = @"
                SELECT Embedding
                FROM dbo.Assets
                WHERE Id = @Id";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", assetId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || await reader.IsDBNullAsync(0)) return null;

            var vector = reader.GetSqlVector<float>(0);
            return vector.IsNull ? null : vector.Memory.ToArray();
        }

        /// <summary>
        /// Loads every AssetId + Embedding pair where Embedding is not null, for
        /// SimilarityService to score all assets in memory.
        /// </summary>
        public async Task<IDictionary<Guid, float[]>> GetAllEmbeddingsAsync()
        {
            const string sql = @"
                SELECT Id, Embedding
                FROM dbo.Assets
                WHERE Embedding IS NOT NULL";

            var results = new Dictionary<Guid, float[]>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd    = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id     = reader.GetGuid(0);
                var vector = reader.GetSqlVector<float>(1);
                if (!vector.IsNull) results[id] = vector.Memory.ToArray();
            }

            return results;
        }

        #endregion

        #region Photos

        /// <summary>
        /// Loads photo metadata (BlobUrl, FileName, ContentType, UploadedAt) from dbo.Photos
        /// for a given asset.
        /// </summary>
        /// <remarks>
        /// AccessKey is NOT returned here — <see cref="IPhotoStorageProvider"/> generates a
        /// fresh SAS token when needed.
        /// </remarks>
        public async Task<IEnumerable<AssetPhotoRef>> GetPhotoRefsForAssetAsync(
            Guid assetId)
        {
            const string sql = @"
                SELECT Id, BlobUrl, AccessKey, FileName, ContentType, UploadedAt
                FROM dbo.Photos
                WHERE AssetId = @AssetId
                ORDER BY UploadedAt DESC";

            var photos = new List<AssetPhotoRef>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@AssetId", assetId);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                photos.Add(new AssetPhotoRef
                {
                    Id          = reader.GetGuid(reader.GetOrdinal("Id")),
                    BlobUrl     = reader.GetString(reader.GetOrdinal("BlobUrl")),
                    AccessKey = reader.IsDBNull(reader.GetOrdinal("AccessKey"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("AccessKey")),
                    FileName    = reader.IsDBNull(reader.GetOrdinal("FileName"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("FileName")),
                    ContentType = reader.IsDBNull(reader.GetOrdinal("ContentType"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("ContentType")),
                    UploadedAt  = reader.GetDateTime(reader.GetOrdinal("UploadedAt")),
                });
            }

            return photos;
        }

        /// <summary>
        /// Single-query lookup of which assets have at least one row in dbo.Photos, so
        /// GetAssetDetails can answer "which reminders have images attached?" without an
        /// N+1 query per asset.
        /// </summary>
        public async Task<HashSet<Guid>> GetAssetIdsWithPhotosAsync()
        {
            const string sql = "SELECT DISTINCT AssetId FROM dbo.Photos";

            var ids = new HashSet<Guid>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                ids.Add(reader.GetGuid(reader.GetOrdinal("AssetId")));

            return ids;
        }

        /// <summary>
        /// Deletes a photo from Blob Storage and removes its row from SQL.
        /// </summary>
        public async Task DeletePhotoAsync(
            Guid photoId,
            IPhotoStorageProvider photoStorage)
        {
            // Step 1 — Get BlobName from SQL
            const string selectSql = @"
                SELECT BlobName
                FROM dbo.Photos
                WHERE Id = @PhotoId";

            string? blobName = null;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using (var selectCmd = new SqlCommand(selectSql, conn))
            {
                selectCmd.Parameters.AddWithValue("@PhotoId", photoId);
                var result = await selectCmd.ExecuteScalarAsync();

                if (result is null or DBNull)
                {
                    Console.WriteLine($"Photo with Id {photoId} not found in SQL.");
                    return;
                }

                blobName = result.ToString();
            }

            // Step 2 — Delete blob from Blob Storage
            await photoStorage.DeletePhotoAsync(blobName!);
            Console.WriteLine($"Blob deleted: {blobName}");

            // Step 3 — Delete row from SQL Photos table
            const string deleteSql = @"
                DELETE FROM dbo.Photos
                WHERE Id = @PhotoId";

            await using var deleteCmd = new SqlCommand(deleteSql, conn);
            deleteCmd.Parameters.AddWithValue("@PhotoId", photoId);
            await deleteCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Photo row deleted from SQL: {photoId}");
        }

        /// <summary>
        /// Deletes all photos for an asset, looked up by asset name: finds the asset, reads
        /// all of its photo rows, deletes each blob from Blob Storage, then deletes all photo
        /// rows from SQL.
        /// </summary>
        /// <returns>The number of photos deleted (0 if the asset wasn't found).</returns>
        public async Task<int> DeletePhotosByAssetNameAsync(
            string assetName,
            IPhotoStorageProvider photoStorage)
        {
            // Step 1 — Find asset by name
            const string findAssetSql = @"
                SELECT Id FROM dbo.Assets
                WHERE Name = @Name";

            Guid? assetId = null;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using (var findCmd = new SqlCommand(findAssetSql, conn))
            {
                findCmd.Parameters.AddWithValue("@Name", assetName);
                var result = await findCmd.ExecuteScalarAsync();
                if (result is null or DBNull)
                    return 0; // asset not found
                assetId = (Guid)result;
            }

            // Step 2 — Get all photos for this asset
            const string getPhotosSql = @"
                SELECT Id, BlobName FROM dbo.Photos
                WHERE AssetId = @AssetId";

            var photos = new List<(Guid Id, string BlobName)>();

            await using (var getCmd = new SqlCommand(getPhotosSql, conn))
            {
                getCmd.Parameters.AddWithValue("@AssetId", assetId);
                await using var reader = await getCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    photos.Add((
                        reader.GetGuid(0),
                        reader.GetString(1)
                    ));
                }
            }

            if (!photos.Any()) return 0;

            // Step 3 — Delete each blob from Blob Storage
            foreach (var (_, blobName) in photos)
            {
                await photoStorage.DeletePhotoAsync(blobName);
            }

            // Step 4 — Delete all photo rows from SQL
            const string deletePhotosSql = @"
                DELETE FROM dbo.Photos
                WHERE AssetId = @AssetId";

            await using var deleteCmd = new SqlCommand(deletePhotosSql, conn);
            deleteCmd.Parameters.AddWithValue("@AssetId", assetId);
            await deleteCmd.ExecuteNonQueryAsync();

            return photos.Count;
        }

        /// <summary>
        /// Inserts a photo reference row into dbo.Photos.
        /// </summary>
        public async Task InsertPhotoAsync(Guid photoId, Guid assetId,
            string blobName, string blobUrl, string accessKey, string fileName)
        {
            const string sql = @"
                INSERT INTO dbo.Photos
                    (Id, AssetId, BlobName, BlobUrl,AccessKey,FileName)
                VALUES
                    (@Id, @AssetId, @BlobName, @BlobUrl, @AccessKey, @FileName)";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id",       photoId);
            cmd.Parameters.AddWithValue("@AssetId",  assetId);
            cmd.Parameters.AddWithValue("@BlobName", blobName);
            cmd.Parameters.AddWithValue("@BlobUrl",  blobUrl);
            cmd.Parameters.AddWithValue("@AccessKey", accessKey);
            cmd.Parameters.AddWithValue("@FileName", fileName);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Maps one SQL data reader row into an <see cref="Asset"/> object.
        /// </summary>
        /// <remarks>
        /// Embedding column is a native VECTOR(1536) (see schema.sql) — read via
        /// <c>GetSqlVector&lt;float&gt;</c>, not a byte[]/string cast.
        /// </remarks>
        private static Asset MapAsset(SqlDataReader r) => new Asset
        {
            Id             = r.GetGuid(r.GetOrdinal("Id")),
            Name           = r.GetString(r.GetOrdinal("Name")),
            Category       = r.IsDBNull(r.GetOrdinal("Category"))
                                ? string.Empty
                                : r.GetString(r.GetOrdinal("Category")),
            Tags           = r.IsDBNull(r.GetOrdinal("Tags"))
                                ? null
                                : r.GetString(r.GetOrdinal("Tags")),
            TimelineState  = r.IsDBNull(r.GetOrdinal("TimelineState"))
                                ? "Present"
                                : r.GetString(r.GetOrdinal("TimelineState")),
            EventDate      = r.IsDBNull(r.GetOrdinal("EventDate"))
                                ? null
                                : r.GetDateTime(r.GetOrdinal("EventDate")),
            CreatedAt      = r.GetDateTime(r.GetOrdinal("CreatedAt")),
            UserInput      = r.IsDBNull(r.GetOrdinal("UserInput"))
                                ? null
                                : r.GetString(r.GetOrdinal("UserInput")),
            UserExperience = r.IsDBNull(r.GetOrdinal("UserExperience"))
                                ? null
                                : r.GetString(r.GetOrdinal("UserExperience")),
            City           = r.IsDBNull(r.GetOrdinal("City"))
                                ? null
                                : r.GetString(r.GetOrdinal("City")),
            Country        = r.IsDBNull(r.GetOrdinal("Country"))
                                ? null
                                : r.GetString(r.GetOrdinal("Country")),
            Region         = r.IsDBNull(r.GetOrdinal("Region"))
                                ? null
                                : r.GetString(r.GetOrdinal("Region")),
            Embedding      = r.IsDBNull(r.GetOrdinal("Embedding"))
                                ? null
                                : r.GetSqlVector<float>(r.GetOrdinal("Embedding")) is { IsNull: false } v
                                    ? v.Memory.ToArray()
                                    : null,
        };

        #endregion
    }
}

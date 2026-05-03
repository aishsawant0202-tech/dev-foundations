using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using System.Text.Json;

namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// Provides a JSON-based implementation of <see cref="IEmbeddingStore"/>.
    /// Handles storage, retrieval, and deletion of embeddings using a file-based approach.
    /// </summary>
    public class JsonEmbeddingStore : IEmbeddingStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonEmbeddingStore"/> class.
        /// Ensures the storage directory exists and sets up the JSON file path.
        /// </summary>
        /// <param name="directory">The directory where embeddings will be stored.</param>
        public JsonEmbeddingStore(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "embeddings.json");
            FileLogger.Info($"JsonEmbeddingStore initialised. File: {_filePath}");
        }

        /// <summary>
        /// Saves or updates an embedding for a given asset identifier.
        /// </summary>
        /// <param name="assetId">The unique identifier of the asset.</param>
        /// <param name="embedding">The embedding vector to store.</param>
        public async Task SaveEmbeddingAsync(Guid assetId, float[] embedding)
        {
            await _lock.WaitAsync();
            try
            {
                var dict = await ReadFileAsync();
                dict[assetId] = embedding;
                await WriteFileAsync(dict);
                FileLogger.Info($"JsonEmbeddingStore | Saved embedding for asset: {assetId}");
            }
            catch (Exception ex)
            {
                FileLogger.Error($"JsonEmbeddingStore.SaveEmbeddingAsync failed: {ex.Message}");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Loads an embedding for a specific asset identifier.
        /// </summary>
        /// <param name="assetId">The unique identifier of the asset.</param>
        /// <returns>
        /// The embedding vector if found; otherwise, null.
        /// </returns>
        public async Task<float[]?> LoadEmbeddingAsync(Guid assetId)
        {
            await _lock.WaitAsync();
            try
            {
                var dict = await ReadFileAsync();
                return dict.TryGetValue(assetId, out var emb) ? emb : null;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"JsonEmbeddingStore.LoadEmbeddingAsync failed: {ex.Message}");
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Loads all embeddings from the JSON store.
        /// </summary>
        /// <returns>
        /// A dictionary mapping asset identifiers to their embedding vectors.
        /// </returns>
        public async Task<Dictionary<Guid, float[]>> LoadAllEmbeddingsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var dict = await ReadFileAsync();
                FileLogger.Info($"JsonEmbeddingStore | Loaded {dict.Count} embeddings.");
                return dict;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"JsonEmbeddingStore.LoadAllEmbeddingsAsync failed: {ex.Message}");
                return new Dictionary<Guid, float[]>();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Deletes the embedding associated with the specified asset identifier.
        /// </summary>
        /// <param name="assetId">The unique identifier of the asset.</param>
        public async Task DeleteEmbeddingAsync(Guid assetId)
        {
            await _lock.WaitAsync();
            try
            {
                var dict = await ReadFileAsync();
                if (dict.Remove(assetId))
                {
                    await WriteFileAsync(dict);
                    FileLogger.Info($"JsonEmbeddingStore | Deleted embedding for: {assetId}");
                }

            }
            catch (Exception ex)
            {
                FileLogger.Error($"JsonEmbeddingStore.DeleteEmbeddingAsync failed: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Reads the JSON file and deserializes it into a dictionary of embeddings.
        /// </summary>
        /// <returns>
        /// A dictionary mapping asset identifiers to embedding vectors.
        /// </returns>
        private async Task<Dictionary<Guid, float[]>> ReadFileAsync()
        {
            if (!File.Exists(_filePath))
                return new Dictionary<Guid, float[]>();

            await using var stream = new FileStream(
                _filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<Dictionary<Guid, float[]>>(
                stream, _jsonOptions);
            return result ?? new Dictionary<Guid, float[]>();
        }

        /// <summary>
        /// Writes the embedding dictionary to disk using a temporary file
        /// and performs an atomic replacement to ensure data integrity.
        /// </summary>
        /// <param name="dict">The embedding dictionary to persist.</param>
        private async Task WriteFileAsync(Dictionary<Guid, float[]> dict)
        {
            string tempPath = _filePath + ".tmp";

            await using (var stream = new FileStream(
                             tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, dict, _jsonOptions);
            }

            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, null);  // atomic swap — subsequent writes
            else
                File.Move(tempPath, _filePath);
        }
    }
}


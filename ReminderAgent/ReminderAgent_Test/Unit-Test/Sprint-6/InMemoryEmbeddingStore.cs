using ReminderAgent.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnitTest_SprintSix
{
    /// <summary>In-memory stub for IEmbeddingStore.</summary>
    public class InMemoryEmbeddingStore : IEmbeddingStore
    {
        private readonly Dictionary<Guid, float[]> _store = new();

        public Task SaveEmbeddingAsync(Guid assetId, float[] embedding)
        {
            _store[assetId] = embedding;
            return Task.CompletedTask;
        }

        public Task<float[]?> LoadEmbeddingAsync(Guid assetId)
            => Task.FromResult(_store.TryGetValue(assetId, out var e) ? e : (float[]?)null);

        public Task<Dictionary<Guid, float[]>> LoadAllEmbeddingsAsync()
            => Task.FromResult(new Dictionary<Guid, float[]>(_store));

        public Task DeleteEmbeddingAsync(Guid assetId)
        {
            _store.Remove(assetId);
            return Task.CompletedTask;
        }
    }
}

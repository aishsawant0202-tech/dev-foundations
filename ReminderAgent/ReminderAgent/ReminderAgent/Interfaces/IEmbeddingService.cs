namespace ReminderAgent.Interfaces
{
    /// <summary>
    /// Defines a contract for generating vector embeddings for text data.
    /// </summary>
    public interface IEmbeddingService
    {

        /// <summary>
        /// Generates an embedding vector for a reminder using structured input fields.
        /// </summary>
        /// <param name="name">The name of the reminder.</param>
        /// <param name="category">The category of the reminder.</param>
        /// <param name="userExperience">User-provided experience or description.</param>
        /// <param name="userInput">Additional user input details (optional).</param>
        /// <param name="tags">Associated tags (optional).</param>
        /// <returns>
        /// A float array representing the generated embedding vector.
        /// </returns>
        Task<float[]> GenerateEmbeddingAsync(
            string name,
            string category,
            string userExperience,
            string userInput = "",
            string tags = "" );

        /// <summary>
        /// Generates an embedding vector for a search query.
        /// </summary>
        /// <param name="query">The search query text.</param>
        /// <returns>
        /// A float array representing the query embedding vector.
        /// </returns>
        Task<float[]> GenerateQueryEmbeddingAsync(string query);
    }
    
    
}
using System.Text.Json.Serialization;

namespace ReminderAgent.Domain
{
    /// <summary>
    /// Represents a single saved reminder asset 
    /// (e.g., book, restaurant, travel destination).
    /// </summary>
    public class Asset
    {
        /// <summary>
        /// Gets or sets the unique identifier for the reminder.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the name of the item 
        /// (e.g., "The Alchemist", "Joe's Pizza").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the asset 
        /// (e.g., Book, Restaurant, Travel).
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tags used for filtering 
        /// (e.g., "Italian", "Adventure").
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the timeline state relative to today 
        /// (Past, Present, Future).
        /// </summary>
        [JsonPropertyName("timelineState")]
        public string TimelineState { get; set; } = "Present";

        /// <summary>
        /// Gets or sets the event date mentioned by the user.
        /// </summary>
        [JsonPropertyName("eventDate")]
        public DateTime? EventDate { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the reminder was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets additional metadata such as 
        /// address, author, rating, or location details.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the raw user input text.
        /// </summary>
        [JsonPropertyName("userInput")]
        public string UserInput { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user experience description 
        /// (e.g., "wonderful", "boring").
        /// </summary>
        [JsonPropertyName("userExperience")]
        public string UserExperience { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of photo references 
        /// (e.g., "Photos/paris.jpg").
        /// </summary>

        [JsonPropertyName("photoRefs")]
        public List<string> PhotoRefs { get; set; } = new();

        /// <summary>
        /// Gets or sets the embedding vector used for semantic search.
        /// </summary>
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
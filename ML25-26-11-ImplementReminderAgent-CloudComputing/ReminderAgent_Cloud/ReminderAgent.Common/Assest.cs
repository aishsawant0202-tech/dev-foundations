using System;
using System.Text.Json.Serialization;

namespace ReminderAgent.Common
{
    /// <summary>
    /// Represents a single saved reminder asset.
    /// Maps directly to dbo.Assets table in Azure SQL Database.
    /// </summary>
    public class Asset
    {
        #region Identity & Core Fields

        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        #endregion

        #region Timeline

        [JsonPropertyName("timelineState")]
        public string TimelineState { get; set; } = "Present";

        [JsonPropertyName("eventDate")]
        public DateTime? EventDate { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region User-Provided Content

        [JsonPropertyName("userInput")]
        public string? UserInput { get; set; }

        [JsonPropertyName("userExperience")]
        public string? UserExperience { get; set; }

        #endregion

        #region Location

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        #endregion

        #region Embedding

        /// <summary>
        /// The float[1536] embedding vector from OpenAI text-embedding-3-small, stored as a
        /// native VECTOR(1536) column in SQL
        /// </summary>
        [JsonIgnore]
        public float[]? Embedding { get; set; }

        #endregion
    }
}
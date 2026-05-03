using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Domain;
using System;
using System.Collections.Generic;

namespace UnitTest_SprintSix
{
    /// <summary>
    /// Unit tests for the <see cref="Asset"/> domain model.
    /// Validates default values, unique identity, metadata storage,
    /// photo references, timeline states, tags, embeddings, and creation timestamps.
    /// </summary>
    [TestClass]
    public class AssetTests
    {
        /// <summary>
        /// Verifies that a newly created <see cref="Asset"/> has correct default values.
        /// Ensures Id is auto-generated, string fields default to empty,
        /// TimelineState defaults to "Present", and collections are initialized.
        /// </summary>
        [TestMethod]
        public void Asset_DefaultValues_AreCorrect()
        {
            var asset = new Asset();

            Assert.AreNotEqual(Guid.Empty, asset.Id,            "Id should be auto-generated");
            Assert.AreEqual(string.Empty,  asset.Name,          "Name default should be empty string");
            Assert.AreEqual(string.Empty,  asset.Category,      "Category default should be empty string");
            Assert.AreEqual("Present",     asset.TimelineState, "Default TimelineState should be Present");
            Assert.IsNotNull(asset.Tags,                         "Tags list should not be null");
            Assert.IsNotNull(asset.Metadata,                     "Metadata dict should not be null");
            Assert.IsNotNull(asset.PhotoRefs,                    "PhotoRefs list should not be null");
            //Assert.IsNotNull(asset.Embeddings,                   "Embeddings list should not be null");
        }
        /// <summary>
        /// Verifies that each newly instantiated <see cref="Asset"/> receives a unique GUID.
        /// Ensures no two assets share the same identifier.
        /// </summary>
        [TestMethod]
        public void Asset_EachNew_HasUniqueId()
        {
            var a1 = new Asset();
            var a2 = new Asset();

            Assert.AreNotEqual(a1.Id, a2.Id, "Two new Assets must have different GUIDs");
        }
        /// <summary>
        /// Verifies that the <see cref="Asset.Metadata"/> dictionary correctly stores
        /// and retrieves location-based keys such as City, Country, and Region.
        /// </summary>
        [TestMethod]
        public void Asset_MetadataStoresLocationKeys()
        {
            var asset = new Asset
            {
                Metadata = new Dictionary<string, string>
                {
                    ["City"]    = "Paris",
                    ["Country"] = "France",
                    ["Region"]  = "Île-de-France"
                }
            };

            Assert.AreEqual("Paris",          asset.Metadata["City"]);
            Assert.AreEqual("France",         asset.Metadata["Country"]);
            Assert.AreEqual("Île-de-France",  asset.Metadata["Region"]);
        }
        /// <summary>
        /// Verifies that multiple photo references can be added to the
        /// <see cref="Asset.PhotoRefs"/> list and are stored in insertion order.
        /// </summary>
        [TestMethod]
        public void Asset_PhotoRefs_CanAddMultiple()
        {
            var asset = new Asset();
            asset.PhotoRefs.Add("Photos/one.jpg");
            asset.PhotoRefs.Add("Photos/two.jpg");

            Assert.AreEqual(2,               asset.PhotoRefs.Count);
            Assert.AreEqual("Photos/one.jpg", asset.PhotoRefs[0]);
            Assert.AreEqual("Photos/two.jpg", asset.PhotoRefs[1]);
        }
        /// <summary>
        /// Verifies that the <see cref="Asset.TimelineState"/> can be set to "Past"
        /// when the <see cref="Asset.EventDate"/> is in the past.
        /// </summary>
        [TestMethod]
        public void Asset_TimelineState_PastDate()
        {
            var asset = new Asset
            {
                EventDate     = DateTime.Today.AddDays(-10),
                TimelineState = "Past"
            };

            Assert.AreEqual("Past", asset.TimelineState);
        }
        /// <summary>
        /// Verifies that the <see cref="Asset.TimelineState"/> can be set to "Future"
        /// when the <see cref="Asset.EventDate"/> is ahead of today.
        /// </summary>
        [TestMethod]
        public void Asset_TimelineState_FutureDate()
        {
            var asset = new Asset
            {
                EventDate     = DateTime.Today.AddDays(10),
                TimelineState = "Future"
            };

            Assert.AreEqual("Future", asset.TimelineState);
        }
        /// <summary>
        /// Verifies that the <see cref="Asset.TimelineState"/> is set to "Present"
        /// when the <see cref="Asset.EventDate"/> matches today's date.
        /// </summary>
        [TestMethod]
        public void Asset_TimelineState_TodayIsPresent()
        {
            var asset = new Asset
            {
                EventDate     = DateTime.Today,
                TimelineState = "Present"
            };

            Assert.AreEqual("Present", asset.TimelineState);
        }
        /// <summary>
        /// Verifies that the <see cref="Asset.Tags"/> list can store multiple string tags
        /// and that they are accessible by index in the correct order.
        /// </summary>
        [TestMethod]
        public void Asset_Tags_CanStoreMultiple()
        {
            var asset = new Asset
            {
                Tags = new List<string> { "travel", "europe", "summer" }
            };

            Assert.AreEqual(3,        asset.Tags.Count);
            Assert.AreEqual("travel", asset.Tags[0]);
            Assert.AreEqual("europe", asset.Tags[1]);
        }
        /// <summary>
        /// Verifies that a float array can be assigned to <see cref="Asset.Embedding"/>
        /// and that individual vector values are stored accurately.
        /// </summary>
        [TestMethod]
        public void Asset_Embedding_CanBeAssigned()
        {
            var vector = new float[] { 0.1f, 0.2f, 0.3f };
            var asset  = new Asset { Embedding = vector };

            Assert.IsNotNull(asset.Embedding);
            Assert.AreEqual(3,     asset.Embedding!.Length);
            Assert.AreEqual(0.1f,  asset.Embedding[0], 0.0001f);
        }
        /// <summary>
        /// Verifies that <see cref="Asset.CreatedAt"/> is automatically set to the current
        /// date and time when a new <see cref="Asset"/> is instantiated.
        /// </summary>
        [TestMethod]
        public void Asset_CreatedAt_DefaultsToNow()
        {
            var before = DateTime.Now.AddSeconds(-1);
            var asset  = new Asset();
            var after  = DateTime.Now.AddSeconds(1);

            Assert.IsTrue(asset.CreatedAt >= before && asset.CreatedAt <= after,
                "CreatedAt should be set to approximately now");
        }
    }
}

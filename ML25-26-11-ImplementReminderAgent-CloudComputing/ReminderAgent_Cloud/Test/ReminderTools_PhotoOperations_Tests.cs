using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ReminderAgent.Common;
using ReminderAgent.Infrastructure;

namespace ReminderAgent.Tests.Integration
{
    /// <summary>
    /// Integration tests for AttachPhoto and DeleteImage. Real SqlDataAccessLayer and
    /// AzureBlobPhotoStorageProvider (no storage mocks); IEmbeddingService and
    /// ISimilarityService are mocked since neither tool calls them. TOOL-14 through
    /// TOOL-17 cover DeleteImage's name/fileName disambiguation. <see
    /// cref="DoNotParallelizeAttribute"/> avoids racing other real-Azure-SQL test classes.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ReminderTools_PhotoOperations_Tests
    {
        #region Setup

        private static IDataAccessLayer _dal = null!;
        private static IPhotoStorageProvider _photoStorage = null!;
        private static ReminderTools _tools = null!;

        private readonly List<Guid> _assetsToCleanUp = new();
        private static string _tempTestImagePath = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.dev.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            _dal = new SqlDataAccessLayer(configuration);
            _photoStorage = new AzureBlobPhotoStorageProvider(configuration);

            var embeddingServiceMock = new Mock<IEmbeddingService>();   
            var similarityServiceMock = new Mock<ISimilarityService>(); 
            var queueServiceMock = new Mock<IQueueService>();           
            var tableStatusServiceMock = new Mock<ITableStatusService>();

            _tools = new ReminderTools(
                _dal,
                embeddingServiceMock.Object,
                similarityServiceMock.Object,
                _photoStorage,
                queueServiceMock.Object,
                tableStatusServiceMock.Object,
                NullLogger<ReminderTools>.Instance);

            // A tiny real file on disk — AttachPhoto validates File.Exists() before upload.
            _tempTestImagePath = Path.Combine(Path.GetTempPath(), "reminderagent-test-photo.jpg");
            File.WriteAllBytes(_tempTestImagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // minimal JPEG header
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (File.Exists(_tempTestImagePath))
                File.Delete(_tempTestImagePath);
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            // Cascade delete removes the Photos SQL rows but not the underlying blobs —
            // AssetPhotoRef doesn't expose BlobName, so this suite can't delete them
            // directly. Known gap: AssetPhotoRef should expose BlobName for full cleanup.
            foreach (var assetId in _assetsToCleanUp)
            {
                try { await _dal.DeleteAssetAsync(assetId); }
                catch { /* best-effort cleanup */ }
            }
            _assetsToCleanUp.Clear();
        }

        private async Task<Asset> CreateTestAssetAsync(string name = "Test Asset")
        {
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                Name = $"{name} {Guid.NewGuid():N}", // unique per test run, avoids name collisions
                Category = "restaurant",
                CreatedAt = DateTime.UtcNow
            };
            var id = await _dal.CreateAssetAsync(asset);
            asset.Id = id;
            _assetsToCleanUp.Add(id);
            return asset;
        }

        #endregion

        #region AttachPhoto Tests

        /// <summary>
        /// An invalid file extension is rejected before any blob upload.
        /// </summary>
        [TestMethod]
        public async Task TOOL07_AttachPhoto_InvalidExtension_RejectsBeforeUpload()
        {
            var asset = await CreateTestAssetAsync();
            var badPath = Path.Combine(Path.GetTempPath(), $"not-a-photo-{Guid.NewGuid():N}.txt");
            File.WriteAllText(badPath, "not an image");

            try
            {
                var result = await _tools.AttachPhoto(asset.Name, badPath);

                StringAssert.Contains(result, "supported photo format");

                var photos = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
                Assert.IsEmpty(photos, "No photo row should exist — upload never happened.");
            }
            finally
            {
                File.Delete(badPath);
            }
        }

        /// <summary>
        /// A photoPath that doesn't exist on disk returns a not-found message.
        /// </summary>
        [TestMethod]
        public async Task TOOL08_AttachPhoto_FileDoesNotExist_ReturnsNotFoundMessage()
        {
            var asset = await CreateTestAssetAsync();
            var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");

            var result = await _tools.AttachPhoto(asset.Name, missingPath);

            StringAssert.Contains(result, "couldn't find a file");

            var photos = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
            Assert.IsEmpty(photos);
        }

        /// <summary>
        /// An asset name that doesn't resolve to any saved asset returns a not-found message.
        /// </summary>
        [TestMethod]
        public async Task TOOL09_AttachPhoto_AssetNotFound_ReturnsNotFoundMessage()
        {
            var nonExistentAssetName = $"NoSuchAsset-{Guid.NewGuid():N}";

            var result = await _tools.AttachPhoto(nonExistentAssetName, _tempTestImagePath);

            StringAssert.Contains(result, "couldn't find a saved asset");
        }

        /// <summary>
        /// Happy path: uploads to Blob, inserts into SQL, splits BlobUrl/AccessKey correctly
        /// (no '?' leaking into BlobUrl), and the reconstructed URL is genuinely fetchable.
        /// </summary>
        [TestMethod]
        public async Task TOOL10_AttachPhoto_HappyPath_UploadsAndPersistsCorrectly()
        {
            var asset = await CreateTestAssetAsync();

            var result = await _tools.AttachPhoto(asset.Name, _tempTestImagePath);

            StringAssert.Contains(result, "Attached");
            StringAssert.Contains(result, asset.Name);

            var photos = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
            Assert.HasCount(1, photos);

            var photo = photos[0];
            Assert.IsFalse(string.IsNullOrWhiteSpace(photo.BlobUrl));
            Assert.DoesNotContain('?', photo.BlobUrl,
                "BlobUrl should be the base URL only — the SAS token belongs in AccessKey.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(photo.AccessKey));
                Assert.DoesNotStartWith("?", photo.AccessKey,
                "AccessKey should be the raw token, without a leading '?'.");

            using var http = new HttpClient();
            var response = await http.GetAsync($"{photo.BlobUrl}?{photo.AccessKey}");
            Assert.IsTrue(response.IsSuccessStatusCode,
                "The reconstructed BlobUrl + AccessKey must be a working, fetchable link.");
        }

        #endregion

        #region DeleteImage Tests

        /// <summary>
        /// With exactly one photo attached and no filename given, DeleteImage resolves and
        /// deletes it automatically.
        /// </summary>
        [TestMethod]
        public async Task TOOL14_DeleteImage_SinglePhoto_NoFileNameNeeded_DeletesIt()
        {
            var asset = await CreateTestAssetAsync();
            await _tools.AttachPhoto(asset.Name, _tempTestImagePath);

            var result = await _tools.DeleteImage(asset.Name);

            StringAssert.Contains(result, "Deleted photo");

            var remaining = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
            Assert.IsEmpty(remaining);
        }

        /// <summary>
        /// With multiple photos attached and no filename given, DeleteImage must not guess —
        /// it asks for disambiguation and deletes nothing.
        /// </summary>
        [TestMethod]
        public async Task TOOL15_DeleteImage_MultiplePhotos_NoFileName_AsksForDisambiguation()
        {
            var asset = await CreateTestAssetAsync();
            var secondImagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
            File.WriteAllBytes(secondImagePath, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header

            try
            {
                await _tools.AttachPhoto(asset.Name, _tempTestImagePath);
                await _tools.AttachPhoto(asset.Name, secondImagePath);

                var beforeCount = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).Count();
                Assert.AreEqual(2, beforeCount);

                var result = await _tools.DeleteImage(asset.Name);

                StringAssert.Contains(result, "has 2 photos");

                var afterCount = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).Count();
                Assert.AreEqual(2, afterCount, "Nothing should be deleted when the request is ambiguous.");
            }
            finally
            {
                File.Delete(secondImagePath);
            }
        }

        /// <summary>
        /// With multiple photos attached and the correct filename given, only that specific
        /// photo is deleted — the other survives.
        /// </summary>
        [TestMethod]
        public async Task TOOL16_DeleteImage_MultiplePhotos_CorrectFileName_DeletesOnlyThatOne()
        {
            var asset = await CreateTestAssetAsync();
            var secondImagePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
            File.WriteAllBytes(secondImagePath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            var secondFileName = Path.GetFileName(secondImagePath);

            try
            {
                await _tools.AttachPhoto(asset.Name, _tempTestImagePath);
                await _tools.AttachPhoto(asset.Name, secondImagePath);

                var result = await _tools.DeleteImage(asset.Name, secondFileName);

                StringAssert.Contains(result, "Deleted photo");
                StringAssert.Contains(result, secondFileName);

                var remaining = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
                Assert.HasCount(1, remaining);
                Assert.AreNotEqual(secondFileName, remaining[0].FileName);
            }
            finally
            {
                File.Delete(secondImagePath);
            }
        }

        /// <summary>
        /// DeleteImage against an asset name that doesn't exist returns a not-found message.
        /// </summary>
        [TestMethod]
        public async Task TOOL17_DeleteImage_AssetNotFound_ReturnsNotFoundMessage()
        {
            var nonExistentAssetName = $"NoSuchAsset-{Guid.NewGuid():N}";

            var result = await _tools.DeleteImage(nonExistentAssetName);

            StringAssert.Contains(result, "couldn't find a saved asset");
        }

        #endregion
    }
}

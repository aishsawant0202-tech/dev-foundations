using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Common;
using ReminderAgent.Infrastructure;

namespace ReminderAgent.Tests.Integration
{
    /// <summary>
    /// Integration tests for AzureBlobPhotoStorageProvider, against a Azure Blob
    /// Storage 'photos' container — no mocks.
    /// </summary>
    /// <remarks>
    /// Needs a Blob Storage connection string with an AccountKey (SAS generation requires
    /// StorageSharedKeyCredential), nested under "ConnectionStrings:BlobStorage" in
    /// appsettings.test.json — a flat top-level "BlobStorage" key won't be picked up by
    /// GetConnectionString and will throw at ClassInitialize.
    /// </remarks>
    [TestClass]
    public class AzureBlobPhotoStorageProviderTests
    {
        #region Setup

        private static IPhotoStorageProvider _provider = null!;
        private static readonly List<string> _blobsToCleanUp = new();
        private static readonly HttpClient _http = new();
        private static string _blobConnectionString = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.dev.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            _blobConnectionString = configuration.GetConnectionString("BlobStorage")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:BlobStorage not found — check appsettings.test.json.");

            _provider = new AzureBlobPhotoStorageProvider(configuration);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            // Best-effort cleanup of every blob created across this class's tests,
            // so repeated test runs don't leave orphaned files in the 'photos' container.
            foreach (var blobName in _blobsToCleanUp)
            {
                try { await _provider.DeletePhotoAsync(blobName); }
                catch { /* already gone — ignore */ }
            }
            _http.Dispose();
        }

        private static MemoryStream MakeFakeImageStream(string content = "fake-image-bytes")
            => new MemoryStream(Encoding.UTF8.GetBytes(content));

        /// <summary>
        /// Minimal manual query-string parser. Avoids taking a dependency on
        /// System.Web (not normally referenceable outside ASP.NET) just to read
        /// one SAS parameter back out of a URL for assertions.
        /// </summary>
        private static string? GetQueryParam(string url, string key)
        {
            var uri = new Uri(url);
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && Uri.UnescapeDataString(kv[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
            return null;
        }

        #endregion

        #region Upload Tests

        /// <summary>
        /// UploadPhotoAsync returns a well-formed blobName and the blob is actually
        /// retrievable afterwards.
        /// </summary>
        [TestMethod]
        public async Task BLOB01_UploadPhotoAsync_ValidJpg_ReturnsBlobNameAndBlobExists()
        {
            var assetId = Guid.NewGuid();
            using var stream = MakeFakeImageStream();

            var blobName = await _provider.UploadPhotoAsync(assetId, stream, "test-photo.jpg");
            _blobsToCleanUp.Add(blobName);

            Assert.IsFalse(string.IsNullOrWhiteSpace(blobName));
            StringAssert.EndsWith(blobName, ".jpg");
            // "{GUID}.jpg" => 36-char GUID + 4-char extension = 40 chars total.
            Assert.AreEqual(40, blobName.Length);

            await using var downloaded = await _provider.DownloadPhotoAsync(blobName);
            Assert.IsNotNull(downloaded);
        }

        /// <summary>
        /// Content type is derived correctly from the extension and set as the blob's
        /// HTTP header.
        /// </summary>
        [TestMethod]
        public async Task BLOB02_UploadPhotoAsync_PngFile_SetsCorrectContentType()
        {
            var assetId = Guid.NewGuid();
            using var stream = MakeFakeImageStream();

            var blobName = await _provider.UploadPhotoAsync(assetId, stream, "photo.png");
            _blobsToCleanUp.Add(blobName);

            var containerClient = new BlobContainerClient(_blobConnectionString, "photos");
            var blobClient = containerClient.GetBlobClient(blobName);
            var props = await blobClient.GetPropertiesAsync();

            Assert.AreEqual("image/png", props.Value.ContentType);
        }

        #endregion

        #region Download Tests

        /// <summary>
        /// Round-trip fidelity: downloaded bytes match uploaded bytes exactly.
        /// </summary>
        [TestMethod]
        public async Task BLOB03_DownloadPhotoAsync_ReturnsExactOriginalBytes()
        {
            var assetId = Guid.NewGuid();
            var originalBytes = Encoding.UTF8.GetBytes("known-test-content-12345");
            using var uploadStream = new MemoryStream(originalBytes);

            var blobName = await _provider.UploadPhotoAsync(assetId, uploadStream, "roundtrip.jpg");
            _blobsToCleanUp.Add(blobName);

            await using var downloadStream = await _provider.DownloadPhotoAsync(blobName);
            using var ms = new MemoryStream();
            await downloadStream.CopyToAsync(ms);
            var downloadedBytes = ms.ToArray();

            CollectionAssert.AreEqual(originalBytes, downloadedBytes);
        }

        #endregion

        #region Delete Tests

        /// <summary>
        /// After deletion, the blob is actually gone (download throws).
        /// </summary>
        [TestMethod]
        public async Task BLOB04_DeletePhotoAsync_RemovesBlob_SubsequentDownloadThrows()
        {
            var assetId = Guid.NewGuid();
            using var stream = MakeFakeImageStream();
            var blobName = await _provider.UploadPhotoAsync(assetId, stream, "to-delete.jpg");

            await _provider.DeletePhotoAsync(blobName);

            try
            {
                await _provider.DownloadPhotoAsync(blobName);
                Assert.Fail("Expected a RequestFailedException because the blob was deleted.");
            }
            catch (RequestFailedException)
            {
                // Expected — blob no longer exists.
            }
            // No cleanup entry needed — the blob was already deleted by this test.
        }

        /// <summary>
        /// Deleting a blob that was never uploaded doesn't throw (DeleteIfExistsAsync
        /// semantics).
        /// </summary>
        [TestMethod]
        public async Task BLOB05_DeletePhotoAsync_NonExistentBlob_DoesNotThrow()
        {
            var fakeBlobName = $"{Guid.NewGuid()}.jpg"; // never uploaded

            await _provider.DeletePhotoAsync(fakeBlobName); // should complete without exception
        }

        #endregion

        #region SAS / Access Key Tests

        /// <summary>
        /// A generated SAS URL is independently fetchable over plain HTTP with no
        /// additional auth — i.e. the signature is valid.
        /// </summary>
        [TestMethod]
        public async Task BLOB06_GetPhotoAccessKeyAsync_ValidSasUrl_IsPubliclyFetchable()
        {
            var assetId = Guid.NewGuid();
            using var stream = MakeFakeImageStream();
            var blobName = await _provider.UploadPhotoAsync(assetId, stream, "sas-test.jpg");
            _blobsToCleanUp.Add(blobName);

            var sasUrl = await _provider.GetPhotoAccessKeyAsync(blobName, expiryHours: 1);

            var response = await _http.GetAsync(sasUrl);

            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Expected 200 OK from SAS URL, got {(int)response.StatusCode}.");
        }

        /// <summary>
        /// An already-expired SAS token is rejected by Azure (403).
        /// </summary>
        [TestMethod]
        public async Task BLOB07_GetPhotoAccessKeyAsync_ExpiredToken_ReturnsForbidden()
        {
            var assetId = Guid.NewGuid();
            using var stream = MakeFakeImageStream();
            var blobName = await _provider.UploadPhotoAsync(assetId, stream, "expired-test.jpg");
            _blobsToCleanUp.Add(blobName);

            // Negative expiry hours => ExpiresOn is already in the past.
            var expiredSasUrl = await _provider.GetPhotoAccessKeyAsync(blobName, expiryHours: -1);

            var response = await _http.GetAsync(expiredSasUrl);

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }

        /// <summary>
        /// The 1-year expiry used by AttachPhoto is actually encoded as ~365 days out in the
        /// 'se' (signed expiry) query parameter.
        /// </summary>
        [TestMethod]
        public async Task BLOB08_GetPhotoAccessKeyAsync_OneYearExpiry_MatchesExpectedDate()
        {
            var assetId = Guid.NewGuid();
            using var stream = MakeFakeImageStream();
            var blobName = await _provider.UploadPhotoAsync(assetId, stream, "one-year-test.jpg");
            _blobsToCleanUp.Add(blobName);

            var beforeCall = DateTimeOffset.UtcNow;
            var sasUrl = await _provider.GetPhotoAccessKeyAsync(blobName, expiryHours: 24 * 365);

            var seValue = GetQueryParam(sasUrl, "se");
            Assert.IsNotNull(seValue, "SAS URL must include an 'se' (expiry) parameter.");

            var actualExpiry = DateTimeOffset.Parse(seValue!);
            var expectedExpiry = beforeCall.AddHours(24 * 365);

            // Allow a small tolerance for test execution time.
            var difference = (actualExpiry - expectedExpiry).Duration();
            Assert.IsTrue(difference < TimeSpan.FromMinutes(5),
                $"Expected expiry near {expectedExpiry}, got {actualExpiry}.");
        }

        #endregion
    }
}

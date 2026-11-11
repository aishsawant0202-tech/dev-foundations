#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using ReminderAgent.Common;

namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// Implements IPhotoStorageProvider using Azure Blob Storage.
    /// Photos are stored in the container named by MyConfig.PhotosContainer.
    /// </summary>
    public class AzureBlobPhotoStorageProvider : IPhotoStorageProvider
    {
        #region Fields

        private readonly BlobContainerClient _containerClient;

        #endregion

        #region Constructor

        public AzureBlobPhotoStorageProvider(IConfiguration configuration, MyConfig? config = null)
        {
            config ??= new MyConfig();

            var connectionString = configuration
                .GetConnectionString("BlobStorage")
                ?? throw new InvalidOperationException(
                    "Connection string 'BlobStorage' not found in appsettings.json");

            _containerClient = new BlobContainerClient(
                connectionString, config.PhotosContainer);
        }

        #endregion

        #region Photo Operations

        /// <summary>
        /// Uploads a photo to the 'photos' Blob container.
        /// assetId is used to logically group photos per asset.
        /// Returns unique BlobName (GUID + extension) to store in SQL.
        /// </summary>
        public async Task<string> UploadPhotoAsync(
            Guid assetId,
            Stream photoStream,
            string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var blobName  = $"{Guid.NewGuid()}{extension}";

            var blobClient = _containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(photoStream, new BlobHttpHeaders
            {
                ContentType = GetContentType(extension)
            });

            return blobName;
        }

        /// <summary>
        /// Downloads a photo from Blob Storage by BlobName.
        /// Returns photo as a Stream.
        /// </summary>
        public async Task<Stream> DownloadPhotoAsync(string blobName)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            var response   = await blobClient.DownloadAsync();
            return response.Value.Content;
        }

        /// <summary>
        /// Deletes a photo blob by BlobName from the 'photos' container.
        /// The Photos SQL row is deleted separately by IDataAccessLayer.
        /// </summary>
        public async Task DeletePhotoAsync(string blobName)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        /// <summary>
        /// Generates a time-limited SAS URL for secure photo access.
        /// expiryHours controls how long the link stays valid (default 1 hour).
        /// </summary>
        public Task<string> GetPhotoAccessKeyAsync(
            string blobName,
            int expiryHours = 1)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            var sasUri = blobClient.GenerateSasUri(
                BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddHours(expiryHours));

            return Task.FromResult(sasUri.ToString());
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Maps file extension to MIME content type.
        /// </summary>
        private static string GetContentType(string extension) => extension switch
        {
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".gif"  => "image/gif",
            ".webp" => "image/webp",
            _       => "application/octet-stream"
        };

        #endregion
    }
}
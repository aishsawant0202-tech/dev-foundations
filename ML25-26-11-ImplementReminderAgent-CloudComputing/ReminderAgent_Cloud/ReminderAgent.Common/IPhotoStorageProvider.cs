using System;
using System.IO;
using System.Threading.Tasks;

namespace ReminderAgent.Common
{
    /// <summary>
    /// Defines photo storage operations backed by Azure Blob Storage.
    /// </summary>
    public interface IPhotoStorageProvider
    {
        #region Photo Operations

        /// <summary>
        /// Uploads a photo to Blob Storage and records it in the Photos SQL table.
        /// Returns the permanent Blob URL.
        /// </summary>
        Task<string> UploadPhotoAsync(Guid assetId, Stream photoStream, string fileName);

        /// <summary>Deletes the photo from Blob Storage AND removes its row from the Photos SQL table.</summary>
        Task DeletePhotoAsync(string blobName);

        /// <summary>
        /// Generates a time-limited SAS URL for secure photo access.
        /// expiryHours controls how long the link stays valid.
        /// </summary>
        Task<string> GetPhotoAccessKeyAsync(string blobName, int expiryHours = 1);

        /// <summary>Downloads a photo from Blob Storage by BlobName, as a readable Stream.</summary>
        Task<Stream> DownloadPhotoAsync(string blobName);

        #endregion
    }
}

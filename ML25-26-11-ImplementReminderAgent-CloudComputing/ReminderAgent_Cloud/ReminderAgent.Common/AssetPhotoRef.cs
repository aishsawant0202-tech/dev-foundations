#nullable enable

namespace ReminderAgent.Common
{
    /// <summary>
    /// A lightweight read-only projection of a Photos row, returned by
    /// GetPhotoRefsForAssetAsync. Does not include AccessKey, since that's only relevant
    /// when IPhotoStorageProvider generates a fresh SAS URL.
    /// </summary>
    public class AssetPhotoRef
    {
        #region Properties

        public Guid Id { get; set; }
        public string BlobUrl { get; set; } = string.Empty;
        public string? AccessKey { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public DateTime UploadedAt { get; set; }

        #endregion
    }
}

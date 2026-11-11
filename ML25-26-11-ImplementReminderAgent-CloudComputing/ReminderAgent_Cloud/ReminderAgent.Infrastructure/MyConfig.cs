using System;

namespace ReminderAgent.Infrastructure
{
    /// <summary>
    /// This project's cloud configuration: the Azure resource names (container, queue, table)
    /// ReminderAgent talks to. Registered as a DI singleton and injected into
    /// AzureBlobPhotoStorageProvider, AzureTableStatusService, BulkUploadWorker, and
    /// ReminderTools, so each resource name is defined once here instead of being hardcoded per
    /// class. Connection strings are NOT here — they're secrets, read directly from
    /// IConfiguration's standard ConnectionStrings:* section (via GetConnectionString) by each
    /// class that needs one, which is also what Azure App Service's own "Connection strings"
    /// blade binds into. Keeping them out of this class avoids the same secret ever needing to
    /// be duplicated into two different config keys.
    /// </summary>
    public class MyConfig
    {
        #region Azure Resource Names

        /// <summary>
        /// The blob container where attached reminder photos are stored.
        /// </summary>
        public string PhotosContainer { get; set; } = "photos";

        /// <summary>
        /// The queue that BulkUploadAssets enqueues jobs to, and that the
        /// background bulk-upload worker (ReminderAgent.Cloud) polls.
        /// </summary>
        public string BulkUploadQueue { get; set; } = "bulk-upload-queue";

        /// <summary>
        /// Where BulkUploadWorker moves a message after it fails more than
        /// its MaxRetries attempts, instead of retrying it forever.
        /// </summary>
        public string BulkUploadPoisonQueue { get; set; } = "bulk-upload-queue-poison";

        /// <summary>
        /// The table that bulk-upload job/row status is written to.
        /// </summary>
        public string BulkUploadStatusTable { get; set; } = "BulkUploadStatus";

        /// <summary>
        /// The table that per-tool-call activity (ToolName, Detail, Success, Timestamp)
        /// is logged to — what GetRecentActivity reads from.
        /// </summary>
        public string ActivityLogTable { get; set; } = "ActivityLog";

        /// <summary>
        /// Set here the name of your group.
        /// </summary>
        public string GroupId { get; set; } = "TechTrio-ReminderAgent";

        #endregion
    }
}

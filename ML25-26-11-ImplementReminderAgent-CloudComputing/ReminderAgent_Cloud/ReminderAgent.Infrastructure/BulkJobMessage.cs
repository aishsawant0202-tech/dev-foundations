namespace ReminderAgent.Infrastructure;

/// <summary>Queue message shape for 'bulk-upload-queue', deserialized by <see cref="BulkUploadWorker"/>.</summary>
internal record BulkJobMessage(string JobId, string BlobName);

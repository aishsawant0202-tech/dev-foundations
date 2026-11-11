using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReminderAgent.Common;

namespace ReminderAgent.Infrastructure;

/// <summary>
/// Polls 'bulk-upload-queue' and imports each queued file's rows into Azure SQL.
/// </summary>
/// <remarks>
/// Shared by ReminderAgent.Cloud and ReminderAgent.McpServer, started as a background
/// task by both, so a queued job gets processed regardless of which one queued it.
/// Messages past <see cref="MaxRetries"/> attempts go to 'bulk-upload-queue-poison'.
/// Status is written to Table Storage (BulkUploadStatus) for GetBulkUploadStatus.
/// </remarks>
public class BulkUploadWorker
{
    #region Fields

    private const int MaxRetries = 3;

    private readonly IQueueService _queue;
    private readonly IPhotoStorageProvider _photoStorage;
    private readonly ITableStatusService _tableStatus;
    private readonly IDataAccessLayer _dal;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<BulkUploadWorker> _logger;
    private readonly MyConfig _config;

    #endregion

    #region Constructor

    public BulkUploadWorker(
        IQueueService queue,
        IPhotoStorageProvider photoStorage,
        ITableStatusService tableStatus,
        IDataAccessLayer dal,
        IEmbeddingService embeddingService,
        ILogger<BulkUploadWorker> logger,
        MyConfig? config = null)
    {
        _queue = queue;
        _photoStorage = photoStorage;
        _tableStatus = tableStatus;
        _dal = dal;
        _embeddingService = embeddingService;
        _logger = logger;
        _config = config ?? new MyConfig();
    }

    #endregion

    #region Public API

    /// <summary>Runs the poll loop until cancelled. Never returns on its own.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bulk-upload queue worker started, polling 'bulk-upload-queue'...");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _queue.ReceiveMessagesAsync(_config.BulkUploadQueue);
                foreach (var (id, popReceipt, body, dequeueCount) in messages)
                {
                    if (dequeueCount > MaxRetries)
                    {
                        _logger.LogWarning("Message {MessageId} failed {Count} times, moving to poison queue.",
                            id, dequeueCount);
                        await _queue.SendMessageAsync(_config.BulkUploadPoisonQueue, body);
                        await _queue.DeleteMessageAsync(_config.BulkUploadQueue, id, popReceipt);
                        continue;
                    }

                    _logger.LogInformation("Dequeued bulk-upload message: {Body} (attempt {Attempt})",
                        body, dequeueCount);

                    var job = JsonSerializer.Deserialize<BulkJobMessage>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                    await using var blobStream = await _photoStorage.DownloadPhotoAsync(job.BlobName);
                    var tempPath = Path.Combine(Path.GetTempPath(), job.BlobName);
                    await using (var fileStream = File.Create(tempPath))
                        await blobStream.CopyToAsync(fileStream, cancellationToken);

                    var rows = BulkFileParser.ParseFile(tempPath);
                    // Loaded once per job, kept updated below — catches duplicates
                    // within this same file too, not just ones already in the DB.
                    var existingAssets = (await _dal.GetAllAssetsAsync()).ToList();
                    int success = 0, failed = 0, skipped = 0, rowNum = 0;
                    foreach (var row in rows)
                    {
                        rowNum++;
                        try
                        {
                            // Same date parsing CreateAsset uses (via TimelineHelpers).
                            var parsedEventDate = TimelineHelpers.ParseEventDate(row.EventDate);
                            if (parsedEventDate is null && !string.IsNullOrWhiteSpace(row.EventDate))
                            {
                                _logger.LogWarning(
                                    "Row {RowNum} ('{Name}'): could not parse eventDate '{EventDate}' — saving without a date.",
                                    rowNum, row.Name, row.EventDate);
                            }

                            // Same Name/Category normalization CreateAsset uses, so an asset
                            // saved via bulk upload is stored identically to one saved via chat.
                            var asset = new Asset
                            {
                                Name = row.Name.Trim(),
                                Category = row.Category.Trim().ToLowerInvariant(),
                                City = string.IsNullOrWhiteSpace(row.City) ? null : row.City,
                                Country = string.IsNullOrWhiteSpace(row.Country) ? null : row.Country,
                                Tags = row.Tags,
                                UserExperience = row.UserExperience,
                                EventDate = parsedEventDate,
                                TimelineState = TimelineHelpers.ComputeTimelineState(parsedEventDate)
                            };

                            // Same duplicate check CreateAsset uses (via DuplicateAssetHelpers):
                            // same EventDate = skip, different EventDate = update in place.
                            var existingMatch = DuplicateAssetHelpers.FindMatch(existingAssets, asset.Name, asset.Category);
                            if (existingMatch is not null && DuplicateAssetHelpers.IsExactDuplicate(existingMatch, parsedEventDate))
                            {
                                await _tableStatus.WriteRowStatusAsync(job.JobId, rowNum, true, row.Name, null, skipped: true);
                                skipped++;
                                continue;
                            }

                            Guid assetId;
                            if (existingMatch is not null)
                            {
                                asset = DuplicateAssetHelpers.MergeForUpdate(asset, existingMatch);
                                await _dal.UpdateAssetAsync(asset);
                                assetId = asset.Id;
                                existingAssets.Remove(existingMatch);
                            }
                            else
                            {
                                // Fills in asset.Id and returns it.
                                assetId = await _dal.CreateAssetAsync(asset);
                            }
                            existingAssets.Add(asset);

                            // 5-field embedding, matching CreateAsset's call (no userInput for bulk rows).
                            var vector = await _embeddingService.GenerateEmbeddingAsync(
                                asset.Name, asset.Category, asset.UserExperience ?? "",
                                userInput: string.Empty, tags: asset.Tags ?? "");
                            await _dal.SaveEmbeddingAsync(assetId, vector);
                            await _tableStatus.WriteRowStatusAsync(job.JobId, rowNum, true, row.Name, null);
                            success++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to insert row '{Name}'", row.Name);
                            await _tableStatus.WriteRowStatusAsync(job.JobId, rowNum, false, row.Name, ex.Message);
                            failed++;
                        }
                    }
                    _logger.LogInformation("Job {JobId} done: {Success} succeeded, {Skipped} skipped, {Failed} failed.",
                        job.JobId, success, skipped, failed);
                    await _tableStatus.WriteJobSummaryAsync(job.JobId, rows.Count, success, failed, skipped);

                    File.Delete(tempPath);
                    await _queue.DeleteMessageAsync(_config.BulkUploadQueue, id, popReceipt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling bulk-upload-queue.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    #endregion
}

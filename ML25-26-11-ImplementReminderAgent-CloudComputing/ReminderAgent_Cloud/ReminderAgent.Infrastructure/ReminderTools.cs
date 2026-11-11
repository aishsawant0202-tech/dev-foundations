using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ReminderAgent.Common;

namespace ReminderAgent.Infrastructure;

/// <summary>
/// The ReminderAgent tools. Exposed both to an embedded OpenAI chat client (via
/// AIFunctionFactory) and as real MCP tools ([McpServerTool] below, reflected over by
/// the MCP SDK) — both paths share the same [Description] text. Every call also logs an
/// entry to Table Storage (ActivityLog) via ITableStatusService.
/// </summary>
[McpServerToolType]
public class ReminderTools
{
    #region Fields

    private readonly IDataAccessLayer _dal;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISimilarityService _similarityService;
    private readonly IPhotoStorageProvider _photoStorage;
    private readonly IQueueService _queueService;
    private readonly ITableStatusService _tableStatusService;
    private readonly ILogger<ReminderTools> _logger;
    private readonly MyConfig _config;

    private static readonly string[] AllowedPhotoExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    #endregion

    #region Properties

    /// <summary>
    /// The current user turn's exact text, set by the chat REPL host (Program.cs).
    /// CreateAsset falls back to this when the model omits its own <c>userInput</c>
    /// argument, so the user's literal wording stays searchable either way. MCP callers
    /// have no REPL turn and always pass userInput explicitly, so this stays null for them.
    /// </summary>
    public string? AmbientUserInput { get; set; }

    #endregion

    #region Constructor

    public ReminderTools(
        IDataAccessLayer dal,
        IEmbeddingService embeddingService,
        ISimilarityService similarityService,
        IPhotoStorageProvider photoStorage,
        IQueueService queueService,
        ITableStatusService tableStatusService,
        ILogger<ReminderTools> logger,
        MyConfig? config = null)
    {
        _dal = dal;
        _embeddingService = embeddingService;
        _similarityService = similarityService;
        _photoStorage = photoStorage;
        _queueService = queueService;
        _tableStatusService = tableStatusService;
        _logger = logger;
        _config = config ?? new MyConfig();
    }

    #endregion

    #region Tool 1: CreateAsset

    /// <summary>
    /// Tool 1: Creates and saves a new asset.
    /// </summary>
    [McpServerTool]
    [Description("Creates and saves a new personal asset (restaurant, book, hotel, " +
                 "contact, destination, etc.) that the user wants to remember. " +
                 "Use this whenever the user wants to save, store, remember, or note something.")]
    public async Task<string> CreateAsset(
        [Description("Name of the asset, e.g. 'Trattoria Roma' or 'Clean Code'")] string name,
        [Description("Category of the asset: restaurant, book, hotel, contact, destination, or other")] string category,
        [Description("Semicolon-separated tags describing the asset, e.g. 'Italian;Cosy;Frankfurt'")] string? tags = null,
        [Description("City where the asset is located, if applicable")] string? city = null,
        [Description("Country where the asset is located, if applicable. If the user only gave a " +
                      "city, infer the country yourself from general knowledge instead of leaving " +
                      "this blank, unless the place name is genuinely ambiguous")] string? country = null,
        [Description("Region of the country, if the user mentioned one or it can be confidently " +
                      "inferred from the city")] string? region = null,
        [Description("Date of the event or visit in dd-MM-yyyy format, if the user mentioned one")] string? eventDate = null,
        [Description("The user's experience, opinion or notes about the asset")] string? userExperience = null,
        [Description("The original user message that triggered this save")] string? userInput = null)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(CreateAsset));
        _logger.LogInformation("CreateAsset raw eventDate argument: '{EventDate}'", eventDate);

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("CreateAsset rejected: missing name.");
            await _tableStatusService.LogActivityAsync(nameof(CreateAsset), "Rejected: missing name", success: false);
            return "I need a name for the asset before I can save it.";
        }
        if (string.IsNullOrWhiteSpace(category))
        {
            _logger.LogWarning("CreateAsset rejected: missing category for '{Name}'.", name);
            await _tableStatusService.LogActivityAsync(nameof(CreateAsset), $"Rejected: missing category for '{name}'", success: false);
            return "I need a category (restaurant, book, hotel, contact, destination, ...) before I can save it.";
        }

        // Shared with BulkUploadWorker via TimelineHelpers — same date parsing either way.
        DateTime? parsedEventDate = TimelineHelpers.ParseEventDate(eventDate);
        if (parsedEventDate is null && !string.IsNullOrWhiteSpace(eventDate))
        {
            _logger.LogWarning(
                "CreateAsset could not parse eventDate '{EventDate}' for '{Name}' — saving without a date.",
                eventDate, name);
        }

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Category = category.Trim().ToLowerInvariant(),
            Tags = tags,
            TimelineState = ComputeTimelineState(parsedEventDate),
            EventDate = parsedEventDate,
            CreatedAt = DateTime.UtcNow,
            // Prefer what the model passed explicitly; fall back to
            // the REPL's captured raw turn so the user's literal wording — and its keywords —
            // is never silently dropped just because the model omitted the argument.
            UserInput = string.IsNullOrWhiteSpace(userInput) ? AmbientUserInput : userInput,
            UserExperience = userExperience,
            City = city,
            Country = country,
            Region = region
        };

        // Same duplicate check BulkUploadWorker uses (via DuplicateAssetHelpers):
        // same EventDate = skip, different EventDate = update in place.
        var existingMatch = DuplicateAssetHelpers.FindMatch(
            await _dal.GetAllAssetsAsync(), asset.Name, asset.Category);

        Guid assetId;
        bool isUpdate = false;
        if (existingMatch is not null && DuplicateAssetHelpers.IsExactDuplicate(existingMatch, parsedEventDate))
        {
            _logger.LogInformation("CreateAsset: skipped duplicate '{Name}' ({Category}) — already saved.",
                asset.Name, asset.Category);
            await _tableStatusService.LogActivityAsync(nameof(CreateAsset),
                $"Skipped duplicate '{asset.Name}' ({asset.Category})", success: true);
            return $"You already have '{existingMatch.Name}' ({existingMatch.Category}) saved" +
                   (existingMatch.EventDate is not null ? $" for {existingMatch.EventDate:dd-MM-yyyy}" : "") +
                   " — nothing new to add.";
        }
        else if (existingMatch is not null)
        {
            asset = DuplicateAssetHelpers.MergeForUpdate(asset, existingMatch);
            await _dal.UpdateAssetAsync(asset);
            assetId = asset.Id;
            isUpdate = true;
        }
        else
        {
            // CreateAssetAsync returns the generated Id — use it for the embedding
            // write so both rows are guaranteed to reference the same key.
            assetId = await _dal.CreateAssetAsync(asset);
        }

        try
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(
                asset.Name,
                asset.Category,
                asset.UserExperience ?? string.Empty,
                asset.UserInput ?? string.Empty,
                asset.Tags ?? string.Empty);

            await _dal.SaveEmbeddingAsync(assetId, vector);
        }
        catch (Exception ex)
        {
            // Embedding generation/save failed after the row was already written (e.g. a
            // transient OpenAI failure). For a fresh insert, roll it back so a retry with
            // the same name/category creates a clean row instead of silently landing on
            // the duplicate-skip branch above and never getting an embedding. An update
            // leaves the existing row's merged fields in place — only the embedding
            // refresh is lost, which is safe to retry on its own.
            if (!isUpdate)
            {
                _logger.LogWarning(ex,
                    "CreateAsset: embedding failed for new asset {AssetId} '{Name}' — rolling back the insert.",
                    assetId, asset.Name);
                try { await _dal.DeleteAssetAsync(assetId); } catch { /* best-effort rollback */ }
            }
            else
            {
                _logger.LogWarning(ex,
                    "CreateAsset: embedding failed while updating asset {AssetId} '{Name}' — row updated, embedding stale.",
                    assetId, asset.Name);
            }
            await _tableStatusService.LogActivityAsync(nameof(CreateAsset),
                $"Embedding failed for '{asset.Name}' ({asset.Category})", success: false);
            throw;
        }

        _logger.LogInformation("{Action} asset {AssetId} '{Name}' ({Category}).",
            isUpdate ? "Updated" : "Created", assetId, asset.Name, asset.Category);
        await _tableStatusService.LogActivityAsync(nameof(CreateAsset),
            $"{(isUpdate ? "Updated" : "Created")} '{asset.Name}' ({asset.Category})", success: true);

        return $"{(isUpdate ? "Updated" : "Saved")} '{asset.Name}' ({asset.Category})" +
               (city is not null ? $" in {city}" : "") +
               (parsedEventDate is not null ? $" for {parsedEventDate:dd-MM-yyyy}" : "") +
               ".";
    }

    #endregion

    #region Tool 2: SearchAssets

    /// <summary>
    /// Tool 2: The single read/find entry point (replaces the old SearchAssets/ListAssets/
    /// GetReminders/SearchRemindersSemantic). No query lists/filters; query with
    /// semantic=false ranks by keyword (auto-falls back to semantic on 0 hits); semantic=true
    /// ranks by embedding similarity.
    /// </summary>
    [McpServerTool]
    [Description("Searches, lists, or filters the user's saved assets — the single tool for finding " +
                 "or browsing what they've saved, including anything with a date: plans, schedule, " +
                 "agenda, events, appointments, what's on/coming up/happening. Combine any of: " +
                 "free-text query, category, city, country, timeline (Past/Present/Future/All), " +
                 "onDate, and withinDays filters. Leave query empty to just list/filter, e.g. 'show " +
                 "me all my restaurants' or 'what's coming up'. For date-specific asks ('what are my " +
                 "plans tomorrow', 'anything next Monday', 'what's on this week'), resolve the date " +
                 "yourself and use onDate/withinDays — do NOT put the relative date phrase itself in " +
                 "query, since the stored data holds an actual date, never words like 'tomorrow', so " +
                 "a keyword search for that word will always find nothing. Set semantic=true for " +
                 "vague/conceptual queries where exact keywords won't match, e.g. 'that cozy place " +
                 "with the great pasta' or 'something to read on vacation'.")]
    public async Task<string> SearchAssets(
        [Description("Free-text search query. Leave empty to just list/filter without ranking. Do not " +
                     "put a relative date phrase here (e.g. 'tomorrow') — use onDate/withinDays instead.")] string query = "",
        [Description("Optional category filter: restaurant, book, hotel, contact, destination")] string? category = null,
        [Description("Optional city filter")] string? city = null,
        [Description("Optional country filter")] string? country = null,
        [Description("Optional timeline filter: Past, Present, Future, or All (default)")] string timeline = "All",
        [Description("Filter to assets whose event date is exactly this day, in dd-MM-yyyy format. " +
                     "Resolve relative phrases ('tomorrow', 'next Monday') to an absolute date yourself " +
                     "before passing it here.")] string? onDate = null,
        [Description("Filter to assets whose event date falls within the next N days from today " +
                     "(inclusive), e.g. 7 for 'this week'. Use instead of onDate for date ranges.")] int? withinDays = null,
        [Description("True for meaning-based (embedding) search instead of keyword matching")] bool semantic = false,
        [Description("Max results to return when semantic=true")] int topK = 5)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(SearchAssets));

        var assets = (await _dal.GetAllAssetsAsync()).ToList();

        // Structured pre-filters first (cheap): category/city/country, then timeline/date.
        IEnumerable<Asset> filtered = assets;
        if (!string.IsNullOrWhiteSpace(category))
            filtered = filtered.Where(a =>
                a.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(city))
            filtered = filtered.Where(a =>
                city.Trim().Equals(a.City, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(country))
            filtered = filtered.Where(a =>
                country.Trim().Equals(a.Country, StringComparison.OrdinalIgnoreCase));

        bool timelineRequested = !timeline.Equals("All", StringComparison.OrdinalIgnoreCase);
        if (timelineRequested)
            filtered = filtered.Where(a =>
                ComputeTimelineState(a.EventDate).Equals(timeline.Trim(), StringComparison.OrdinalIgnoreCase));

        bool onDateRequested = !string.IsNullOrWhiteSpace(onDate);
        if (onDateRequested)
        {
            // Same flexible parser as the query-fallback below (not just the exact dd-MM-yyyy
            // we ask for) — callers don't reliably send the requested format even for a
            // dedicated parameter, e.g. ISO yyyy-MM-dd instead.
            if (TryParseFlexibleDate(onDate!, out var exactDate))
            {
                filtered = filtered.Where(a => a.EventDate?.Date == exactDate.Date);
            }
            else
            {
                _logger.LogWarning("SearchAssets could not parse onDate '{OnDate}' — ignoring that filter.", onDate);
                onDateRequested = false;
            }
        }

        bool withinDaysRequested = withinDays is not null;
        if (withinDaysRequested)
        {
            var today = DateTime.UtcNow.Date;
            var horizon = today.AddDays(Math.Max(0, withinDays!.Value));
            filtered = filtered.Where(a =>
                a.EventDate is not null && a.EventDate.Value.Date >= today && a.EventDate.Value.Date <= horizon);
        }

        var candidates = filtered.ToList();
        var noOtherFilters = !timelineRequested && !onDateRequested && !withinDaysRequested &&
                              category is null && city is null && country is null;

        if (candidates.Count == 0)
        {
            var reason = noOtherFilters ? "You have no saved reminders yet." : "No saved assets match those filters.";
            await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Query '{query}' matched 0 assets", success: true);
            return reason;
        }

        // LLM callers sometimes put a resolved date in free-text query instead of onDate
        // (e.g. query="15-08-2026"), which keyword/semantic matching can never find. Detect
        // a query that's just a date and match on EventDate directly instead of returning
        // a false "no results".
        if (!string.IsNullOrWhiteSpace(query) && TryParseFlexibleDate(query, out var queryDate))
        {
            var dateMatches = candidates.Where(a => a.EventDate?.Date == queryDate.Date).ToList();
            if (dateMatches.Count > 0)
            {
                _logger.LogInformation(
                    "SearchAssets query '{Query}' looked like a date — matched {Count} assets by EventDate.",
                    query, dateMatches.Count);
                await _tableStatusService.LogActivityAsync(nameof(SearchAssets),
                    $"Query '{query}' interpreted as date filter, matched {dateMatches.Count} assets", success: true);
                return FormatAssetList(dateMatches);
            }
        }

        // Semantic ranking, requested explicitly or as an automatic fallback below. If the
        // embedding call itself fails (OpenAI rate limit, network blip, etc.), sets
        // semanticUnavailable so the caller falls back to keyword search below instead of
        // surfacing a raw error.
        bool semanticUnavailable = false;
        async Task<string?> TrySemanticAsync()
        {
            if (semanticUnavailable) return null;

            var embeddings = await _dal.GetAllEmbeddingsAsync();
            if (embeddings.Count == 0) return null;

            foreach (var asset in candidates)
                if (embeddings.TryGetValue(asset.Id, out var vec)) asset.Embedding = vec;

            var scorable = candidates.Where(a => a.Embedding is not null).ToList();
            if (scorable.Count == 0) return null;

            float[] queryVector;
            try
            {
                queryVector = await _embeddingService.GenerateQueryEmbeddingAsync(query);
            }
            catch (Exception ex)
            {
                semanticUnavailable = true;
                _logger.LogWarning(ex,
                    "SearchAssets: embedding call failed for '{Query}' — falling back to keyword search.", query);
                return null;
            }

            var top = await _similarityService.GetTopKSimilarAsync(queryVector, scorable, topK: Math.Max(1, topK));
            if (top.Count == 0) return null;

            var sb = new StringBuilder("Closest matches:\n");
            foreach (var asset in top)
                sb.AppendLine("  " + FormatAssetLine(asset));
            return sb.ToString().TrimEnd();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Filter-only search matched {candidates.Count} assets", success: true);

            // No query and no filters at all: an "overview" ask ("what have I got?"),
            // most useful grouped by timeline rather than as one flat dump.
            if (noOtherFilters)
            {
                var sb = new StringBuilder();
                foreach (var group in candidates.GroupBy(a => ComputeTimelineState(a.EventDate))
                             .OrderBy(g => TimelineOrder(g.Key)))
                {
                    sb.AppendLine($"{group.Key}:");
                    foreach (var asset in group)
                        sb.AppendLine("  " + FormatAssetLine(asset));
                }
                return sb.ToString().TrimEnd();
            }

            return FormatAssetList(candidates);
        }

        if (semantic)
        {
            var semanticResult = await TrySemanticAsync();
            if (semanticResult is not null)
            {
                await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Semantic query '{query}' matched {topK} assets", success: true);
                return semanticResult;
            }
            if (!semanticUnavailable)
            {
                await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Semantic query '{query}' matched 0 assets", success: true);
                return "No saved assets were similar enough to that query.";
            }
            // The embedding call itself failed — fall through to the keyword ranker below
            // instead of returning a raw error to the caller.
            await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Semantic query '{query}' unavailable, used keyword fallback", success: true);
        }

        // Rank by how many significant query terms appear across the asset's text
        // fields — stopwords/short filler words are excluded so common words like
        // "to" or "in" don't produce false-positive substring matches.
        var terms = ExtractSignificantTerms(query);
        var matched = terms.Length == 0
            ? candidates // query was only stopwords/filler — fall back to the filtered list
            : candidates
                .Select(a => (Asset: a, Score: CountMatchingTerms(a, terms)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Asset)
                .ToList();

        if (matched.Count == 0)
        {
            // Keyword matching found nothing concrete — the query is likely
            // conceptual ("something cozy") rather than misspelled, so try
            // semantic search over the same filtered candidates before giving up.
            var fallback = await TrySemanticAsync();
            if (fallback is not null)
            {
                await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Query '{query}' matched 0 keyword hits, used semantic fallback", success: true);
                return fallback;
            }
        }

        _logger.LogInformation("SearchAssets '{Query}' (category={Category}, city={City}, country={Country}, timeline={Timeline}) matched {ResultCount} assets.",
            query, category, city, country, timeline, matched.Count);
        await _tableStatusService.LogActivityAsync(nameof(SearchAssets), $"Query '{query}' matched {matched.Count} assets", success: true);

        return matched.Count == 0
            ? "No saved assets match that query."
            : FormatAssetList(matched);
    }

    #endregion

    #region Tool 3: AttachPhoto

    /// <summary>
    /// Tool 3: Uploads a photo to Azure Blob Storage via <see cref="IPhotoStorageProvider"/>,
    /// generates a 1-year SAS token, and persists the reference in the Photos SQL table via
    /// <see cref="IDataAccessLayer"/>.
    ///
    /// Accepts either photoPath (a path this server process can read directly — only
    /// meaningful when caller and server share a filesystem, e.g. ReminderAgent.Cloud or
    /// a local MCP client against a locally-running McpServer) or photoContentBase64 +
    /// fileName (Base64-encoded image bytes — works from any remote client, including a
    /// photo attached directly in a chat, once the deployed server has no filesystem in
    /// common with the caller). Exactly one of photoPath / photoContentBase64 is required.
    /// </summary>
    [McpServerTool]
    [Description("Attaches a photo to a saved asset. " +
                 "Use this when the user wants to add, upload, or attach an image to an asset. " +
                 "Provide exactly ONE of: photoPath (only if you and this server share a " +
                 "filesystem, e.g. local dev) or photoContentBase64 + fileName (Base64-encoded " +
                 "image bytes — the normal choice for a remote chat client or a photo attached " +
                 "in this conversation).")]
    public async Task<string> AttachPhoto(
        [Description("Name of the asset to attach the photo to")] string assetName,
        [Description("File path readable by THIS SERVER PROCESS directly. Only use when you " +
                     "know the server and the file are on the same machine (e.g. local dev). " +
                     "Leave empty otherwise.")] string? photoPath = null,
        [Description("Base64-encoded bytes of the photo. Use this for a photo attached in the " +
                     "current chat, or any remote client. Requires fileName.")] string? photoContentBase64 = null,
        [Description("Original file name with extension (.jpg, .jpeg, .png, .gif, or .webp). " +
                     "Required when using photoContentBase64; ignored with photoPath.")] string? fileName = null)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(AttachPhoto));

        if (string.IsNullOrWhiteSpace(assetName))
            return "I need the name of the asset to attach a photo to.";

        var suppliedCount = new[] { photoPath, photoContentBase64 }.Count(v => !string.IsNullOrWhiteSpace(v));
        if (suppliedCount == 0)
        {
            return "Provide the photo either as photoPath (only if the server can read it " +
                   "directly) or photoContentBase64 + fileName (for a remote client or an " +
                   "attached photo).";
        }
        if (suppliedCount > 1)
        {
            return "Provide only one of photoPath or photoContentBase64 — not both.";
        }

        byte[] photoBytes;
        string extension;
        string effectiveFileName;

        if (!string.IsNullOrWhiteSpace(photoPath))
        {
            // 1. Validate extension before touching disk or Blob Storage.
            extension = Path.GetExtension(photoPath).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(extension))
            {
                _logger.LogWarning("AttachPhoto rejected: unsupported extension '{Extension}' for '{PhotoPath}'.",
                    extension, photoPath);
                await _tableStatusService.LogActivityAsync(nameof(AttachPhoto), $"Rejected: unsupported extension '{extension}'", success: false);
                return $"'{extension}' isn't a supported photo format. " +
                       "Please use jpg, jpeg, png, gif, or webp.";
            }

            if (!File.Exists(photoPath))
            {
                _logger.LogWarning("AttachPhoto rejected: file not found at '{PhotoPath}'.", photoPath);
                await _tableStatusService.LogActivityAsync(nameof(AttachPhoto), $"Rejected: file not found '{photoPath}'", success: false);
                return $"I couldn't find a file at '{photoPath}'. If this server is deployed " +
                       "remotely, photoPath won't work — use photoContentBase64 instead.";
            }

            effectiveFileName = Path.GetFileName(photoPath);
            photoBytes = await File.ReadAllBytesAsync(photoPath);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "fileName is required alongside photoContentBase64 (e.g. 'trattoria.jpg').";
            }

            extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(extension))
            {
                await _tableStatusService.LogActivityAsync(nameof(AttachPhoto), $"Rejected: unsupported extension '{extension}'", success: false);
                return $"'{extension}' isn't a supported photo format. " +
                       "Please use jpg, jpeg, png, gif, or webp.";
            }

            try
            {
                photoBytes = Convert.FromBase64String(photoContentBase64!);
            }
            catch (FormatException)
            {
                return "photoContentBase64 doesn't look like valid Base64 — check the encoding.";
            }

            effectiveFileName = fileName;
        }

        var asset = await FindAssetByNameAsync(assetName);
        if (asset is null)
        {
            _logger.LogWarning("AttachPhoto rejected: no asset named '{AssetName}'.", assetName);
            await _tableStatusService.LogActivityAsync(nameof(AttachPhoto), $"Rejected: no asset named '{assetName}'", success: false);
            return $"I couldn't find a saved asset named '{assetName}'.";
        }

        // 2. Upload the raw bytes to Blob Storage. Returns the blob's name only.
        string blobName;
        using (var stream = new MemoryStream(photoBytes))
        {
            blobName = await _photoStorage.UploadPhotoAsync(asset.Id, stream, effectiveFileName);
        }

        // 3. Generate a SAS-secured URL, valid for 1 year (personal-asset use case).
        string fullSasUrl = await _photoStorage.GetPhotoAccessKeyAsync(blobName, expiryHours: 24 * 365);

        // 4. Split into base URL + token, per schema: BlobUrl (no token) / AccessKey (token).
        var parts = fullSasUrl.Split('?', 2);
        string blobUrl = parts[0];
        string accessKey = parts.Length > 1 ? parts[1] : string.Empty;

        // 5. Persist the reference row in SQL.
        await _dal.InsertPhotoAsync(Guid.NewGuid(), asset.Id, blobName, blobUrl, accessKey, effectiveFileName);

        _logger.LogInformation("Attached photo '{BlobName}' to asset {AssetId} '{AssetName}'.",
            blobName, asset.Id, asset.Name);
        await _tableStatusService.LogActivityAsync(nameof(AttachPhoto), $"Attached '{effectiveFileName}' to '{asset.Name}'", success: true);

        return $"Attached '{effectiveFileName}' to '{asset.Name}'. Photo URL: {blobUrl}";
    }

    #endregion

    #region Tool 4: GetAssetDetails

    /// <summary>
    /// Tool 4: Returns a single asset by name, including photo references. Omitting the
    /// name instead lists every asset that has at least one photo attached — the only tool
    /// that can truthfully answer that, since it's the only one that reads the Photos table.
    /// </summary>
    [McpServerTool]
    [Description("Gets the full details of a single saved asset by its name, including tags, " +
                 "city, event date, the user's notes, and any attached photos. Use this when the " +
                 "user asks about one specific saved item, e.g. 'tell me about Trattoria Roma'. " +
                 "Omit the name to instead list every saved asset that currently has a photo " +
                 "attached, e.g. 'which reminders have images attached?'")]
    public async Task<string> GetAssetDetails(
        [Description("Name of the asset to look up. Leave empty/omit to list all assets " +
                     "that have at least one photo attached.")]
        string? name = null)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(GetAssetDetails));

        if (string.IsNullOrWhiteSpace(name))
            return await ListAssetsWithPhotosAsync();

        var assets = (await _dal.GetAllAssetsAsync()).ToList();

        var asset = assets.FirstOrDefault(a =>
                        a.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? assets.FirstOrDefault(a =>
                        a.Name.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            await _tableStatusService.LogActivityAsync(nameof(GetAssetDetails), $"Not found: '{name}'", success: false);
            return $"I couldn't find a saved asset named '{name}'.";
        }

        await _tableStatusService.LogActivityAsync(nameof(GetAssetDetails), $"Details returned for '{asset.Name}'", success: true);

        var sb = new StringBuilder();
        sb.AppendLine($"Name: {asset.Name}");
        sb.AppendLine($"Category: {asset.Category}");
        if (!string.IsNullOrWhiteSpace(asset.Tags)) sb.AppendLine($"Tags: {asset.Tags}");
        if (!string.IsNullOrWhiteSpace(asset.City)) sb.AppendLine($"City: {asset.City}");
        if (!string.IsNullOrWhiteSpace(asset.Country)) sb.AppendLine($"Country: {asset.Country}");
        if (!string.IsNullOrWhiteSpace(asset.Region)) sb.AppendLine($"Region: {asset.Region}");
        sb.AppendLine($"Timeline: {ComputeTimelineState(asset.EventDate)}");
        if (asset.EventDate is not null) sb.AppendLine($"Event date: {asset.EventDate:dd-MM-yyyy}");
        sb.AppendLine($"Saved on: {asset.CreatedAt:dd-MM-yyyy}");
        if (!string.IsNullOrWhiteSpace(asset.UserExperience)) sb.AppendLine($"Notes: {asset.UserExperience}");

        // Photo metadata is read-only here; upload/delete arrive with Sprint 4.
        var photos = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
        if (photos.Count > 0)
        {
            sb.AppendLine("Photos:");
            foreach (var photo in photos)
            {
                var fullUrl = string.IsNullOrEmpty(photo.AccessKey)
                    ? photo.BlobUrl
                    : $"{photo.BlobUrl}?{photo.AccessKey}";
                sb.AppendLine($"  {photo.FileName ?? "(unnamed)"} — {fullUrl}");
            }

        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Backs <c>GetAssetDetails(name: null)</c> — the only grounded way to answer "which
    /// reminders have images attached?" since it's the only path that reads the Photos table
    /// instead of guessing from Name/Tags/Notes text.
    /// </summary>
    private async Task<string> ListAssetsWithPhotosAsync()
    {
        var idsWithPhotos = await _dal.GetAssetIdsWithPhotosAsync();
        if (idsWithPhotos.Count == 0)
        {
            await _tableStatusService.LogActivityAsync(nameof(GetAssetDetails), "No assets have photos attached", success: true);
            return "None of your saved reminders have a photo attached yet.";
        }

        var assets = (await _dal.GetAllAssetsAsync())
            .Where(a => idsWithPhotos.Contains(a.Id))
            .ToList();

        await _tableStatusService.LogActivityAsync(nameof(GetAssetDetails), $"{assets.Count} asset(s) have photos attached", success: true);

        var sb = new StringBuilder();
        foreach (var asset in assets)
        {
            var photos = (await _dal.GetPhotoRefsForAssetAsync(asset.Id)).ToList();
            sb.Append("- ").Append(FormatAssetLine(asset));
            var fileNames = photos.Select(p => p.FileName ?? "(unnamed)");
            sb.Append(photos.Count == 1 ? " — photo: " : " — photos: ").Append(string.Join(", ", fileNames));
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    #endregion

    #region Tool 5: DeleteImage

    /// <summary>
    /// Tool 5: Deletes a photo from both Blob Storage and the SQL Photos table. The LLM
    /// invokes this when the user says "delete photo" or "remove image".
    /// </summary>
    [McpServerTool]
    [Description("Deletes a photo from Blob Storage and removes it from " +
                 "the database. Use when the user says 'delete photo', " +
                 "'remove image', or 'delete image from [asset name]'. " +
                 "Pass either the asset name (e.g. 'Vapiano') or the photo GUID. " +
                 "If the asset has more than one photo, also pass fileName to say which one.")]
    public async Task<string> DeleteImage(
        [Description("The name of the asset whose photo should be deleted " +
                     "(e.g. 'Vapiano'), OR the photo GUID if known.")]
        string assetNameOrPhotoId,
        [Description("The file name of the specific photo to delete, e.g. 'trattoria.jpg'. " +
                     "Only needed when the asset has more than one photo; omit otherwise.")]
        string? fileName = null)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(DeleteImage));
        try
        {
            // ── Case 1: GUID provided directly ──────────────────────────
            if (Guid.TryParse(assetNameOrPhotoId, out var photoGuid))
            {
                await _dal.DeletePhotoAsync(photoGuid, _photoStorage);
                await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"Deleted photo {photoGuid}", success: true);
                return $"Deleted photo {photoGuid}.";
            }

            // ── Case 2: Asset name provided ─────────────────────────────
            // Find asset by name first (exact match, falling back to partial
            // match, same as every other name-based tool)
            var asset = await FindAssetByNameAsync(assetNameOrPhotoId);

            if (asset == null)
            {
                await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"No asset named '{assetNameOrPhotoId}'", success: false);
                return $"Sorry, I couldn't find a saved asset named '{assetNameOrPhotoId}'. " +
                       $"Please check the name and try again.";
            }

            // Get all photos for this asset
            var photos = await _dal.GetPhotoRefsForAssetAsync(asset.Id);
            var photoList = photos.ToList();

            if (!photoList.Any())
            {
                await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"No photos for '{asset.Name}'", success: false);
                return $"No photos found for '{asset.Name}'.";
            }

            // ── A specific file was named — delete only that one ────────
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var match = photoList.FirstOrDefault(p =>
                    string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"No photo named '{fileName}' for '{asset.Name}'", success: false);
                    return $"'{asset.Name}' has no photo named '{fileName}'.";
                }

                await _dal.DeletePhotoAsync(match.Id, _photoStorage);
                await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"Deleted photo '{fileName}' from '{asset.Name}'", success: true);
                return $"Deleted photo '{fileName}' from '{asset.Name}'.";
            }

            // ── No file named: only safe to auto-delete when there's ────
            // exactly one photo. With more than one, ask instead of guessing.
            if (photoList.Count > 1)
            {
                return $"'{asset.Name}' has {photoList.Count} photos. " +
                       $"Please specify which one to delete, e.g. by file name.";
            }

            var only = photoList[0];
            await _dal.DeletePhotoAsync(only.Id, _photoStorage);
            await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"Deleted photo from '{asset.Name}'", success: true);
            return string.IsNullOrEmpty(only.FileName)
                ? $"Deleted photo from '{asset.Name}'."
                : $"Deleted photo '{only.FileName}' from '{asset.Name}'.";
        }
        catch (Exception ex)
        {
            await _tableStatusService.LogActivityAsync(nameof(DeleteImage), $"Error: {ex.Message}", success: false);
            return $"Failed to delete photo: {ex.Message}";
        }
    }

    #endregion

    #region Tool 6: DeleteAsset

    /// <summary>
    /// Tool 6: Deletes an asset entirely, by name. Clears its photos (blobs + SQL rows) via
    /// <see cref="IDataAccessLayer.DeletePhotosByAssetNameAsync"/> first, so no blob is left
    /// orphaned in Blob Storage — a plain SQL cascade-delete would only remove the Photos
    /// rows, not the underlying blob files.
    /// </summary>
    [McpServerTool]
    [Description("Deletes a saved asset (reminder) entirely, including any attached photos. " +
                 "Use when the user says 'delete reminder', 'remove asset', or 'forget about " +
                 "[name]'. This cannot be undone.")]
    public async Task<string> DeleteAsset(
        [Description("Name of the asset to delete, e.g. 'Trattoria Roma'")] string name)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(DeleteAsset));

        if (string.IsNullOrWhiteSpace(name))
            return "I need the name of the asset to delete.";

        var asset = await FindAssetByNameAsync(name);
        if (asset is null)
        {
            await _tableStatusService.LogActivityAsync(nameof(DeleteAsset), $"No asset named '{name}'", success: false);
            return $"I couldn't find a saved asset named '{name}'.";
        }

        await _dal.DeletePhotosByAssetNameAsync(asset.Name, _photoStorage);
        await _dal.DeleteAssetAsync(asset.Id);

        _logger.LogInformation("Deleted asset {AssetId} '{Name}'.", asset.Id, asset.Name);
        await _tableStatusService.LogActivityAsync(nameof(DeleteAsset), $"Deleted '{asset.Name}'", success: true);

        return $"Deleted '{asset.Name}'.";
    }

    #endregion

    #region Tool 7: BulkUploadAssets

    /// <summary>
    /// Tool 7: Queues a large import (CSV, JSON, or ZIP) for background bulk processing.
    ///
    /// Accepts three mutually exclusive ways to supply the file, because "a file path"
    /// only means something when the caller and this server share a filesystem —
    /// true for ReminderAgent.Cloud (in-process) and for a local MCP client pointed at
    /// a locally-running McpServer, but never true for a remote client (Claude.ai web,
    /// ChatGPT, a phone app) talking to this server once it's deployed to Azure App
    /// Service. Exactly one of the three must be provided:
    ///   - filePath            — a path this server process can read directly. Only
    ///                           works when server and caller are the same machine.
    ///   - fileContentText     — the raw visible text of a .csv or .json file. Works
    ///                           from any remote client that can read/paste text (e.g.
    ///                           a file attached in a Claude.ai chat) — no encoding
    ///                           step required, just the file's contents verbatim.
    ///   - fileContentBase64   — Base64-encoded bytes, for a .zip (binary) file from a
    ///                           remote client, or any file type a client prefers to
    ///                           send encoded rather than as raw text.
    /// fileName is required alongside either content-based option, since the parser
    /// needs the extension to know whether to treat it as CSV, JSON, or ZIP.
    /// </summary>
    [McpServerTool]
    [Description("Bulk-uploads many assets at once from a CSV, JSON, or ZIP file. " +
             "Use when the user wants to import several assets in one go. " +
             "Provide exactly ONE of: filePath (only if you and this server share a " +
             "filesystem, e.g. local dev), fileContentText (paste the raw CSV/JSON text " +
             "content, plus fileName — the normal choice for a remote chat client or a " +
             "file attached in this conversation), or fileContentBase64 (Base64-encoded " +
             "bytes, plus fileName — for ZIP files or binary content from a remote client).")]
    public async Task<string> BulkUploadAssets(
        [Description("File path readable by THIS SERVER PROCESS directly. Only use when " +
                     "you know the server and the file are on the same machine (e.g. local " +
                     "dev). Leave empty otherwise.")] string? filePath = null,
        [Description("Raw text content of a .csv or .json file, pasted verbatim. Use this " +
                     "for a file attached in the current chat, or any remote client that can " +
                     "read the file as text — no encoding needed. Requires fileName.")] string? fileContentText = null,
        [Description("Base64-encoded bytes of the file. Use for a .zip file, or any file a " +
                     "remote client prefers to send encoded. Requires fileName.")] string? fileContentBase64 = null,
        [Description("Original file name with extension (.csv, .json, or .zip). Required " +
                     "when using fileContentText or fileContentBase64; ignored with filePath.")] string? fileName = null)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(BulkUploadAssets));

        var suppliedCount = new[] { filePath, fileContentText, fileContentBase64 }
            .Count(v => !string.IsNullOrWhiteSpace(v));
        if (suppliedCount == 0)
        {
            return "Provide the file one of three ways: filePath (only if the server can " +
                   "read it directly), fileContentText (paste the CSV/JSON text plus " +
                   "fileName), or fileContentBase64 (encoded bytes plus fileName).";
        }
        if (suppliedCount > 1)
        {
            return "Provide only one of filePath, fileContentText, or fileContentBase64 — not more than one.";
        }

        byte[] fileBytes;
        string extension;

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            if (!File.Exists(filePath))
            {
                await _tableStatusService.LogActivityAsync(nameof(BulkUploadAssets), $"File not found '{filePath}'", success: false);
                return $"I couldn't find a file at '{filePath}'. If this server is deployed " +
                       "remotely, filePath won't work — use fileContentText or fileContentBase64 instead.";
            }

            extension = Path.GetExtension(filePath).ToLowerInvariant();
            fileBytes = await File.ReadAllBytesAsync(filePath);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "fileName is required (e.g. 'assets.json') so I know whether to parse it as CSV, JSON, or ZIP.";
            }

            extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(fileContentText))
            {
                if (extension == ".zip")
                    return "A .zip file is binary — send it via fileContentBase64, not fileContentText.";
                fileBytes = Encoding.UTF8.GetBytes(fileContentText);
            }
            else
            {
                try
                {
                    fileBytes = Convert.FromBase64String(fileContentBase64!);
                }
                catch (FormatException)
                {
                    return "fileContentBase64 doesn't look like valid Base64 — check the encoding.";
                }
            }
        }

        if (extension is not (".csv" or ".json" or ".zip"))
        {
            return $"'{extension}' isn't supported. Use .csv, .json, or .zip.";
        }

        // BulkFileParser.ParseFile() reads from a real path on disk (including
        // ZipFile.ExtractToDirectory for .zip) — reuse it unchanged by writing the
        // supplied bytes to a temp file on THIS server's own local disk first,
        // regardless of which of the three input modes provided them.
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"bulk-{Guid.NewGuid():N}{extension}");
        List<BulkAssetRow> rows;
        try
        {
            await File.WriteAllBytesAsync(tempFilePath, fileBytes);
            rows = BulkFileParser.ParseFile(tempFilePath);
        }
        catch (Exception ex)
        {
            await _tableStatusService.LogActivityAsync(nameof(BulkUploadAssets), $"Parse failed: {ex.Message}", success: false);
            return $"Couldn't read that file: {ex.Message}";
        }
        finally
        {
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        }

        if (rows.Count == 0)
        {
            await _tableStatusService.LogActivityAsync(nameof(BulkUploadAssets), "File had no rows", success: false);
            return "That file has no assets in it.";
        }

        var jobId = Guid.NewGuid().ToString();
        string blobName;

        using (var stream = new MemoryStream(fileBytes))
        {
            blobName = await _photoStorage.UploadPhotoAsync(Guid.Empty, stream, $"bulk-{jobId}{extension}");
        }

        var message = System.Text.Json.JsonSerializer.Serialize(new { jobId, blobName });
        await _queueService.SendMessageAsync(_config.BulkUploadQueue, message);

        _logger.LogInformation("Queued bulk upload job {JobId}, {RowCount} assets, blob {BlobName}.",
            jobId, rows.Count, blobName);
        await _tableStatusService.LogActivityAsync(nameof(BulkUploadAssets), $"Queued job {jobId} with {rows.Count} rows", success: true);

        return $"Started bulk upload job {jobId} with {rows.Count} assets. " +
               $"Ask me for the status of job {jobId} to check progress.";
    }

    #endregion

    #region Tool 8: GetBulkUploadStatus

    /// <summary>
    /// Tool 8: Reads a bulk-upload job's status summary from Table Storage.
    /// </summary>
    [McpServerTool]
    [Description("Checks the status of a previously started bulk upload job. " +
                 "Use when the user asks about the progress or result of a bulk upload, " +
                 "given the job id returned when the upload started.")]
    public async Task<string> GetBulkUploadStatus(
        [Description("The job id returned when the bulk upload started")] string jobId)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(GetBulkUploadStatus));

        var status = await _tableStatusService.GetJobStatusAsync(jobId);
        if (status is null)
            return $"Job {jobId} isn't finished yet, or wasn't found. It may still be processing — try again in a few seconds.";

        // Older job records have no "Skipped" column — fall back to "0".
        var skipped = status.TryGetValue("Skipped", out var sk) ? sk : "0";
        return $"Job {jobId}: {status["Succeeded"]} succeeded, {skipped} skipped (already saved), " +
               $"{status["Failed"]} failed out of {status["Total"]}, completed {status["CompletedAt"]}.";
    }

    #endregion

    #region Tool 9: GetRecentActivity

    /// <summary>
    /// Tool 9: Shows the general tool-call activity log from Table Storage.
    /// </summary>
    [McpServerTool]
    [Description("Shows a log of recent tool activity — what actions were performed " +
                 "and whether they succeeded. Use when the user asks 'what have you done recently', " +
                 "'show recent activity', or wants an audit trail.")]
    public async Task<string> GetRecentActivity(
        [Description("How many recent entries to return")] int count = 10)
    {
        _logger.LogInformation("Tool invoked: {Tool}", nameof(GetRecentActivity));

        var entries = await _tableStatusService.GetRecentActivityAsync(Math.Max(1, count));
        if (entries.Count == 0)
            return "No activity has been logged yet.";

        var sb = new StringBuilder("Recent activity:\n");
        foreach (var entry in entries)
        {
            var timestamp = entry.TryGetValue("Timestamp", out var ts) ? ts : "?";
            var tool = entry.TryGetValue("ToolName", out var t) ? t : "?";
            var detail = entry.TryGetValue("Detail", out var d) ? d : "";
            var success = entry.TryGetValue("Success", out var s) ? s : "?";
            sb.AppendLine($"- [{timestamp}] {tool} ({success}): {detail}");
        }
        return sb.ToString().TrimEnd();
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Generic function words that would otherwise substring-match almost any text (e.g. "to"
    /// matches inside "historic", "story", "auto") and drown out real search terms.
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "to", "in", "on", "at", "of", "for", "and", "or", "is", "are",
        "was", "were", "be", "i", "me", "my", "can", "do", "does", "did", "that", "this",
        "it", "with", "from", "about", "into"
    };

    private static string[] ExtractSignificantTerms(string query) =>
        query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !Stopwords.Contains(t))
            .ToArray();

    private static int CountMatchingTerms(Asset asset, string[] terms)
    {
        var haystack = string.Join(" ", new[]
        {
            asset.Name, asset.Tags, asset.City, asset.Country, asset.Region,
            asset.UserExperience, asset.UserInput
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return terms.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Asset?> FindAssetByNameAsync(string name)
    {
        var assets = (await _dal.GetAllAssetsAsync()).ToList();
        return assets.FirstOrDefault(a =>
                   a.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(a =>
                   a.Name.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tries to parse <paramref name="text"/> as a whole date: dd-MM-yyyy and a few other
    /// common formats first, then a stricter general parse (digits/separators only, so a
    /// short numeric query like "24" isn't misread as a date). Used by SearchAssets to
    /// detect a resolved date passed in the free-text query instead of onDate.
    /// </summary>
    private static bool TryParseFlexibleDate(string text, out DateTime date)
    {
        var trimmed = text.Trim();
        string[] exactFormats = { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "yyyy/MM/dd" };
        if (DateTime.TryParseExact(trimmed, exactFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
            return true;

        // Only treat it as a date if the whole query is plausibly one, not just any string
        // DateTime.TryParse happens to accept a substring of — require it to look like a date
        // (digits and separators only) before trying the lenient parse.
        if (trimmed.All(c => char.IsDigit(c) || c is '-' or '/' or '.') &&
            DateTime.TryParse(trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
            return true;

        date = default;
        return false;
    }

    // Delegates to the shared helper — see TimelineHelpers.
    private static string ComputeTimelineState(DateTime? eventDate) =>
        TimelineHelpers.ComputeTimelineState(eventDate);

    private static int TimelineOrder(string state) => state switch
    {
        "Past" => 0,
        "Present" => 1,
        "Future" => 2,
        _ => 3
    };

    private static string FormatAssetLine(Asset asset) =>
        $"{asset.Name} ({asset.Category})" +
        (string.IsNullOrWhiteSpace(asset.City) ? "" : $", {asset.City}") +
        (asset.EventDate is null ? "" : $", {asset.EventDate:dd-MM-yyyy}");

    private static string FormatAssetList(IEnumerable<Asset> assets, bool includeTimeline = false)
    {
        var sb = new StringBuilder();
        foreach (var a in assets)
        {
            sb.Append("- ").Append(FormatAssetLine(a));
            if (includeTimeline) sb.Append($" [{ComputeTimelineState(a.EventDate)}]");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    #endregion
}
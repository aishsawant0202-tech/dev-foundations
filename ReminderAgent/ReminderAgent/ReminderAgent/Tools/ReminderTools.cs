using Microsoft.Extensions.AI;
using ReminderAgent.Domain;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;


namespace ReminderAgent.Tools
{
    /// <summary>
    /// Provides tool operations for creating, retrieving, searching,
    /// and managing reminder assets through the agent.
    /// </summary>
    public class ReminderTools
    {
        private readonly IStorageProvider _storageProvider;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimilarityService _similarityService;
        private readonly IChatClient _chatClient;

        // Supported image extensions for validation
        private static readonly string[] _allowedExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic" };

        /// <summary>
        /// Initializes a new instance of the <see cref="ReminderTool"/> class.
        /// </summary>
        /// <param name="storageProvider">Storage provider for assets.</param>
        /// <param name="embeddingService">Service for generating embeddings.</param>
        /// <param name="similarityService">Service for similarity computation.</param>
        /// <param name="chatClient">Chat client for generating responses.</param>
        public ReminderTools(
            IStorageProvider storageProvider,
            IEmbeddingService embeddingService,
            ISimilarityService similarityService,
             IChatClient chatClient)
        {
            _storageProvider = storageProvider;
            _embeddingService = embeddingService;
            _similarityService = similarityService;
            _chatClient = chatClient;
        }

        /// <summary>
        /// Creates and saves a new reminder asset with metadata, tags, location, and optional media.
        /// </summary>
        /// <param name="name">The name or title of the item.</param>
        /// <param name="category">The category of the item.</param>
        /// <param name="userInput">The original user-provided text.</param>
        /// <param name="userExperience">The user’s experience or sentiment.</param>
        /// <param name="tags">Optional tags associated with the item.</param>
        /// <param name="metadata">Additional metadata such as notes or details.</param>
        /// <param name="timeContext">Relative time context.</param>
        /// <param name="eventDate">Specific or relative event date.</param>
        /// <param name="photoRefs">Relative photo references.</param>
        /// <param name="city">City information.</param>
        /// <param name="country">Country information.</param>
        /// <param name="region">Region information.</param>
        /// <returns>A confirmation message indicating success or failure.</returns>
        [Description("Creates and saves a new reminder asset such as a book, restaurant, travel destination, contact, playlist, hiking route, or anything else the user wants to remember.")]
        public async Task<string> CreateAsset(
           [Description("The name or title of the item, e.g., 'The Hobbit', 'Vapiano', 'Twelve Apostles'")]
            string name,

           [Description("The category, e.g., 'Book', 'Restaurant', 'Hiking Route', 'Travel', 'Contact', 'Playlist', 'Electronics', 'Event', 'Exercise'")]
            string category,

           [Description("The FULL, UNEDITED raw text typed by the user. Do not paraphrase or change any words.")]
            string userInput,

           [Description("The descriptive word(s) used by the user (e.g., 'wonderful', 'boring'). If none given, provide a 1-word mood summary.")]
            string userExperience,

           [Description("Tags separated by semicolons, e.g., 'summer;travel;germany;italian food'")]
            string? tags= null,

           [Description("Additional metadata: phone number, price, author name, or any other non-location info. Do NOT put location here — use city/country/region instead.")]
            string? metadata= null,

           [Description("Relative time context, e.g., 'next year', 'last month'")]
            string? timeContext= null,

           [Description("Event date in DD-MM-YYYY format if user gives a specific date. If user says a relative phrase like 'next month', 'next September', 'next year', 'last month', 'last week' — pass the phrase AS-IS. Do NOT substitute today's date for relative phrases. Pass null if no date or time context is mentioned at all.")]
            string? eventDate = null,

           [Description("Photo path relative to UserData folder. ONLY the filename with Photos/ prefix e.g., 'Photos/paris.jpg'. NEVER include full directory paths like ReminderAgent/ or UserData/. Leave null if no photo.")]
            string? photoRefs = null,

           [Description("The city mentioned or implied in the user's message, e.g., 'Frankfurt', 'Paris', 'Rome'. Infer from context if not stated directly (e.g., 'Colosseum' → 'Rome'). Leave null if no location.")]
            string? city = null,

            [Description("The country mentioned or implied in the user's message, e.g., 'Germany', 'France', 'Italy'. Infer from city if needed (e.g., Frankfurt → Germany). Leave null if no location.")]
            string? country = null,

            [Description("The region, state, or area, e.g., 'Hesse', 'Bavaria', 'Tuscany', 'New South Wales'. Leave null if unknown or not mentioned.")]
            string? region = null
        )
        {
            // DATE PARSING
            DateTime finalDate;
            bool isParsed = DateTime.TryParseExact(
                eventDate,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out finalDate);
            if (!isParsed)
            {
                finalDate = ResolveRelativeDate(eventDate);
                isParsed = (finalDate != DateTime.Today ||
                             string.IsNullOrWhiteSpace(eventDate));
            }
            if (!isParsed)
            {
                if (!DateTime.TryParse(eventDate, out finalDate))
                {
                    finalDate = DateTime.Today;
                }
            }

            // EXPERIENCE NORMALIZATION
            string finalExperience =
                string.IsNullOrWhiteSpace(userExperience) ||
                userExperience.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                    ? "Neutral"
                    : userExperience;

            // TIMELINE STATE
            string calculatedState = finalDate.Date switch
            {
                var d when d < DateTime.Today => "Past",
                var d when d > DateTime.Today => "Future",
                _ => "Present"
            };

            FileLogger.Info($"CreateAsset | Name: {name} | Category: {category} | City: {city} | Country: {country} | Region: {region}");

            // GENERATE EMBEDDING (MAF STYLE)
            var tagString = tags ?? string.Empty;
            var locationContext = BuildLocationString(city, country, region);
            var embeddingInput = string.IsNullOrWhiteSpace(locationContext)
                ? userInput
                : $"{userInput}. Location: {locationContext}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                name,
                category,
                finalExperience,
                embeddingInput, 
                tagString    
            );

            var metadataDict = new Dictionary<string, string>
            {
                { "Info", metadata ?? string.Empty }
            };

            // Only add location keys if they were provided by the LLM
            if (!string.IsNullOrWhiteSpace(city)) metadataDict["City"] = city.Trim();
            if (!string.IsNullOrWhiteSpace(country)) metadataDict["Country"] = country.Trim();
            if (!string.IsNullOrWhiteSpace(region)) metadataDict["Region"] = region.Trim();

            // BUILD ASSET
            var newAsset = new Asset
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = category,
                UserInput = userInput,
                UserExperience = finalExperience,
                EventDate = finalDate,
                CreatedAt = DateTime.Now,
                TimelineState = calculatedState,
                Embedding = embedding,

                Tags = tags?.Split(',')
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList() ?? new List<string>(),

                Metadata = metadataDict,

                PhotoRefs = photoRefs?
                    .Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList() ?? new List<string>()
            };

            // SAVE
            bool success;
            try
            {
                success = await _storageProvider.SaveAssetAsync(newAsset);
            }
            catch (InvalidOperationException ex)
            {
                FileLogger.Error($"CreateAsset | Save blocked: {ex.Message}");
                return $"Could not save '{name}': {ex.Message}";
            }

            if (success)
            {
                string locationMsg = !string.IsNullOrWhiteSpace(locationContext)
                   ? $" in {locationContext}" : "";
                FileLogger.Info($"Asset saved: {name}{locationMsg}");
                return $"Successfully remembered the {category}: '{name}'{locationMsg} " +
                       $"as a {calculatedState} event for {finalDate:dd-MM-yyyy}.";
            }

            FileLogger.Warning($"Storage failed for: {name}");
            return $"I had trouble saving the '{name}'. Please try again.";
        }

        /// <summary>
        /// Retrieves reminders filtered by timeline state (Past, Present, Future).
        /// </summary>
        /// <param name="state">Optional state filter.</param>
        /// <returns>A natural-language response listing reminders.</returns>
        [Description("Lists reminders by timeline state: Past, Present, or Future. ONLY use when user explicitly says past/future/present reminders with no time period. Do NOT use for last week, last month, next 2 weeks - use SearchAssets for those.")]
        public async Task<string> GetReminders(
            [Description("Filter by: 'Past', 'Present', or 'Future'. Leave empty to get all.")]
            string? state = null)
        {
            FileLogger.Info($"GetReminders | State: {state}");

            var assets = await _storageProvider.GetAssetsAsync();
            DateTime today = DateTime.Today;

            var dynamicReminders = assets.Select(a =>
            {
                if (a.EventDate.HasValue)
                {
                    a.TimelineState =
                        a.EventDate.Value.Date < today ? "Past" :
                        a.EventDate.Value.Date > today ? "Future" :
                        "Present";
                }
                return a;
            }).ToList();

            var filtered = string.IsNullOrWhiteSpace(state)
                ? dynamicReminders
                : dynamicReminders
                    .Where(a => a.TimelineState.Equals(state, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (!filtered.Any())
                return $"I found no {state ?? "total"} reminders.";

            // Build context for RAG
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine($"The following {filtered.Count} reminder(s) were found. Present ALL of them:");
            contextBuilder.AppendLine();

            for (int i = 0; i < filtered.Count; i++)
            {
                var a = filtered[i];
                string location = GetLocationDisplay(a.Metadata);
                string locationPart = string.IsNullOrWhiteSpace(location) ? "" : $" in {location}";
                string notePart = a.Metadata.ContainsKey("Info") && !string.IsNullOrWhiteSpace(a.Metadata["Info"])
                    ? $" Note: {a.Metadata["Info"]}." : "";
                string photoPart = a.PhotoRefs.Any() ? $" Photos: {string.Join(", ", a.PhotoRefs)}." : "";

                contextBuilder.AppendLine(
                    $"Item {i + 1}: {a.Name} is a {a.Category}{locationPart}, " +
                    $"dated {a.EventDate:dd-MM-yyyy} ({a.TimelineState}), " +
                    $"experienced as {a.UserExperience}. " +
                    $"The user said: \"{a.UserInput}\".{notePart}{photoPart}");
                contextBuilder.AppendLine();
            }

            var ragMessages = new List<ChatMessage>
            { new ChatMessage(ChatRole.System,
                                    $"""
                    You are a friendly reminder assistant. Today is {DateTime.Now:f}.
                    The user wants to see their {state ?? "all"} reminders.

                    RULES:
                    1. Present ALL {filtered.Count} reminder(s) — do not skip any.
                    2. Use ONLY the data in the context below — do not invent anything.
                    3. CRITICAL FORMAT: Write in natural flowing prose ONLY.
                       NEVER use bullet points, dashes, numbered lists, or bold labels like **Name:** or **Location:**.
                       NEVER mirror the "Item 1:", "Item 2:" markers — they are for reference only.
                       Weave name, location, date, and experience into sentences naturally.
                    4. Correct example style:
                       "You visited the Eiffel Tower in Paris on 09-02-2026 — a beautiful past trip!
                       You also have an Amsterdam solo adventure coming up on 01-05-2026."
                    5. Be warm and conversational. Keep it concise.

                    CONTEXT ({filtered.Count} reminders — present all of them):
                    {contextBuilder}
                    """),
                new ChatMessage(ChatRole.User, $"show me {state ?? "all"} reminders")
            };

            var ragResponse = await _chatClient.GetResponseAsync(ragMessages);
            return ragResponse.Text ?? $"Found {filtered.Count} reminders but could not generate a response.";
        }

        /// <summary>
        /// Searches reminders using filters such as category, date range, and location,
        /// with optional semantic similarity ranking.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="category">Optional category filter.</param>
        /// <param name="dateRange">Optional date range filter.</param>
        /// <param name="location">Optional location filter.</param>
        /// <param name="topK">Maximum number of results.</param>
        /// <returns>A natural-language response describing results.</returns>
        [Description("Searches saved reminders. ALWAYS use for time periods: 'last week', 'last month', 'next week', 'last year'. Pass as dateRange. Use for locations and categories too. ALWAYS extract category when mentioned: '2 restaurants in Germany'=category:Restaurant, topK:2, location:Germany. Extract count to topK.")]
        public async Task<string> SearchAssets(
                   [Description("What the user is looking for, e.g., 'food places I enjoyed', 'books about habits'")]
            string query,

                    [Description("Category filter. ALWAYS extract from user query when category is mentioned: 'restaurants'='Restaurant', 'books'='Book', 'travel'='Travel', 'events'='Event', 'shopping'='Shopping'. Leave null only if no category mentioned.")]
            string? category = null,

                   [Description("Optional time range, e.g., 'last week', 'last month', 'last two months', 'last year'. Leave empty for no date filter.")]
            string? dateRange = null,

                   [Description("Optional location filter — city, country, or region, e.g., 'Frankfurt', 'Germany', 'Italy'. Leave empty for no location filter.")]
            string? location = null,

                   [Description("Maximum number of results. Extract from user query: 'top 2'=2, 'show 3'=3, 'last week 5 reminders'=5. Default 5 if not mentioned.")]
            int topK = 5)
        {
            FileLogger.Info($"SearchAssets | Query: {query} | Category: {category} | DateRange: {dateRange} | Location: {location}");


            // PHASE 0: PRE-FILTERING
            var allAssets = (await _storageProvider.GetAssetsAsync()).ToList();

            var filtered = allAssets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                filtered = filtered.Where(a =>
                    a.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
                FileLogger.Info($"SearchAssets | After category filter '{category}': {filtered.Count()} assets");
            }

            // Filter 1: Date Range 
            if (!string.IsNullOrWhiteSpace(dateRange))
            {
                var (fromDate, toDate) = ParseDateRange(dateRange);

                if (fromDate.HasValue)
                {
                    filtered = filtered.Where(a =>
                        a.EventDate.HasValue &&
                        a.EventDate.Value.Date >= fromDate.Value.Date &&
                        a.EventDate.Value.Date <= (toDate ?? DateTime.Today).Date);

                    FileLogger.Info($"SearchAssets | Date range: {fromDate:dd-MM-yyyy} to {toDate:dd-MM-yyyy}");
                    FileLogger.Info($"SearchAssets | After date filter: {filtered.Count()} assets");
                }
            }

            // Filter 2: Location
            if (!string.IsNullOrWhiteSpace(location))
            {
                filtered = filtered.Where(a => AssetMatchesLocation(a, location));
                FileLogger.Info($"SearchAssets | After location filter '{location}': {filtered.Count()} assets");
            }

            var filteredList = filtered.ToList();

            // If filters removed everything, return early
            if (!filteredList.Any())
            {
                string filterInfo = BuildFilterDescription(category, dateRange, location);
                return $"I couldn't find any reminders{filterInfo}. Try broadening your search.";
            }
            // PHASE 1: RETRIEVAL
            bool hasStructuredFilter = !string.IsNullOrWhiteSpace(dateRange)
                                    || !string.IsNullOrWhiteSpace(location);
            bool userSpecifiedCount = topK != 5;
            FileLogger.Info($"SearchAssets | hasStructuredFilter={hasStructuredFilter} | topK={topK} | userSpecifiedCount={userSpecifiedCount} | filteredCount={filteredList.Count}");

            List<Asset> topAssets;
            if (hasStructuredFilter)
            {
                var ordered = filteredList.OrderByDescending(a => a.EventDate ?? a.CreatedAt);
                topAssets = userSpecifiedCount
                    ? ordered.Take(topK).ToList()   // "top 2 restaurants in Germany" -> 2
                    : ordered.ToList();             // "last week reminders" -> ALL
                FileLogger.Info($"SearchAssets | Structured path returning {topAssets.Count} assets");
            }
            else
            {
                var queryEmbedding = await _embeddingService.GenerateQueryEmbeddingAsync(query);
                topAssets = await _similarityService.GetTopKSimilarAsync(
                    queryEmbedding, filteredList, topK, threshold: 0.75f);
                if (!topAssets.Any())
                    topAssets = filteredList
                        .OrderByDescending(a => a.EventDate ?? a.CreatedAt)
                        .Take(topK)
                        .ToList();
            }

            // PHASE 2: RESPONSE GENERATION
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine($"The following {topAssets.Count} reminder(s) were found. Present ALL of them:");
            contextBuilder.AppendLine();
            for (int i = 0; i < topAssets.Count; i++)
            {
                var a = topAssets[i];
                string locString = GetLocationDisplay(a.Metadata);
                string locationPart = string.IsNullOrWhiteSpace(locString) ? "" : $" in {locString}";
                string tagPart = a.Tags.Any() ? $" Tagged: {string.Join(", ", a.Tags)}." : "";
                string notePart = a.Metadata.ContainsKey("Info") && !string.IsNullOrWhiteSpace(a.Metadata["Info"])
                    ? $" Note: {a.Metadata["Info"]}." : "";
                string photoPart = a.PhotoRefs.Any() ? $" Photos: {string.Join(", ", a.PhotoRefs)}." : "";

                contextBuilder.AppendLine(
                    $"Item {i + 1}: {a.Name} is a {a.Category}{locationPart}, " +
                    $"dated {a.EventDate:dd-MM-yyyy} ({a.TimelineState}), " +
                    $"described as {a.UserExperience}. " +
                    $"The user said: \"{a.UserInput}\".{tagPart}{notePart}{photoPart}");
                contextBuilder.AppendLine();
            }

            string filterDescription = BuildFilterDescription(category, dateRange, location);
            var ragMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System,
                    $"""
                    You are a friendly reminder assistant. Today is {DateTime.Now:f}.
                    The user searched for: "{query}"{filterDescription}

                    RULES:
                    1. Present ALL {topAssets.Count} reminder(s) — do not skip any.
                    2. Use ONLY the data in the context below - do not invent anything.
                    3. CRITICAL FORMAT: Write in natural flowing prose ONLY.
                       NEVER use bullet points, dashes, numbered lists, or bold labels like **Name:** or **Location:**.
                       NEVER mirror the "Item 1:", "Item 2:" structure from the context — those are reference only.
                       Weave name, location, date and experience into sentences naturally.
                    4. Correct example style:
                       "You have Losteria near Konstabelwache in Berlin coming up on 24-03-2026
                       for some delicious pizza, and Vapiano in Frankfurt, Hesse on 30-03-2026 —
                       known for great Italian food!"
                    5. Keep the overall response concise and warm.

                    CONTEXT ({topAssets.Count} reminders - present all of them):
                    {contextBuilder}
                    """),
                new ChatMessage(ChatRole.User, query)
            };
            var ragResponse = await _chatClient.GetResponseAsync(ragMessages);
            return ragResponse.Text ?? "I found reminders but could not generate a response.";
        }

        /// <summary>
        /// Lists all saved reminders, optionally filtered by category.
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <returns>A formatted list of reminders.</returns>
        [Description("Lists all saved reminders, optionally filtered by category. Use when the user wants to browse or see all items of a specific type like 'show me all my books', 'list my contacts', or 'what do I have saved'.")]
        public async Task<string> ListAssets(
            [Description("Optional category to filter by, e.g., 'Book', 'Restaurant', 'Travel'. Leave empty to list everything grouped by category.")]
            string? category = null)
        {
            FileLogger.Info($"ListAssets | Category: {category ?? "ALL"}");

            var allAssets = (await _storageProvider.GetAssetsAsync()).ToList();

            if (!allAssets.Any())
                return "You haven't saved any reminders yet.";

            if (!string.IsNullOrWhiteSpace(category))
            {
                // Filter by specific category — case insensitive
                var filtered = allAssets
                    .Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(a => a.EventDate ?? a.CreatedAt)
                    .ToList();

                if (!filtered.Any())
                    return $"I don't have any saved reminders in the '{category}' category.";

                var sb = new StringBuilder();
                sb.AppendLine($"Here are all your {category} reminders ({filtered.Count} total):");
                sb.AppendLine();

                foreach (var a in filtered)
                {
                    string location = GetLocationDisplay(a.Metadata);
                    sb.AppendLine($"• {a.Name}");
                    sb.AppendLine($"  Date:       {a.EventDate:dd-MM-yyyy} ({a.TimelineState})");
                    if (!string.IsNullOrWhiteSpace(location))
                        sb.AppendLine($"  Location:   {location}");
                    if (!string.IsNullOrWhiteSpace(a.UserExperience) && a.UserExperience != "Neutral")
                        sb.AppendLine($"  Experience: {a.UserExperience}");
                    if (a.Tags.Any())
                        sb.AppendLine($"  Tags:       {string.Join(", ", a.Tags)}");
                    if (a.PhotoRefs.Any())
                        sb.AppendLine($"  Photos:     {string.Join(", ", a.PhotoRefs)}");
                    sb.AppendLine();
                }

                return sb.ToString().TrimEnd();
            }
            else
            {
                var grouped = allAssets
                    .GroupBy(a => a.Category)
                    .OrderBy(g => g.Key)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"You have {allAssets.Count} total reminders across {grouped.Count} categories:");
                sb.AppendLine();

                foreach (var group in grouped)
                {
                    sb.AppendLine($"── {group.Key} ({group.Count()}) ──");

                    foreach (var a in group.OrderBy(a => a.EventDate ?? a.CreatedAt))
                    {
                        string location = GetLocationDisplay(a.Metadata);
                        string locationStr = string.IsNullOrWhiteSpace(location) ? "" : $" | {location}";
                        string photos = a.PhotoRefs.Any() ? $" | 📷 {a.PhotoRefs.Count}" : "";
                        string dateInfo = a.EventDate.HasValue
                            ? $"| {a.EventDate:dd-MM-yyyy} ({a.TimelineState})" : "";
                        sb.AppendLine($"  • {a.Name}{locationStr} {dateInfo}");
                    }

                    sb.AppendLine();
                }

                return sb.ToString().TrimEnd();
            }
        }


        /// <summary>
        /// Retrieves complete details for a specific reminder.
        /// </summary>
        /// <param name="assetIdentifier">The name or identifier of the asset.</param>
        /// <returns>A detailed description of the asset.</returns>
        [Description("Retrieves complete details for one specific saved reminder. Use when the user asks for full information about a specific item like 'tell me everything about Vapiano' or 'show me details for my Paris trip'.")]
        public async Task<string> GetAssetDetails(
            [Description("The name, partial name, or ID of the asset to look up.")]
            string assetIdentifier)
        {
            FileLogger.Info($"GetAssetDetails | Identifier: {assetIdentifier}");

            if (string.IsNullOrWhiteSpace(assetIdentifier))
                return "Please provide the name of the item you want details for.";

            var allAssets = (await _storageProvider.GetAssetsAsync()).ToList();

            // STEP 1: Exact name match
            var exactMatches = allAssets
                .Where(a => a.Name.Equals(assetIdentifier, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exactMatches.Count == 1)
                return FormatAssetDetails(exactMatches[0]);

            // STEP 2: Partial name match 
            static IEnumerable<string> AssetWords(string s) =>
               s.ToLowerInvariant()
                .Split(new[] { ' ', '-', '_', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2);

            var queryWords = AssetWords(assetIdentifier).ToHashSet();

            var substringMatches = allAssets
                .Where(a =>
                    a.Name.Contains(assetIdentifier, StringComparison.OrdinalIgnoreCase) ||
                    assetIdentifier.Contains(a.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var keywordMatches = allAssets
                .Where(a => AssetWords(a.Name).Any(w => queryWords.Contains(w)))
                .ToList();

            var partialMatches = substringMatches.Union(keywordMatches).Distinct().ToList();

            if (partialMatches.Count == 1)
            {
                FileLogger.Info($"GetAssetDetails | Keyword/partial match: {partialMatches[0].Name}");
                return FormatAssetDetails(partialMatches[0]);
            }

            // STEP 3: ID match
            if (Guid.TryParse(assetIdentifier, out Guid searchId))
            {
                var idMatch = allAssets.FirstOrDefault(a => a.Id == searchId);
                if (idMatch != null)
                    return FormatAssetDetails(idMatch);
            }

            // STEP 4: Multiple matches
            var allMatches = exactMatches.Any() ? exactMatches : partialMatches;

            if (allMatches.Count > 1)
            {
                var options = allMatches.Select(a =>
                {
                    string loc = GetLocationDisplay(a.Metadata);
                    string locStr = string.IsNullOrWhiteSpace(loc) ? "" : $" in {loc}";
                    return $"  • {a.Name} ({a.Category}){locStr} — {a.EventDate:dd-MM-yyyy}";
                });
                return $"I found multiple items matching '{assetIdentifier}'. Which one did you mean?\n\n"
                       + string.Join("\n", options)
                       + "\n\nPlease be more specific.";
            }

            // STEP 5: Not found
            FileLogger.Warning($"GetAssetDetails | Not found: {assetIdentifier}");
            return $"I do not know '{assetIdentifier}'. " +
                   $"It hasn't been saved in your reminders. " +
                   $"Would you like to save it now?";
        }

        /// <summary>
        /// Attaches a photo reference to an existing reminder.
        /// </summary>
        /// <param name="assetIdentifier">The target reminder identifier.</param>
        /// <param name="photoReference">Relative photo path.</param>
        /// <returns>A message indicating the result.</returns>
        [Description("Attaches a relative photo path to an existing saved reminder. Use when the user says 'attach a photo to X', 'add a picture to my Paris trip'. The photoReference must be a relative path like 'Photos/paris.jpg'.")]
        public async Task<string> AttachPhoto(
            [Description("The name or partial name of the reminder to attach the photo to, e.g., 'Vapiano', 'Paris Trip', 'Atomic Habits'")]
            string assetIdentifier,

            [Description("Relative path to the photo, MUST be explicitly provided by the user in their message, e.g., 'Photos/paris.jpg'. NEVER invent, guess, or generate this value. If the user did not provide a file path, do NOT call this tool - instead ask the user: 'Please provide the photo file path, e.g., Photos/filename.jpg'")]
            string photoReference)
        {
            FileLogger.Info($"AttachPhoto | Asset: {assetIdentifier} | Photo:{photoReference}");

            // VALIDATION 
            if (string.IsNullOrWhiteSpace(assetIdentifier))
                return "Please specify which reminder you want to attach the photo to.";

            if (string.IsNullOrWhiteSpace(photoReference))
                return "Please provide a relative photo path, e.g., 'Photos/paris.jpg'.";

            if (IsAbsolutePath(photoReference))
            {
                return $"'{photoReference}' looks like an absolute path (e.g., C:\\...). " +
                       $"Please use a relative path instead, e.g., 'Photos/{Path.GetFileName(photoReference)}'.";
            }

            if (photoReference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                photoReference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return $"URLs are not supported. Please copy the photo to the Photos/ folder " +
                       $"and use a relative path instead, e.g., 'Photos/myimage.jpg'.";
            }

            if (photoReference.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                photoReference.Length > 300)
            {
                return "Base64 image data is not supported — it would make the CSV unreadable. " +
                       "Please save the image as a file in the Photos/ folder and use a relative path, " +
                       "e.g., 'Photos/myimage.jpg'.";
            }

            string ext = Path.GetExtension(photoReference).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !_allowedExtensions.Contains(ext))
            {
                string allowed = string.Join(", ", _allowedExtensions);
                return $"'{photoReference}' does not have a recognised image extension. " +
                       $"Supported formats: {allowed}. " +
                       $"Example: 'Photos/vapiano.jpg'";
            }

            string normalizedPath = photoReference.Trim().Replace('\\', '/');

            // Find the asset 
            var allAssets = (await _storageProvider.GetAssetsAsync()).ToList();

            static IEnumerable<string> Words(string s) =>
                s.ToLowerInvariant()
                 .Split(new[] { ' ', '-', '_', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                 .Where(w => w.Length > 2);

            // Exact match
            var exactMatches = allAssets
                .Where(a => a.Name.Equals(assetIdentifier, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Substring match
            var substringMatches = allAssets
                .Where(a =>
                    a.Name.Contains(assetIdentifier, StringComparison.OrdinalIgnoreCase) ||
                    assetIdentifier.Contains(a.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var queryWords = Words(assetIdentifier).ToHashSet();
            var keywordMatches = allAssets
                .Where(a => Words(a.Name).Any(w => queryWords.Contains(w)))
                .ToList();

            // Merge: exact → substring → keyword, deduplicated, preserving priority order
            var partialMatches = substringMatches
                .Union(keywordMatches)
                .Distinct()
                .ToList();

            // ID match
            Asset? targetAsset = null;

            if (exactMatches.Count >= 1)
            {
                targetAsset = exactMatches[0];
                FileLogger.Info($"AttachPhoto | Exact match: {targetAsset.Name}");
            }
            else if (partialMatches.Count == 1)
            {
                targetAsset = partialMatches[0];
                FileLogger.Info($"AttachPhoto | Partial match found: {targetAsset.Name}");
            }
            else if (Guid.TryParse(assetIdentifier, out Guid gid))
            {
                targetAsset = allAssets.FirstOrDefault(a => a.Id == gid);
            }

            // Multiple matches
            var candidates = exactMatches.Any() ? exactMatches : partialMatches;
            if (targetAsset == null && candidates.Count > 1)
            {
                targetAsset = candidates
                    .OrderBy(a => a.PhotoRefs?.Count ?? 0)
                    .ThenByDescending(a => a.CreatedAt)
                    .First();
                FileLogger.Info($"AttachPhoto | Multiple matches ({candidates.Count}), auto-selected: " +
                                $"{targetAsset.Name} ({targetAsset.Category}) id={targetAsset.Id}");
            }

            // Not found
            if (targetAsset == null)
            {
                FileLogger.Warning($"AttachPhoto | Not found: {assetIdentifier}");
                return $"I do not know '{assetIdentifier}'. " +
                       $"It hasn't been saved in your reminders. " +
                       $"Would you like to save it first?";
            }

            // Duplicate Check
            bool alreadyAttached = targetAsset.PhotoRefs.Any(p =>
                p.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (alreadyAttached)
                return $"'{normalizedPath}' is already attached to '{targetAsset.Name}'. " +
                       $"It currently has {targetAsset.PhotoRefs.Count} photo(s).";

            // Append + Save
            targetAsset.PhotoRefs ??= new List<string>();
            targetAsset.PhotoRefs.Add(normalizedPath);

            FileLogger.Info($"AttachPhoto | Attaching '{normalizedPath}' to '{targetAsset.Name}'. " +
                            $"Total photos: {targetAsset.PhotoRefs.Count}");
            bool success = await _storageProvider.UpdateAssetAsync(targetAsset);

            if (!success)
            {
                FileLogger.Error($"AttachPhoto | UpdateAssetAsync failed for: {targetAsset.Name}");
                return $"I found '{targetAsset.Name}' but had trouble saving the photo. Please try again.";

            }

            FileLogger.Info($"AttachPhoto | Success. '{targetAsset.Name}' now has {targetAsset.PhotoRefs.Count} photo(s).");

            return $"Done! '{normalizedPath}' is now attached to '{targetAsset.Name}'.\n" +
                   $"'{targetAsset.Name}' has {targetAsset.PhotoRefs.Count} photo(s) total.";
        }

        /// <summary>
        /// Performs semantic search over reminders using embeddings.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="topK">Maximum number of results.</param>
        /// <returns>A natural-language response with relevant reminders.</returns>
        [Description("Searches reminders purely by meaning using RAG. Use for vague or descriptive queries with no specific category,date or location filter.")]
        public async Task<string> SearchRemindersSemantic(
            [Description("What the user is searching for")]
            string query,
            int topK = 5)
        {
            FileLogger.Info($"SearchRemindersSemantic | Query: {query}");
            var queryEmbedding = await _embeddingService.GenerateQueryEmbeddingAsync(query);
            var assetList = (await _storageProvider.GetAssetsAsync()).ToList();

            var topAssets = await _similarityService.GetTopKSimilarAsync(
                queryEmbedding, assetList, topK, threshold: 0.25f);

            if (!topAssets.Any())
                return "I couldn't find anything related to that in your saved reminders.";

            // Build context
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Here are the user's relevant saved reminders:");
            contextBuilder.AppendLine();

            for (int i = 0; i < topAssets.Count; i++)
            {
                var a = topAssets[i];
                string loc = GetLocationDisplay(a.Metadata);
                string locationPart = string.IsNullOrWhiteSpace(loc) ? "" : $" in {loc}";
                string notePart = a.Metadata.ContainsKey("Info") && !string.IsNullOrWhiteSpace(a.Metadata["Info"])
                    ? $" Note: {a.Metadata["Info"]}." : "";
                string tagPart = a.Tags.Any() ? $" Tags: {string.Join(", ", a.Tags)}." : "";
                string photoPart = a.PhotoRefs.Any() ? $" Photos: {string.Join(", ", a.PhotoRefs)}." : "";

                contextBuilder.AppendLine(
                    $"Item {i + 1}: {a.Name} is a {a.Category}{locationPart}, " +
                    $"dated {a.EventDate:dd-MM-yyyy} ({a.TimelineState}), " +
                    $"experienced as {a.UserExperience}. " +
                    $"The user said: \"{a.UserInput}\".{notePart}{tagPart}{photoPart}");
                contextBuilder.AppendLine();
            }
            var ragMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System,
                    $"""
                    You are a helpful and friendly reminder assistant. Today is {DateTime.Now:f}.
                    Answer using ONLY the reminder context below.
                    Do NOT invent reminders not in the context.
                    RULES:
                    1. CRITICAL FORMAT: Write in natural flowing prose ONLY.
                       NEVER use bullet points, dashes, numbered lists, or bold labels like **Name:** or **Location:**.
                       NEVER mirror the "Item 1:", "Item 2:" structure from the context — those are reference only.
                       Weave name, location, date and experience into sentences naturally.
                    2. Correct example style:
                       "You found the Black Forest really relaxing during your trip
                       to Baden-Württemberg last March."
                    3. Be warm and conversational. Mention location naturally when relevant.
                    4. Keep it concise — one or two sentences per reminder is enough.

                    CONTEXT:
                    {contextBuilder}
                    """),
                new ChatMessage(ChatRole.User, query)
            };

            var ragResponse = await _chatClient.GetResponseAsync(ragMessages);
            return ragResponse.Text ?? "I found reminders but could not generate a response.";
        }

        /// <summary>
        /// Formats an asset into a detailed human-readable string.
        /// </summary>
        /// <param name="a">The asset to format.</param>
        /// <returns>A formatted string containing asset details.</returns>
        private string FormatAssetDetails(Asset a)
        {
            FileLogger.Info($"FormatAssetDetails | Formatting: {a.Name}");

            var sb = new StringBuilder();
            sb.AppendLine($"── Details for: {a.Name} ──");
            sb.AppendLine();
            sb.AppendLine($"  ID:          {a.Id}");
            sb.AppendLine($"  Name:        {a.Name}");
            sb.AppendLine($"  Category:    {a.Category}");
            sb.AppendLine($"  Timeline:    {a.TimelineState}");
            sb.AppendLine($"  Event Date:  {(a.EventDate.HasValue ? a.EventDate.Value.ToString("dd-MM-yyyy") : "Not set")}");
            sb.AppendLine($"  Created At:  {a.CreatedAt:dd-MM-yyyy HH:mm}");
            sb.AppendLine($"  Experience:  {a.UserExperience}");

            // Show location fields separately
            if (a.Metadata.ContainsKey("City") && !string.IsNullOrWhiteSpace(a.Metadata["City"]))
                sb.AppendLine($"  City:        {a.Metadata["City"]}");
            if (a.Metadata.ContainsKey("Country") && !string.IsNullOrWhiteSpace(a.Metadata["Country"]))
                sb.AppendLine($"  Country:     {a.Metadata["Country"]}");
            if (a.Metadata.ContainsKey("Region") && !string.IsNullOrWhiteSpace(a.Metadata["Region"]))
                sb.AppendLine($"  Region:      {a.Metadata["Region"]}");

            if (a.Metadata.ContainsKey("Info") && !string.IsNullOrWhiteSpace(a.Metadata["Info"]))
                sb.AppendLine($"  Notes:       {a.Metadata["Info"]}");
            if (a.Tags.Any())
                sb.AppendLine($"  Tags:        {string.Join(", ", a.Tags)}");
            if (a.PhotoRefs.Any())
            {
                sb.AppendLine($"  Photos ({a.PhotoRefs.Count}):");
                for (int i = 0; i < a.PhotoRefs.Count; i++)
                    sb.AppendLine($"    [{i + 1}] {a.PhotoRefs[i]}");
            }
            else
                sb.AppendLine($"  Photos:      None attached");

            sb.AppendLine();
            sb.AppendLine($"  Original note: \"{a.UserInput}\"");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Determines whether a file path is absolute.
        /// </summary>
        /// <param name="path">The path to evaluate.</param>
        /// <returns>True if the path is absolute; otherwise false.</returns>
        private bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            // Windows absolute: starts with drive letter + colon (e.g., "C:\")
            if (path.Length >= 2 && path[1] == ':') return true;

            // Windows UNC: starts with "\\"
            if (path.StartsWith("\\\\")) return true;

            // Unix/Linux/Mac absolute: starts with "/"
            if (path.StartsWith("/")) return true;

            return false;
        }

        /// <summary>
        /// Determines whether an asset matches a given location filter.
        /// </summary>
        /// <param name="a">The asset to evaluate.</param>
        /// <param name="location">The location filter.</param>
        /// <returns>True if the asset matches the location; otherwise false.</returns>
        private bool AssetMatchesLocation(Asset a, string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return true;

            string loc = location.ToLowerInvariant().Trim();

            bool cityMatch = a.Metadata.ContainsKey("City") &&
                                a.Metadata["City"].ToLowerInvariant().Contains(loc);
            bool countryMatch = a.Metadata.ContainsKey("Country") &&
                                a.Metadata["Country"].ToLowerInvariant().Contains(loc);
            bool regionMatch = a.Metadata.ContainsKey("Region") &&
                                a.Metadata["Region"].ToLowerInvariant().Contains(loc);
            bool cityReverse = a.Metadata.ContainsKey("City") &&
                                  loc.Contains(a.Metadata["City"].ToLowerInvariant());
            bool countryReverse = a.Metadata.ContainsKey("Country") &&
                                  loc.Contains(a.Metadata["Country"].ToLowerInvariant());

            return cityMatch || countryMatch || regionMatch || cityReverse || countryReverse;
        }

        /// <summary>
        /// Builds a display string from location metadata.
        /// </summary>
        /// <param name="metadata">The metadata dictionary.</param>
        /// <returns>A formatted location string.</returns>
        private string GetLocationDisplay(Dictionary<string, string> metadata)
        {
            var parts = new List<string>();

            if (metadata.ContainsKey("City") && !string.IsNullOrWhiteSpace(metadata["City"]))
                parts.Add(metadata["City"]);
            if (metadata.ContainsKey("Region") && !string.IsNullOrWhiteSpace(metadata["Region"]))
                parts.Add(metadata["Region"]);
            if (metadata.ContainsKey("Country") && !string.IsNullOrWhiteSpace(metadata["Country"]))
                parts.Add(metadata["Country"]);

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Builds a combined location string from city, region, and country.
        /// </summary>
        private string BuildLocationString(string? city, string? country, string? region)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(city)) parts.Add(city!.Trim());
            if (!string.IsNullOrWhiteSpace(region)) parts.Add(region!.Trim());
            if (!string.IsNullOrWhiteSpace(country)) parts.Add(country!.Trim());
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Parses a natural language date range into start and end dates.
        /// </summary>
        private (DateTime? from, DateTime? to) ParseDateRange(string dateRange)
        {
            if (string.IsNullOrWhiteSpace(dateRange)) return (null, null);
            string input = dateRange.ToLowerInvariant().Trim();
            DateTime today = DateTime.Today;

            // Future: next week, next 2 weeks, next month, upcoming
            bool isFuture = input.Contains("next") || input.Contains("upcoming") || input.Contains("coming");
            if (isFuture)
            {
                if (input.Contains("month")) { int n = ExtractNumber(input); return (today, today.AddMonths(n > 1 ? n : 1)); }
                if (input.Contains("week")) { int n = ExtractNumber(input); return (today, today.AddDays((n > 1 ? n : 1) * 7)); }
                if (input.Contains("year")) { int n = ExtractNumber(input); return (today, today.AddYears(n > 1 ? n : 1)); }
                if (input.Contains("day")) { int n = ExtractNumber(input); if (n > 0) return (today, today.AddDays(n)); }
            }

            // Past: calendar boundaries
            if (input.Contains("month"))
            {
                int n = ExtractNumber(input);
                var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
                if (n > 1) return (firstOfThisMonth.AddMonths(-n), firstOfThisMonth.AddDays(-1));
                return (firstOfThisMonth.AddMonths(-1), firstOfThisMonth.AddDays(-1));
            }
            if (input.Contains("week"))
            {
                int n = ExtractNumber(input);
                int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
                var thisMonday = today.AddDays(-daysFromMonday);
                if (n > 1) return (thisMonday.AddDays(-n * 7), thisMonday.AddDays(-1));
                return (thisMonday.AddDays(-7), thisMonday.AddDays(-1));
            }
            if (input.Contains("year"))
            {
                int n = ExtractNumber(input);
                var firstOfThisYear = new DateTime(today.Year, 1, 1);
                if (n > 1) return (firstOfThisYear.AddYears(-n), firstOfThisYear.AddDays(-1));
                return (firstOfThisYear.AddYears(-1), firstOfThisYear.AddDays(-1));
            }
            if (input.Contains("day")) { int n = ExtractNumber(input); if (n > 0) return (today.AddDays(-n), today); }

            return (null, null);
        }

        /// <summary>
        /// Extracts a numeric value from a natural language string.
        /// </summary>
        private int ExtractNumber(string input)
        {
            // Try parsing a digit directly first (e.g., "last 3 months")
            var words = input.Split(' ');
            foreach (var word in words)
                if (int.TryParse(word, out int n)) return n;

            // Map written words to numbers (e.g., "last two months")
            var wordMap = new Dictionary<string, int>
            {
                { "one",   1 }, { "two",   2 }, { "three", 3 },
                { "four",  4 }, { "five",  5 }, { "six",   6 },
                { "seven", 7 }, { "eight", 8 }, { "nine",  9 },
                { "ten",  10 }, { "eleven",11 }, { "twelve",12 }
            };

            foreach (var word in words)
                if (wordMap.TryGetValue(word, out int mapped)) return mapped;

            return 0;
        }

        /// <summary>
        /// Builds a readable description of applied filters.
        /// </summary>
        private string BuildFilterDescription(string? category, string? dateRange, string? location = null)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(category))
                parts.Add($" in category '{category}'");

            if (!string.IsNullOrWhiteSpace(dateRange))
                parts.Add($" from '{dateRange}'");

            if (!string.IsNullOrWhiteSpace(location))
                parts.Add($" in '{location}'");

            return string.Join("", parts);
        }

        /// <summary>
        /// Resolves relative date expressions into concrete dates.
        /// </summary>
        private DateTime ResolveRelativeDate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return DateTime.Today;

            string s = input.ToLowerInvariant().Trim();
            DateTime today = DateTime.Today;

            // "next year" / "next month" / "next week"
            if (s.Contains("next year")) return today.AddYears(1);
            if (s.Contains("next month")) return today.AddMonths(1);
            if (s.Contains("next week")) return today.AddDays(7);

            // "next N months" / "next N weeks" / "next N years"
            int n = ExtractNumber(s);
            if (s.Contains("month") && n > 0) return today.AddMonths(n);
            if (s.Contains("week") && n > 0) return today.AddDays(n * 7);
            if (s.Contains("year") && n > 0) return today.AddYears(n);

            // Seasons → map to first day of that season's month
            if (s.Contains("spring")) { int y = today.Month < 3 ? today.Year : today.Year + 1; return new DateTime(y, 3, 1); }
            if (s.Contains("summer")) { int y = today.Month < 6 ? today.Year : today.Year + 1; return new DateTime(y, 6, 1); }
            if (s.Contains("autumn") || s.Contains("fall"))
            { int y = today.Month < 9 ? today.Year : today.Year + 1; return new DateTime(y, 9, 1); }
            if (s.Contains("winter")) { int y = today.Month < 12 ? today.Year : today.Year + 1; return new DateTime(y, 12, 1); }

            // "next september", "next june" etc.
            string[] months = {"january","february","march","april","may","june",
                       "july","august","september","october","november","december"};

            for (int i = 0; i < months.Length; i++)
            {
                if (s.Contains(months[i]))
                {
                    int targetMonth = i + 1;
                    int targetYear = today.Month < targetMonth
                        ? today.Year
                        : today.Year + 1;   // already passed this month → next year
                    return new DateTime(targetYear, targetMonth, 1);
                }
            }

            // "last month" / "last week" / "yesterday"
            if (s.Contains("last year")) return today.AddYears(-1);
            if (s.Contains("last month")) return today.AddMonths(-1);
            if (s.Contains("last week")) return today.AddDays(-7);
            if (s.Contains("yesterday")) return today.AddDays(-1);
            if (s.Contains("today")) return today;
            if (s.Contains("tomorrow")) return today.AddDays(1);

            return DateTime.Today; // true fallback
        }
    }
}
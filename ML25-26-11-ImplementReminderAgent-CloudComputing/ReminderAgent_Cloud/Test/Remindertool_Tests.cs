using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReminderAgent.Common;
using OpenAI;
using ReminderAgent.Infrastructure;
using Microsoft.Extensions.AI;

namespace ReminderAgent.Tests.Integration;

/// <summary>
/// Integration tests for ReminderTools' create/search/details flow, plus the
/// TimelineHelpers and DuplicateAssetHelpers logic it depends on. Runs against a real
/// ReminderTools instance backed by real Azure SQL — nothing here is mocked.
/// </summary>
/// <remarks>
/// <see cref="DoNotParallelizeAttribute"/> avoids racing other Azure-SQL test classes.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class RemindertoolTests
{
    #region Setup

    private static ServiceProvider _provider = null!;
    private static IDataAccessLayer      _dal   = null!;
    private static ITableStatusService   _table = null!;
    private static IQueueService         _queue = null!;
    private static ReminderTools         _tools = null!;

    private readonly List<Guid> _assetIds = new();

    private static readonly string Run = Guid.NewGuid().ToString("N")[..6];

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json",     optional: false)
            .AddJsonFile("appsettings.dev.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<MyConfig>();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IDataAccessLayer,      SqlDataAccessLayer>();
        services.AddSingleton<IPhotoStorageProvider, AzureBlobPhotoStorageProvider>();
        services.AddSingleton<IQueueService,         AzureQueueService>();
        services.AddSingleton<ITableStatusService,   AzureTableStatusService>();
        services.AddSingleton<ISimilarityService,    SimilarityService>();

        // Register embedding service using a factory that reads from config
        // This avoids calling AsIEmbeddingGenerator directly
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            var cfg    = sp.GetRequiredService<IConfiguration>();
            var apiKey = cfg["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey missing");

            // Use reflection to call the same code as Program.cs
            // without needing the package reference directly
            var openAiClient = new OpenAI.OpenAIClient(
                new System.ClientModel.ApiKeyCredential(apiKey));
            var embClient = openAiClient.GetEmbeddingClient("text-embedding-3-small");
           var embGen = embClient.AsIEmbeddingGenerator();

            return new EmbeddingService(embGen);
        });

        services.AddSingleton<ReminderTools>();

        _provider = services.BuildServiceProvider();
        _dal      = _provider.GetRequiredService<IDataAccessLayer>();
        _table    = _provider.GetRequiredService<ITableStatusService>();
        _queue    = _provider.GetRequiredService<IQueueService>();
        _tools    = _provider.GetRequiredService<ReminderTools>();
    }

    [ClassCleanup]
    public static void ClassCleanup() => _provider?.Dispose();

    [TestCleanup]
    public async Task Cleanup()
    {
        foreach (var id in _assetIds)
            try { await _dal.DeleteAssetAsync(id); } catch { }
    }

    private string N(string label) => $"Test-{label}-{Run}";

    private async Task<Asset> MakeAsset(string label,
        string category = "restaurant",
        string city = "Frankfurt",
        string? eventDate = null)
    {
        var n = N(label);
        await _tools.CreateAsset(
            name: n, category: category,
            city: city, country: "Germany",
            userExperience: "Integration test asset",
            eventDate: eventDate);
        var all = (await _dal.GetAllAssetsAsync()).ToList();
        var a   = all.First(x =>
            x.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
        _assetIds.Add(a.Id);
        return a;
    }

    #endregion

    #region CreateAsset Tests

    /// <summary>
    /// CreateAsset's reply confirms the save, mentioning either "saved" or the asset's name.
    /// </summary>
    [TestMethod]
    public async Task TC_01_CreateAsset_ConsoleOutput_ShowsConfirmation()
    {
        var name  = N("Rest");
        var reply = await _tools.CreateAsset(
            name: name, category: "restaurant",
            tags: "Italian;Pasta", city: "Frankfurt", country: "Germany",
            userExperience: "Great pasta", userInput: "Save restaurant");

        Console.WriteLine($"[TC-01] Output: {reply}");

        var all = (await _dal.GetAllAssetsAsync()).ToList();
        var a   = all.FirstOrDefault(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (a != null) _assetIds.Add(a.Id);

        Assert.IsTrue(
            reply.ToLower().Contains("saved") || reply.Contains(name),
            $"Expected confirmation. Got: {reply}");
    }

    /// <summary>
    /// An asset created through CreateAsset actually exists as a row in Azure SQL.
    /// </summary>
    [TestMethod]
    public async Task TC_02_CreateAsset_RowSaved_InAzureSQL()
    {
        var asset   = await MakeAsset("SQLRow");
        var fetched = await _dal.GetAssetByIdAsync(asset.Id);

        Console.WriteLine($"[TC-02] Name={fetched?.Name} Category={fetched?.Category}");

        Assert.IsNotNull(fetched, "Row must exist in dbo.Assets.");
        Assert.AreEqual(asset.Name, fetched.Name, "Name must match.");
    }

    /// <summary>
    /// Every field passed to CreateAsset (category, city, country) is saved correctly in SQL.
    /// </summary>
    [TestMethod]
    public async Task TC_03_CreateAsset_AllFields_SavedCorrectlyInSQL()
    {
        var name = N("FieldCheck");
        await _tools.CreateAsset(
            name:           name,
            category:       "restaurant",
            tags:           "Italian;Frankfurt",
            city:           "Frankfurt",
            country:        "Germany",
            userExperience: "Amazing food",
            userInput:      "Save restaurant");

        await Task.Delay(1000);

        var all   = (await _dal.GetAllAssetsAsync()).ToList();
        var asset = all.FirstOrDefault(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (asset != null) _assetIds.Add(asset.Id);

        Console.WriteLine($"[TC-03] Name={asset?.Name}");
        Console.WriteLine($"[TC-03] Category={asset?.Category}");
        Console.WriteLine($"[TC-03] City={asset?.City}");
        Console.WriteLine($"[TC-03] Country={asset?.Country}");

        Assert.IsNotNull(asset,               "Asset must exist in SQL.");
        Assert.AreEqual("restaurant",         asset.Category, "Category must match.");
        Assert.AreEqual("Frankfurt",          asset.City,     "City must match.");
        Assert.AreEqual("Germany",            asset.Country,  "Country must match.");
    }

    /// <summary>
    /// An asset with a future event date gets TimelineState "Future".
    /// </summary>
    [TestMethod]
    public async Task TC_04_CreateAsset_FutureDate_TimelineIsFuture()
    {
        var asset = await MakeAsset("Future", eventDate: "01-01-2030");

        Console.WriteLine($"[TC-04] TimelineState: {asset.TimelineState}");

        Assert.AreEqual("Future", asset.TimelineState, "Must be Future.");
    }

    /// <summary>
    /// An asset with a past event date gets TimelineState "Past".
    /// </summary>
    [TestMethod]
    public async Task TC_05_CreateAsset_PastDate_TimelineIsPast()
    {
        var asset = await MakeAsset("Past", eventDate: "01-01-2020");

        Console.WriteLine($"[TC-05] TimelineState: {asset.TimelineState}");

        Assert.AreEqual("Past", asset.TimelineState, "Must be Past.");
    }

    /// <summary>
    /// CreateAsset rejects a missing name with an error message that mentions "name".
    /// </summary>
    [TestMethod]
    public async Task TC_06_CreateAsset_EmptyName_ReturnsError()
    {
        var reply = await _tools.CreateAsset(name: "", category: "restaurant");

        Console.WriteLine($"[TC-06] Output: {reply}");

        Assert.Contains("name", reply, StringComparison.OrdinalIgnoreCase,
            $"Must mention name in error. Got: {reply}");
    }

    #endregion

    #region SearchAssets Tests

    /// <summary>
    /// SearchAssets with no filters returns the full list of saved assets.
    /// </summary>
    [TestMethod]
    public async Task TC_07_SearchAssets_NoFilters_ReturnsFullList()
    {
        await MakeAsset("List");
        var reply = await _tools.SearchAssets();

        Console.WriteLine($"[TC-07] Output: {reply[..Math.Min(100, reply.Length)]}");

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply),
            "Must return non-empty result.");
    }

    /// <summary>
    /// SearchAssets' category filter returns results matching that category.
    /// </summary>
    [TestMethod]
    public async Task TC_08_SearchAssets_CategoryFilter_ReturnsResults()
    {
        await MakeAsset("Cat", category: "hotel");
        var reply = await _tools.SearchAssets(category: "hotel");

        Console.WriteLine($"[TC-08] Output: {reply[..Math.Min(100, reply.Length)]}");

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply),
            "Category filter must return results.");
    }

    /// <summary>
    /// SearchAssets' timeline=Future filter returns results.
    /// </summary>
    [TestMethod]
    public async Task TC_09_SearchAssets_TimelineFuture_ReturnsResults()
    {
        await MakeAsset("FutureTimeline", eventDate: "01-01-2030");
        var reply = await _tools.SearchAssets(timeline: "Future");

        Console.WriteLine($"[TC-09] Output: {reply[..Math.Min(100, reply.Length)]}");

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply),
            "Timeline=Future must return results.");
    }

    /// <summary>
    /// SearchAssets with semantic=true returns results via embedding similarity.
    /// </summary>
    [TestMethod]
    public async Task TC_10_SearchAssets_Semantic_ReturnsResults()
    {
        await MakeAsset("Sem");
        await Task.Delay(500);
        var reply = await _tools.SearchAssets(
            query: "cosy Italian restaurant",
            semantic: true, topK: 5);

        Console.WriteLine($"[TC-10] Output: {reply[..Math.Min(100, reply.Length)]}");

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply),
            "Semantic search must return results.");
    }

    #endregion

    #region GetAssetDetails Tests

    /// <summary>
    /// GetAssetDetails returns the full details of a saved asset by name.
    /// </summary>
    [TestMethod]
    public async Task TC_11_GetAssetDetails_ReturnsFullDetails()
    {
        var asset = await MakeAsset("Det");
        var reply = await _tools.GetAssetDetails(asset.Name);

        Console.WriteLine($"[TC-11] Output:\n{reply}");

        Assert.IsTrue(
            reply.Contains("restaurant", StringComparison.OrdinalIgnoreCase) ||
            reply.Contains("Frankfurt",  StringComparison.OrdinalIgnoreCase) ||
            reply.Contains(asset.Name,   StringComparison.OrdinalIgnoreCase),
            $"Must contain details. Got: {reply}");
    }

    /// <summary>
    /// GetAssetDetails for a name that doesn't resolve to any saved asset returns a
    /// not-found message.
    /// </summary>
    [TestMethod]
    public async Task TC_12_GetAssetDetails_UnknownName_ReturnsNotFound()
    {
        var reply = await _tools.GetAssetDetails("NonExistentXYZ999999");

        Console.WriteLine($"[TC-12] Output: {reply}");

        Assert.IsTrue(
            reply.ToLower().Contains("couldn't find") ||
            reply.ToLower().Contains("not found"),
            $"Must return not-found. Got: {reply}");
    }

    #endregion

    #region TimelineHelpers Tests

    /// <summary>
    /// TimelineHelpers.ParseEventDate parses both the expected dd-MM-yyyy format and the
    /// ISO yyyy-MM-dd fallback.
    /// </summary>
    [TestMethod]
    public void TC_13_ParseEventDate_ExactAndFallbackFormats_BothParse()
    {
        Assert.AreEqual(new DateTime(2030, 12, 25), TimelineHelpers.ParseEventDate("25-12-2030")!.Value.Date);
        Assert.AreEqual(new DateTime(2030, 12, 25), TimelineHelpers.ParseEventDate("2030-12-25")!.Value.Date);
    }

    /// <summary>
    /// TimelineHelpers.ParseEventDate returns null for blank or unparseable input.
    /// </summary>
    [TestMethod]
    public void TC_14_ParseEventDate_BlankOrUnparseable_ReturnsNull()
    {
        Assert.IsNull(TimelineHelpers.ParseEventDate(null));
        Assert.IsNull(TimelineHelpers.ParseEventDate("   "));
        Assert.IsNull(TimelineHelpers.ParseEventDate("next Monday"));
    }

    /// <summary>
    /// TimelineHelpers.ComputeTimelineState covers Past, Present, Future, and a null date
    /// (treated as Present).
    /// </summary>
    [TestMethod]
    public void TC_15_ComputeTimelineState_PastPresentFutureAndNull()
    {
        var today = DateTime.UtcNow.Date;

        Assert.AreEqual("Past",    TimelineHelpers.ComputeTimelineState(today.AddDays(-1)));
        Assert.AreEqual("Present", TimelineHelpers.ComputeTimelineState(today));
        Assert.AreEqual("Future",  TimelineHelpers.ComputeTimelineState(today.AddDays(1)));
        Assert.AreEqual("Present", TimelineHelpers.ComputeTimelineState(null));
    }

    #endregion

    #region DuplicateAssetHelpers Tests

    /// <summary>
    /// DuplicateAssetHelpers.FindMatch matches on Name+Category, trimmed and
    /// case-insensitively, and returns no match when the category differs.
    /// </summary>
    [TestMethod]
    public void TC_16_DuplicateAssetHelpers_FindMatch_TrimmedCaseInsensitive()
    {
        var existing = new List<Asset> { new() { Name = " Trattoria Roma ", Category = "restaurant" } };

        var match = DuplicateAssetHelpers.FindMatch(existing, "trattoria roma", "RESTAURANT");
        var noMatch = DuplicateAssetHelpers.FindMatch(existing, "Trattoria Roma", "hotel");

        Assert.IsNotNull(match);
        Assert.IsNull(noMatch);
    }

    /// <summary>
    /// DuplicateAssetHelpers.IsExactDuplicate: same EventDate (including both null) counts
    /// as an exact duplicate; a different EventDate does not.
    /// </summary>
    [TestMethod]
    public void TC_17_DuplicateAssetHelpers_IsExactDuplicate_SameOrDifferentDate()
    {
        var existing = new Asset { EventDate = new DateTime(2030, 1, 1) };

        Assert.IsTrue(DuplicateAssetHelpers.IsExactDuplicate(existing, new DateTime(2030, 1, 1)));
        Assert.IsFalse(DuplicateAssetHelpers.IsExactDuplicate(existing, new DateTime(2030, 1, 2)));
        Assert.IsTrue(DuplicateAssetHelpers.IsExactDuplicate(new Asset { EventDate = null }, null));
    }

    /// <summary>
    /// DuplicateAssetHelpers.MergeForUpdate backfills only the incoming asset's blank
    /// fields from the existing row; a non-blank incoming value always wins.
    /// </summary>
    [TestMethod]
    public void TC_18_DuplicateAssetHelpers_MergeForUpdate_BackfillsBlankFieldsOnly()
    {
        var existing = new Asset
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTime(2020, 1, 1),
            Tags = "Italian;Cosy",
            City = "Frankfurt",
            UserExperience = "Loved it"
        };
        // Incoming save only has a new EventDate — everything else is blank, which is
        // exactly the scenario that used to null out the existing row's data.
        var incoming = new Asset { Tags = null, City = null, UserExperience = "Even better this time" };

        var merged = DuplicateAssetHelpers.MergeForUpdate(incoming, existing);

        Assert.AreEqual(existing.Id, merged.Id);
        Assert.AreEqual(existing.CreatedAt, merged.CreatedAt);
        Assert.AreEqual("Italian;Cosy", merged.Tags, "Blank incoming Tags must fall back to existing.");
        Assert.AreEqual("Frankfurt", merged.City, "Blank incoming City must fall back to existing.");
        Assert.AreEqual("Even better this time", merged.UserExperience, "Non-blank incoming value must win.");
    }

    #endregion
}

using Azure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using ReminderAgent.Common;
using ReminderAgent.Infrastructure;
using System.ClientModel;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace ReminderAgent.Cloud;

/// <summary>ReminderAgent.Cloud — console REPL + background bulk-upload worker.</summary>
/// <remarks>
/// Verifies end-to-end storage against Azure SQL/Blob/Queue/Table
/// Runs the chat loop and BulkUploadWorker.
/// </remarks>
public class Program
{
    public static async Task Main(string[] args)
    {
        #region Configuration & Logging

        var configuration = InitHelpers.InitConfiguration(args);
        var loggerFactory = InitHelpers.InitLogging(configuration);
        var logger = loggerFactory.CreateLogger("ReminderAgent.Cloud");

        string openAiApiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException(
                "OpenAI:ApiKey is missing. Add it to ReminderAgent.Cloud/appsettings.dev.json.");

        #endregion

        #region Dependency Injection

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        // Single source of truth for Azure resource names (container/queue/table) — see
        // MyConfig's own doc comment. Shared with ReminderAgent.McpServer.
        services.AddSingleton<MyConfig>();
        services.AddSingleton<IDataAccessLayer, SqlDataAccessLayer>();
        services.AddSingleton<IPhotoStorageProvider, AzureBlobPhotoStorageProvider>();
        services.AddSingleton<IQueueService, AzureQueueService>();
        services.AddSingleton<ITableStatusService, AzureTableStatusService>();

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OpenAIClient(new ApiKeyCredential(openAiApiKey))
                .GetEmbeddingClient("text-embedding-3-small")
                .AsIEmbeddingGenerator());

        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<ISimilarityService, SimilarityService>();

        services.AddSingleton<IChatClient>(_ =>
            new OpenAIClient(new ApiKeyCredential(openAiApiKey))
                .GetChatClient("gpt-4o-mini")
                .AsIChatClient()
                .AsBuilder()
                .UseFunctionInvocation()
                .Build());

        services.AddSingleton<ReminderTools>();
        services.AddSingleton<BulkUploadWorker>();

        var provider = services.BuildServiceProvider();

        var dal = provider.GetRequiredService<IDataAccessLayer>();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var tools = provider.GetRequiredService<ReminderTools>();

        #endregion

        #region Background Bulk-Upload Worker

        // Background bulk-upload worker, on its own Task so it doesn't block the chat REPL.
        _ = provider.GetRequiredService<BulkUploadWorker>().RunAsync();

        #endregion

        #region Chat Tools & System Prompt

        var aiTools = new List<AITool>
        {
            AIFunctionFactory.Create(tools.CreateAsset),
            // SearchAssets is the single find/list/filter/semantic-search entry point —
            // it absorbs what used to be four separate near-duplicate tools (SearchAssets,
            // ListAssets, GetReminders, SearchRemindersSemantic).
            AIFunctionFactory.Create(tools.SearchAssets),
            AIFunctionFactory.Create(tools.AttachPhoto),
            AIFunctionFactory.Create(tools.GetAssetDetails),
            AIFunctionFactory.Create(tools.DeleteImage),
            AIFunctionFactory.Create(tools.DeleteAsset),
            AIFunctionFactory.Create(tools.BulkUploadAssets),
            AIFunctionFactory.Create(tools.GetBulkUploadStatus),
            AIFunctionFactory.Create(tools.GetRecentActivity),
        };
        var chatOptions = new ChatOptions { Tools = aiTools };

        // LLMs are unreliable at mental day-of-week arithmetic (e.g. computing "next Monday" from
        // "today is Friday" — this reliably produces off-by-one-week errors). Instead of asking the
        // model to compute relative dates, hand it a literal weekday->date lookup table for the next
        // two weeks so resolving "next Monday" / "this Friday" is a table lookup, not calculation.
        var upcomingDates = string.Join(", ", Enumerable.Range(1, 14)
            .Select(offset => DateTime.Now.Date.AddDays(offset))
            .Select(d => $"{d:dddd}={d:dd-MM-yyyy}"));

        var history = new List<ChatMessage>
        {
            new(ChatRole.System,
                $"You are ReminderAgent, an assistant that stores and recalls the user's personal " +
                "reminders (places, books, travel, contacts, and more) using the available tools. " +
                $"Today's date is {DateTime.Now:dddd} {DateTime.Now:dd-MM-yyyy}. Here is a reference " +
                $"calendar for the next 14 days, so you never have to calculate weekdays yourself — " +
                $"just look up the matching name: {upcomingDates}. " +
                "When the user mentions a relative date (e.g. 'tomorrow', 'next Monday', 'this " +
                "Friday', 'in 3 days'), find the matching date in the reference calendar above " +
                "(or add days to today's date for 'in N days') rather than computing it yourself, " +
                "and pass the absolute result to the tool as eventDate in dd-MM-yyyy format — do not " +
                "pass the relative phrase itself. " +
                "When the user gives a city but not a country (and/or region), fill in the country " +
                "(and region, if you're confident of it) yourself from your own knowledge of that " +
                "city — do not leave them blank just because the user didn't say them explicitly. " +
                "If the place name is genuinely ambiguous (e.g. a city that shares its name with its " +
                "own country, or a city name that exists in multiple countries), ask the user to " +
                "clarify instead of guessing. " +
                "This is a plain-text console — do not use Markdown formatting (no **bold**, no " +
                "[text](url) links, no bullet/numbered list markers). Write plain sentences and print " +
                "URLs as-is, e.g. 'Photo URL: https://...'.")
        };

        #endregion

        #region Chat Loop

        var startupAssets = (await dal.GetAllAssetsAsync()).ToList();
        logger.LogInformation("Connected to Azure SQL. Assets currently stored: {AssetCount}", startupAssets.Count);
        Console.WriteLine("Type a message and press Enter. Type 'exit' to quit.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;
            if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            // Captured deterministically so CreateAsset can fall back to it if the model omits
            // its own userInput argument — see ReminderTools.AmbientUserInput.
            tools.AmbientUserInput = input;

            history.Add(new ChatMessage(ChatRole.User, input));

            try
            {
                var response = await chatClient.GetResponseAsync(history, chatOptions);
                var reply = response.Text ?? "(no reply)";
                history.Add(new ChatMessage(ChatRole.Assistant, reply));

                Console.WriteLine($"Agent: {reply}");

                var currentAssets = (await dal.GetAllAssetsAsync()).ToList();
                Console.WriteLine($"[Azure SQL asset count: {currentAssets.Count}]");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling chat request");
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Goodbye.");

        #endregion
    }
}
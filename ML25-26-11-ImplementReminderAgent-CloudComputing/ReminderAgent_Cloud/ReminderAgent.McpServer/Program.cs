using Microsoft.Extensions.AI;
using OpenAI;
using ReminderAgent.Common;
using ReminderAgent.Infrastructure;
using System.ClientModel;

/// <summary>
/// The Model Context Protocol server: exposes ReminderTools over MCP (Streamable HTTP) to
/// any MCP client — no LLM lives here, the connecting client brings its own model.
/// </summary>
public class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        #region Configuration

        // appsettings.json / appsettings.{Environment}.json are already loaded (both
        // optional) by CreateBuilder. Layer the dev-secrets file on top, same
        // filename convention as ReminderAgent.Cloud.
        builder.Configuration.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);

        string openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException(
                "OpenAI:ApiKey is missing. Add it to ReminderAgent.McpServer/appsettings.dev.json.");

        #endregion

        #region Dependency Injection

        // Single source of truth for Azure resource names (container/queue/table) — see
        // MyConfig's own doc comment. Shared with ReminderAgent.Cloud.
        builder.Services.AddSingleton<MyConfig>();
        builder.Services.AddSingleton<IDataAccessLayer, SqlDataAccessLayer>();
        builder.Services.AddSingleton<IPhotoStorageProvider, AzureBlobPhotoStorageProvider>();
        builder.Services.AddSingleton<IQueueService, AzureQueueService>();
        builder.Services.AddSingleton<ITableStatusService, AzureTableStatusService>();

        // Still needed even without an embedded chat client: SearchAssets(semantic: true)
        // does its own embedding + cosine-similarity search when an MCP client calls it.
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OpenAIClient(new ApiKeyCredential(openAiApiKey))
                .GetEmbeddingClient("text-embedding-3-small")
                .AsIEmbeddingGenerator());
        builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
        builder.Services.AddSingleton<ISimilarityService, SimilarityService>();

        builder.Services.AddSingleton<ReminderTools>();

        // Shared with ReminderAgent.Cloud — started below, after the app is built.
        builder.Services.AddSingleton<BulkUploadWorker>();

        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithTools<ReminderTools>();

        #endregion

        #region App Startup

        var app = builder.Build();

        // launchSettings.json opens a browser to "/" on every dev run (launchBrowser: true).
        // Without this route that tab would just show a raw MCP protocol error, since the
        // actual MCP endpoint is /mcp, not "/" — so serve a plain status message instead.
        app.MapGet("/", () => "ReminderAgent MCP server is running. MCP endpoint: /mcp");

        app.MapMcp("/mcp");

        var dal = app.Services.GetRequiredService<IDataAccessLayer>();
        var startupAssets = (await dal.GetAllAssetsAsync()).ToList();
        app.Logger.LogInformation("Connected to Azure SQL. Assets currently stored: {AssetCount}", startupAssets.Count);
        app.Logger.LogInformation("ReminderAgent MCP server ready. MCP endpoint: /mcp");

        // Runs on its own Task so it doesn't block the MCP HTTP host below.
        _ = app.Services.GetRequiredService<BulkUploadWorker>().RunAsync();

        await app.RunAsync();

        #endregion
    }
}

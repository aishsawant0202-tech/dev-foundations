using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;
using ReminderAgent.Infrastructure;
using ReminderAgent.Interfaces;
using ReminderAgent.Tools;

using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole    = Microsoft.Extensions.AI.ChatRole;

namespace ReminderAgent
{
    /// <summary>
    /// Entry point of the ReminderAgent application.
    /// Configures dependency injection, initializes services,
    /// and runs the conversational REPL loop.
    /// </summary>
    public class Program
    {

        /// <summary>
        /// Main application entry point. Bootstraps services, configures AI tools,
        /// and starts the interactive conversation loop.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>A task representing the asynchronous execution.</returns>
        public static async Task Main(string[] args)
        {

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var builder = Host.CreateApplicationBuilder(args);

            // builder.Configuration, so no direct environment variable access is needed.
            string? apiKey = builder.Configuration["OpenAI:ApiKey"];
            string? model = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("CRITICAL ERROR: OpenAI:ApiKey is not configured.");
                Console.WriteLine("Add your key to appsettings.json under OpenAI:ApiKey,");
                Console.WriteLine("or pass it as: dotnet run --OpenAI:ApiKey=your-key-here");
                return;
            }

            // Register the shared OpenAI client used by both the chat and embedding services
            var openAIClient = new OpenAIClient(apiKey);
            builder.Services.AddSingleton(openAIClient);

            // Register GPT-4o-mini as the chat client with automatic tool/function invocation enabled
            builder.Services.AddChatClient(sp =>
            {
                var client = sp.GetRequiredService<OpenAIClient>();
                return client.GetChatClient(model).AsIChatClient();
            }).UseFunctionInvocation();
            
            // Register text-embedding-3-small for generating 1536-dim semantic vectors
            builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(sp =>
            {
                var client = sp.GetRequiredService<OpenAIClient>();
                return client.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();
            });

            // Resolve the UserData folder path by walking up to the .csproj directory
            string userDataPath = ResolveUserDataPath();

            // Register JsonEmbeddingStore — persists embedding vectors to embeddings.json
            builder.Services.AddSingleton<IEmbeddingStore>(
                new JsonEmbeddingStore(userDataPath));

            // Register CsvStorageProvider — persists asset data to reminders.csv
            builder.Services.AddSingleton<IStorageProvider>(sp =>
            {
                var embeddingStore = sp.GetRequiredService<IEmbeddingStore>();
                return new CsvStorageProvider(embeddingStore);
            });

            //Register EmbeddingService and SimilarityService
            builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
            builder.Services.AddSingleton<ISimilarityService, SimilarityService>();

            // Register ReminderTool — the tool layer injected with all four dependencies
            builder.Services.AddSingleton<ReminderTools>(sp =>
            {
                var storage    = sp.GetRequiredService<IStorageProvider>();
                var embeddings = sp.GetRequiredService<IEmbeddingService>();
                var similarity = sp.GetRequiredService<ISimilarityService>();
                var chat       = sp.GetRequiredService<IChatClient>(); 
                return new ReminderTools(storage, embeddings, similarity, chat);
            });

            using IHost host = builder.Build();
            var chatClient   = host.Services.GetRequiredService<IChatClient>();
            var reminderTool = host.Services.GetRequiredService<ReminderTools>();

            // Register all seven tool methods so GPT-4o-mini can invoke them via function calling
            ChatOptions chatOptions = new()
            {
                Tools = new List<AITool>
                {
                    AIFunctionFactory.Create(reminderTool.CreateAsset),
                    AIFunctionFactory.Create(reminderTool.GetReminders),
                    AIFunctionFactory.Create(reminderTool.SearchAssets),             
                    AIFunctionFactory.Create(reminderTool.SearchRemindersSemantic), 
                    AIFunctionFactory.Create(reminderTool.ListAssets),              
                    AIFunctionFactory.Create(reminderTool.GetAssetDetails),       
                    AIFunctionFactory.Create(reminderTool.AttachPhoto),

                }
            };

            // System prompt — defines the agent identity, tool routing rules, and strict response format
            var chatHistory = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, $"""
                    Today is {DateTime.Now:f}.
                    You are a professional Reminder Agent built with the Microsoft Agent Framework.

                    YOUR TOOLS:
                    CreateAsset
                    → Save a NEW reminder. Call EXACTLY ONCE per save. Never duplicate.
                    → Always extract city, country, region from the user's sentence.
                    Examples of inference:
                    "Vapiano in Frankfurt"       → city=Frankfurt, country=Germany, region=Hesse
                    "visited the Colosseum"      → city=Rome, country=Italy, region=Lazio
                    "hiking in the Black Forest" → country=Germany, region=Baden-Württemberg
                    Never put location in the metadata field.
                    → FOR eventDate: pass the phrase AS-IS for relative time expressions.
                    Examples:
                      "next month"       → eventDate="next month"
                      "next September"   → eventDate="next September"
                      "next summer"      → eventDate="next summer"
                      "next spring"      → eventDate="next spring" 
                      "last week"        → eventDate="last week"
                      "on 25-03-2025"    → eventDate="25-03-2025"
                    
                    GetReminders
                    → ONLY use for: "show me past reminders", "what are my future plans",
                      "list present reminders" — when user asks by Past/Present/Future state ONLY.
                    → Do NOT use this tool if the user mentions any specific time period
                      like "last week", "last month", "last year", "last 2 months".
                      Use SearchAssets with dateRange instead.
                    
                    SearchAssets
                    → Search by meaning with optional category, date range, or location filters.
                    → Examples: "restaurants in Frankfurt", "books I read last month"
                    → ALWAYS use this tool when user mentions ANY time period — past OR future:
                    Past:   "last week", "last month", "last 2 months", "last year"
                    Future: "next week", "next 2 weeks", "next month", "upcoming plans",
                            "plans for next week", "what are my plans for next 2 weeks"
                    Pass the time period as the dateRange parameter.
                    → If the user specifies a number of results like "top 3", "show 2",
                    "last week 3 reminders", extract that number and pass it as topK.
                    Examples:
                      "top 2 restaurants in Frankfurt" → topK=2, location="Frankfurt"
                      "last week 3 reminders"          → topK=3, dateRange="last week"
                      "show me 5 books"                → topK=5, category="Book"
                    → Do NOT use GetReminders for time-period queries.
                    → Examples: "what did I do last month", "last 2 months plans",
                      "restaurants last week", "what were my plans last month",
                      "last week reminders", "show me last week", "what happened last week"
                    
                    SearchRemindersSemantic
                      → Pure semantic search — no structured filters.
                    → Examples: "what did I find relaxing?", "show me inspiring things"
                    
                    ListAssets
                      → Browse all items or all items in a category.
                    → Examples: "show me all my books", "list everything I saved"
                    
                    GetAssetDetails
                      → Full details for ONE specific item by name.
                    → Examples: "tell me everything about Vapiano"
                    
                    AttachPhoto
                    → Add a photo reference to an EXISTING saved reminder.
                    → Call when user says: "attach a photo", "add a picture", "save this image",
                      "link a photo to X", "add this photo to my X reminder".
                    → The photoReference MUST be a relative path explicitly typed by the user,
                      e.g., "Photos/paris.jpg" or "Photos\sarvana-bhavan.jpg".
                    → NEVER invent, guess, or generate a photoReference yourself.
                      If the user did NOT provide a file path in their message, DO NOT call
                      AttachPhoto. Instead reply asking: "Please provide the photo file path,
                      e.g., Photos/filename.jpg"
                    → NEVER call CreateAsset to attach a photo to an existing reminder.
                      Only call AttachPhoto for this purpose.
                    → If the asset name is ambiguous, AttachPhoto will ask for clarification.
                    STRICT RULES 

                    1. ALWAYS call a tool for any retrieval or search — never answer from memory.
                    2. CreateAsset: call EXACTLY ONCE. Never re-save existing items.
                    3. CreateAsset: ALWAYS extract city/country/region. Never put location in metadata.
                    4. SearchAssets: pass location param when user mentions a place name.
                    5. GetAssetDetails: if not found, report clearly and offer to save.
                    6. Never invent or assume data — only return what tools provide.
                    7. Be conversational, friendly, and concise.
                    8. CRITICAL: When a tool returns a response, relay it to the user EXACTLY as-is —
                       word for word, with zero reformatting. Do NOT rewrite, restructure, summarise,
                       or add bullet points, numbered lists, or bold labels. The tool response is
                       already formatted for the user. Your only job is to pass it through unchanged.
                    9. Never use markdown image syntax like ![name](path) in responses.
                       When mentioning a photo path, write it as plain text only.
                       Example: write  Photos/Vapiano-Restaurant.jpg  not  ![Vapiano](Photos/Vapiano-Restaurant.jpg)
                    """)
            };

            Console.WriteLine("---Reminder Agent Active --- \n  Hi, there! \n");
            Console.WriteLine("Type 'exit' to stop.\n");

            // REPL loop — reads user input, calls the agent, and prints the reply
            while (true)
            {
                Console.Write("User: ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
                    break;

                chatHistory.Add(new ChatMessage(ChatRole.User, input));

                try
                {
                    var response = await chatClient.GetResponseAsync(chatHistory, new ChatOptions
                    {
                        Tools    = chatOptions.Tools,
                        ToolMode = ChatToolMode.Auto
                    });

                    string agentReply;
                    var toolMessage = response.Messages
                        .SelectMany(m => m.Contents)
                        .OfType<FunctionResultContent>()
                        .FirstOrDefault();

                    if (toolMessage != null && toolMessage.Result is string toolResult
                        && !string.IsNullOrWhiteSpace(toolResult))
                    {
                        // Tool returned a RAG-formatted response — use it directly
                        agentReply = toolResult;
                    }
                    else
                    {
                        agentReply = response.Text ?? "I have handled that for you.";
                    }

                    Console.WriteLine($"\nAgent: {agentReply}\n");
                    chatHistory.Add(new ChatMessage(ChatRole.Assistant, agentReply));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR]: {ex.Message}");
                    FileLogger.Error($"Main loop exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Resolves the path to the UserData directory by traversing up the directory tree
        /// to locate the project root (.csproj file).
        /// </summary>
        /// <returns>
        /// The absolute path to the UserData directory. Falls back to the base directory
        /// if the project root cannot be found.
        /// </returns>
        private static string ResolveUserDataPath()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < 10; i++)
            {
                if (Directory.GetFiles(current, "*.csproj").Length > 0)
                    return Path.Combine(current, "UserData");

                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            FileLogger.Warning("ResolveUserDataPath | No .csproj found. Using BaseDirectory fallback.");

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
        }
    }
}

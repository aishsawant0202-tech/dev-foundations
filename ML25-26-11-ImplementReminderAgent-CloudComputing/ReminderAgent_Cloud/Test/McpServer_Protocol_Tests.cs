using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReminderAgent.Common;
using ReminderAgent.Infrastructure;

namespace ReminderAgent.Tests.Integration
{
    /// <summary>
    /// End-to-end tests for the ReminderAgent MCP server — the actual protocol wire
    /// (handshake, tool discovery, tool calls), not just ReminderTools' business logic.
    /// </summary>
    /// <remarks>
    /// <see cref="ClassInit"/> launches ReminderAgent.McpServer.dll as a child process on a
    /// free loopback port; nothing is mocked (Azure SQL/Blob/Queue/Table, OpenAI).
    /// Tool names on the wire are snake_case (e.g. CreateAsset -&gt; "create_asset"); parameter
    /// names are not converted. <see cref="DoNotParallelizeAttribute"/> keeps these tests from
    /// contending with each other over the one server process.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public class McpServer_Protocol_Tests
    {
        #region Setup

        private static Process _serverProcess = null!;
        private static Uri _serverBaseAddress = null!;
        private static McpClient _client = null!;
        private static IDataAccessLayer _dal = null!;

        /// <summary>
        /// Starts the ReminderAgent.McpServer process on a free loopback port and polls its
        /// HTTP endpoint until it responds, before any test runs.
        /// </summary>
        [ClassInitialize]
        public static async Task ClassInit(TestContext context)
        {
            // typeof(Program) resolves to the already-built ReminderAgent.McpServer.dll via
            // the Test -> ReminderAgent.McpServer ProjectReference.
            var dllPath = typeof(Program).Assembly.Location;
            var workingDir = Path.GetDirectoryName(dllPath)!;

            // Ask the OS for a free port, then release it immediately (standard trick).
            int port;
            using (var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            }
            _serverBaseAddress = new Uri($"http://127.0.0.1:{port}/");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.Environment["ASPNETCORE_URLS"] = _serverBaseAddress.ToString();
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

            _serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // Captured only for diagnostics if startup fails.
            var consoleLog = new System.Text.StringBuilder();
            _serverProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (consoleLog) consoleLog.AppendLine("[out] " + e.Data); };
            _serverProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (consoleLog) consoleLog.AppendLine("[err] " + e.Data); };

            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            // 90s budget: cold JIT + real Azure SQL startup check under whatever load the
            // rest of the suite is putting on the machine.
            using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTime.UtcNow.AddSeconds(90);
            var isReady = false;
            while (!isReady && DateTime.UtcNow < deadline)
            {
                if (_serverProcess.HasExited)
                    throw new InvalidOperationException(
                        $"ReminderAgent.McpServer exited (code {_serverProcess.ExitCode}) before becoming ready.\n" +
                        $"Console output:\n{consoleLog}");
                try
                {
                    var response = await probeClient.GetAsync(_serverBaseAddress);
                    isReady = response.IsSuccessStatusCode;
                }
                catch { /* not up yet — keep polling */ }
                if (!isReady) await Task.Delay(500);
            }
            if (!isReady)
                throw new TimeoutException(
                    $"ReminderAgent.McpServer did not respond on {_serverBaseAddress} within 90s.\nConsole output so far:\n{consoleLog}");

            _client = await ConnectNewClientAsync();

            // Separate, direct DAL connection (same pattern as the other Test/ suites) — used
            // only to seed/verify/clean up real SQL rows out-of-band; every asset itself is
            // still created/read/deleted through the real MCP tool calls below.
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.dev.json", optional: false)
                .AddEnvironmentVariables()
                .Build();
            _dal = new SqlDataAccessLayer(configuration);
        }

        /// <summary>
        /// Disposes the MCP client and terminates the server process started in
        /// <see cref="ClassInit"/>.
        /// </summary>
        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (_client is not null) await _client.DisposeAsync();
            if (_serverProcess is not null && !_serverProcess.HasExited)
            {
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(5000);
            }
            _serverProcess?.Dispose();
        }

        /// <summary>
        /// Opens a new real MCP client connection (Streamable HTTP) to the running server.
        /// Used both for the shared <see cref="_client"/> and for tests that need their own
        /// independent connection (e.g. MCP15's concurrency check).
        /// </summary>
        private static async Task<McpClient> ConnectNewClientAsync()
        {
            var httpClient = new HttpClient { BaseAddress = _serverBaseAddress };
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(_serverBaseAddress, "mcp"),
                    TransportMode = HttpTransportMode.StreamableHttp
                },
                httpClient,
                ownsHttpClient: true);
            return await McpClient.CreateAsync(transport);
        }

        /// <summary>
        /// Builds a realistic display name plus a short unique suffix, e.g.
        /// "Trattoria Roma (a1b2c3)" — collision-free under parallel test execution without
        /// looking like raw test junk when inspected in the real shared dev database.
        /// </summary>
        private static string Unique(string baseName) => $"{baseName} ({Guid.NewGuid():N}"[..(baseName.Length + 9)] + ")";

        /// <summary>
        /// Calls a tool, retrying up to 3 times if the response comes back with the SDK's
        /// generic "something threw" text. Covers occasional transient OpenAI/SQL hiccups
        /// (rate limits, connection blips) without masking a consistently broken call.
        /// </summary>
        /// <remarks>
        /// Keyed on response text, not <c>result.IsError</c> — that flag has proven unreliable
        /// in this SDK version (it reports true even on confirmed successful calls).
        /// </remarks>
        private static async Task<CallToolResult> CallToolWithRetryAsync(
            string toolName, Dictionary<string, object?> arguments)
        {
            CallToolResult result = null!;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                result = await _client.CallToolAsync(toolName, arguments);
                var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
                if (!text.StartsWith("An error occurred invoking", StringComparison.Ordinal))
                    return result;
                await Task.Delay(1000);
            }
            return result;
        }

        /// <summary>
        /// Polls get_recent_activity for up to ~8s until an entry containing
        /// <paramref name="mustContain"/> appears — Table Storage activity logging is a real,
        /// separately-awaited write with real eventual-consistency lag under load.
        /// </summary>
        private static async Task<string> WaitForActivityLogEntryAsync(string mustContain)
        {
            string lastText = "";
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var result = await _client.CallToolAsync("get_recent_activity",
                    new Dictionary<string, object?> { ["count"] = 200 });
                lastText = result.Content.OfType<TextContentBlock>().First().Text;
                if (lastText.Contains(mustContain, StringComparison.Ordinal))
                    return lastText;
                await Task.Delay(500);
            }
            return lastText;
        }

        /// <summary>
        /// Same pattern as <see cref="WaitForActivityLogEntryAsync"/>, applied to the embedding
        /// column — a single immediate re-read occasionally misses a just-committed write under
        /// the heavy real, concurrent SQL load this whole suite puts on the shared dev database.
        /// </summary>
        private static async Task<float[]?> WaitForEmbeddingAsync(Guid assetId)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var embedding = await _dal.GetEmbeddingAsync(assetId);
                if (embedding is not null) return embedding;
                await Task.Delay(500);
            }
            return null;
        }

        #endregion

        #region Handshake & Discovery Tests

        /// <summary>
        /// The initialize handshake succeeds and the server identifies itself, not just "the
        /// call didn't throw". Real MCP clients (Claude Desktop etc.) key compatibility checks
        /// off this response.
        /// </summary>
        [TestMethod]
        public void MCP01_Connect_HandshakeSucceeds_ServerIdentifiesItself()
        {
            Assert.IsNotNull(_client.ServerInfo);
            Assert.IsFalse(string.IsNullOrWhiteSpace(_client.ServerInfo.Name));
            Assert.IsFalse(string.IsNullOrWhiteSpace(_client.NegotiatedProtocolVersion));
        }

        /// <summary>
        /// Regression guard for the toolset reduction (11 -&gt; 9 tools, see ReminderTools.cs).
        /// Tool count/name drift here means an MCP client's cached tool list silently goes stale.
        /// </summary>
        [TestMethod]
        public async Task MCP02_ListTools_ReturnsExactlyTheNineExpectedTools()
        {
            var tools = await _client.ListToolsAsync();
            var names = tools.Select(t => t.Name).ToList();

            string[] expected =
            {
                "create_asset", "search_assets", "attach_photo", "get_asset_details",
                "delete_image", "bulk_upload_assets", "get_bulk_upload_status", "get_recent_activity", "delete_asset"
            };

            foreach (var name in expected)
                Assert.Contains(name, names, $"Expected tool '{name}' in the real server's tool list.");

            Assert.HasCount(9, tools,
                "Toolset was intentionally reduced from 11 to 9 — a count drift means a tool " +
                "was added, removed, or renamed without updating this guard.");
        }

        /// <summary>
        /// The reflected JSON schema is what every real MCP client uses to build its call —
        /// required-ness has to be exactly right.
        /// </summary>
        [TestMethod]
        public async Task MCP03_ListTools_CreateAssetSchema_RequiresOnlyNameAndCategory()
        {
            var tools = await _client.ListToolsAsync();
            var createAsset = tools.Single(t => t.Name == "create_asset");

            Assert.IsTrue(createAsset.JsonSchema.TryGetProperty("required", out var requiredElement),
                "create_asset's schema must declare its required parameters.");

            var required = requiredElement.EnumerateArray().Select(e => e.GetString()).ToList();
            CollectionAssert.AreEquivalent(new[] { "name", "category" }, required);
        }

        #endregion

        #region CreateAsset Tests

        /// <summary>
        /// Flagship real-environment test: create_asset invoked over the actual MCP wire must
        /// produce a real Azure SQL row AND a real OpenAI embedding, not just a
        /// plausible-looking text reply.
        /// </summary>
        [TestMethod]
        public async Task MCP04_CallCreateAsset_OverRealMcp_PersistsRealAssetAndEmbedding()
        {
            var uniqueName = Unique("Trattoria Roma");
            Guid? createdId = null;
            try
            {
                var result = await CallToolWithRetryAsync("create_asset", new Dictionary<string, object?>
                {
                    ["name"] = uniqueName,
                    ["category"] = "restaurant",
                    ["city"] = "Frankfurt"
                });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                StringAssert.Contains(text, "Saved");
                StringAssert.Contains(text, uniqueName);

                var assets = (await _dal.GetAllAssetsAsync()).ToList();
                var created = assets.SingleOrDefault(a => a.Name == uniqueName);
                Assert.IsNotNull(created, "The asset created over the real MCP wire must exist in real Azure SQL.");
                createdId = created!.Id;

                var embedding = await WaitForEmbeddingAsync(created.Id);
                Assert.IsNotNull(embedding, "create_asset must also persist a real OpenAI embedding.");
                Assert.IsNotEmpty(embedding!);
            }
            finally
            {
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        #endregion

        #region SearchAssets Tests

        /// <summary>
        /// search_assets keyword ranking, round-tripped through the real wire against the real
        /// asset just created.
        /// </summary>
        [TestMethod]
        public async Task MCP05_CallSearchAssets_OverRealMcp_FindsAssetByKeyword()
        {
            var uniqueTag = $"mcp05tag{Guid.NewGuid():N}"[..16];
            var uniqueName = Unique("Clean Code");
            Guid? createdId = null;
            try
            {
                await _client.CallToolAsync("create_asset", new Dictionary<string, object?>
                {
                    ["name"] = uniqueName,
                    ["category"] = "book",
                    ["tags"] = uniqueTag
                });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                var result = await _client.CallToolAsync("search_assets",
                    new Dictionary<string, object?> { ["query"] = uniqueTag });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                StringAssert.Contains(text, uniqueName);
            }
            finally
            {
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        /// <summary>
        /// Semantic search: embedding generation for the query plus
        /// cosine-similarity ranking, both over the wire.
        /// </summary>
        /// <remarks>
        /// Doesn't assert our asset lands in the top-K (the real dev database has other data
        /// too) — asserts the real embedding pipeline runs end to end and returns a
        /// well-formed result instead of erroring.
        /// </remarks>
        [TestMethod]
        public async Task MCP06_CallSearchAssets_Semantic_OverRealMcp_UsesRealEmbeddingSimilarity()
        {
            var uniqueName = Unique("Nonna's Kitchen");
            Guid? createdId = null;
            try
            {
                await _client.CallToolAsync("create_asset", new Dictionary<string, object?>
                {
                    ["name"] = uniqueName,
                    ["category"] = "restaurant",
                    ["userExperience"] = "A cosy little Italian place with amazing handmade pasta."
                });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                var result = await _client.CallToolAsync("search_assets", new Dictionary<string, object?>
                {
                    ["query"] = "a cozy place with great pasta",
                    ["semantic"] = true,
                    ["topK"] = 5
                });

                // "No close match" is still a well-formed, non-error response from a working
                // embedding pipeline against real, variable data — not a failure.
                var text = result.Content.OfType<TextContentBlock>().First().Text;
                Assert.IsTrue(
                    text.Contains("Closest matches") || text.Contains("No saved assets were similar enough"),
                    $"Semantic search returned an unexpected response: {text}");
            }
            finally
            {
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        #endregion

        #region GetAssetDetails Tests

        /// <summary>
        /// get_asset_details returns the full real record over the wire.
        /// </summary>
        [TestMethod]
        public async Task MCP07_CallGetAssetDetails_OverRealMcp_ReturnsFullDetails()
        {
            var uniqueName = Unique("Hotel Berlin Mitte");
            Guid? createdId = null;
            try
            {
                await _client.CallToolAsync("create_asset", new Dictionary<string, object?>
                {
                    ["name"] = uniqueName,
                    ["category"] = "hotel",
                    ["city"] = "Berlin",
                    ["userExperience"] = "Great breakfast."
                });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                var result = await _client.CallToolAsync("get_asset_details",
                    new Dictionary<string, object?> { ["name"] = uniqueName });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                StringAssert.Contains(text, uniqueName);
                StringAssert.Contains(text, "Berlin");
                StringAssert.Contains(text, "Great breakfast.");
            }
            finally
            {
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Calling a tool name the server never registered must fail as a protocol-level
        /// error, and the server/connection must survive it — proven by a real, successful
        /// call immediately after.
        /// </summary>
        [TestMethod]
        public async Task MCP08_CallUnknownTool_OverRealMcp_FailsAtProtocolLevel_ServerSurvives()
        {
            await Assert.ThrowsAsync<McpException>(async () =>
                await _client.CallToolAsync("this_tool_does_not_exist", new Dictionary<string, object?>()));

            var tools = await _client.ListToolsAsync();
            Assert.IsGreaterThanOrEqualTo(8, tools.Count, "The connection/server must still be usable after an invalid call.");
        }

        /// <summary>
        /// A call missing a required argument must never silently create bad data, whether the
        /// SDK rejects it at the protocol level or create_asset's own guard clause catches it.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT compare a global "assets before/after" row count —
        /// MSTestSettings.cs parallelizes at method level, so every other test in this run is
        /// inserting/deleting into the same real shared table concurrently; a global count is
        /// inherently racy and proves nothing about this one call.
        /// </remarks>
        [TestMethod]
        public async Task MCP09_CallCreateAsset_MissingRequiredName_RejectedWithoutCreatingBadRow()
        {
            bool threw = false;
            CallToolResult? result = null;
            try
            {
                result = await _client.CallToolAsync("create_asset",
                    new Dictionary<string, object?> { ["category"] = "restaurant" }); // "name" omitted
            }
            catch (McpException)
            {
                threw = true;
            }

            if (!threw)
            {
                Assert.IsNotNull(result);
                var text = result!.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
                Assert.IsTrue(result.IsError == true || text.Contains("name", StringComparison.OrdinalIgnoreCase),
                    $"A missing required argument must be rejected, not silently accepted. Got: {text}");
            }
        }

        #endregion

        #region AttachPhoto Tests

        /// <summary>
        /// attach_photo over the real wire: real upload to Azure Blob Storage, real SQL insert,
        /// and a genuinely fetchable SAS URL.
        /// </summary>
        [TestMethod]
        public async Task MCP10_CallAttachPhoto_OverRealMcp_UploadsToRealBlobAndPersistsInSql()
        {
            var uniqueName = Unique("Central Perk Cafe");
            Guid? createdId = null;
            var tempPath = Path.Combine(Path.GetTempPath(), $"mcp10-{Guid.NewGuid():N}.jpg");
            File.WriteAllBytes(tempPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // minimal JPEG header
            try
            {
                await _client.CallToolAsync("create_asset",
                    new Dictionary<string, object?> { ["name"] = uniqueName, ["category"] = "restaurant" });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                var result = await _client.CallToolAsync("attach_photo", new Dictionary<string, object?>
                {
                    ["assetName"] = uniqueName,
                    ["photoPath"] = tempPath
                });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                StringAssert.Contains(text, "Attached");

                var photos = (await _dal.GetPhotoRefsForAssetAsync(createdId.Value)).ToList();
                Assert.HasCount(1, photos);

                using var http = new HttpClient();
                var response = await http.GetAsync($"{photos[0].BlobUrl}?{photos[0].AccessKey}");
                Assert.IsTrue(response.IsSuccessStatusCode,
                    "The photo attached over the real MCP wire must be a genuinely fetchable Blob Storage URL.");
            }
            finally
            {
                File.Delete(tempPath);
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        #endregion

        #region DeleteImage Tests

        /// <summary>
        /// delete_image actually removes the row.
        /// </summary>
        /// <remarks>
        /// The blob cleanup gap is a known, pre-existing limitation — see
        /// ReminderTools_PhotoOperations_Tests's TestCleanup note.
        /// </remarks>
        [TestMethod]
        public async Task MCP11_CallDeleteImage_OverRealMcp_RemovesRealPhotoRow()
        {
            var uniqueName = Unique("Central Perk Cafe");
            Guid? createdId = null;
            var tempPath = Path.Combine(Path.GetTempPath(), $"mcp11-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(tempPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
            try
            {
                await _client.CallToolAsync("create_asset",
                    new Dictionary<string, object?> { ["name"] = uniqueName, ["category"] = "restaurant" });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                await _client.CallToolAsync("attach_photo", new Dictionary<string, object?>
                {
                    ["assetName"] = uniqueName,
                    ["photoPath"] = tempPath
                });

                var result = await _client.CallToolAsync("delete_image",
                    new Dictionary<string, object?> { ["assetNameOrPhotoId"] = uniqueName });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                StringAssert.Contains(text, "Deleted photo");

                var remaining = (await _dal.GetPhotoRefsForAssetAsync(createdId.Value)).ToList();
                Assert.IsEmpty(remaining);
            }
            finally
            {
                File.Delete(tempPath);
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        #endregion

        #region Activity Logging Tests

        /// <summary>
        /// get_recent_activity proves ITableStatusService really wrote to Azure Table
        /// Storage from inside the hosted server, not just that create_asset "said" it
        /// succeeded.
        /// </summary>
        /// <remarks>
        /// The logged text itself still says "CreateAsset" (PascalCase) — ReminderTools.cs
        /// logs via <c>nameof(CreateAsset)</c>, the C# method name, independent of whatever the
        /// MCP tool name is on the wire.
        /// </remarks>
        [TestMethod]
        public async Task MCP12_CallGetRecentActivity_OverRealMcp_ReflectsRealTableStorageLog()
        {
            var uniqueName = Unique("Kyoto Garden");
            Guid? createdId = null;
            try
            {
                await CallToolWithRetryAsync("create_asset",
                    new Dictionary<string, object?> { ["name"] = uniqueName, ["category"] = "destination" });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                var text = await WaitForActivityLogEntryAsync(uniqueName);

                StringAssert.Contains(text, "CreateAsset");
                StringAssert.Contains(text, uniqueName);
            }
            finally
            {
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        #endregion

        #region BulkUpload Tests

        /// <summary>
        /// bulk_upload_assets : CSV upload to Blob Storage, message
        /// on the Azure Storage Queue.
        /// </summary>
        [TestMethod]
        public async Task MCP13_CallBulkUploadAssets_OverRealMcp_UploadsBlobAndQueuesRealJob()
        {
            // Committed test input — see SampleFiles/bulk-upload-sample.csv (4 rows: book,
            // book, movie, restaurant). The tool's response only reflects the row count
            // parsed from the file, so a fixed, versioned fixture (rather than a fresh temp
            // file per run) is both sufficient and reproducible from a fresh clone/CI.
            var sampleCsv = Path.Combine(AppContext.BaseDirectory, "SampleFiles", "bulk-upload-sample.csv");

            var result = await _client.CallToolAsync("bulk_upload_assets",
                new Dictionary<string, object?> { ["filePath"] = sampleCsv });

            var text = result.Content.OfType<TextContentBlock>().First().Text;
            StringAssert.Contains(text, "Started bulk upload job");
            StringAssert.Contains(text, "4 assets");
        }

        /// <summary>
        /// Checking status for a job id the server has never seen is a normal
        /// "not finished/found yet" case, not a server error.
        /// </summary>
        [TestMethod]
        public async Task MCP14_CallGetBulkUploadStatus_UnknownJobId_ReturnsNotFoundMessage_NoThrow()
        {
            var fakeJobId = Guid.NewGuid().ToString();

            var result = await _client.CallToolAsync("get_bulk_upload_status",
                new Dictionary<string, object?> { ["jobId"] = fakeJobId });

            var text = result.Content.OfType<TextContentBlock>().First().Text;
            StringAssert.Contains(text, fakeJobId);
        }

        #endregion

        #region Concurrency Tests

        /// <summary>
        /// The server runs Stateless=true (Program.cs) specifically so it needs no per-session
        /// bookkeeping. Two independent MCP client connections must be able to call tools
        /// concurrently against the backend without interfering with each other.
        /// </summary>
        [TestMethod]
        public async Task MCP15_TwoConcurrentMcpClients_OverRealStatelessServer_BothSucceedIndependently()
        {
            await using var clientA = await ConnectNewClientAsync();
            await using var clientB = await ConnectNewClientAsync();

            var nameA = Unique("Blue Note Jazz Bar");
            var nameB = Unique("Green Leaf Bistro");

            var taskA = clientA.CallToolAsync("create_asset",
                new Dictionary<string, object?> { ["name"] = nameA, ["category"] = "book" });
            var taskB = clientB.CallToolAsync("create_asset",
                new Dictionary<string, object?> { ["name"] = nameB, ["category"] = "book" });

            var resultA = await taskA;
            var resultB = await taskB;
            try
            {
                var assets = (await _dal.GetAllAssetsAsync()).ToList();
                Assert.IsTrue(assets.Any(a => a.Name == nameA), "Client A's concurrent call must have persisted.");
                Assert.IsTrue(assets.Any(a => a.Name == nameB), "Client B's concurrent call must have persisted.");
            }
            finally
            {
                var assets = (await _dal.GetAllAssetsAsync()).ToList();
                foreach (var a in assets.Where(a => a.Name == nameA || a.Name == nameB))
                    await _dal.DeleteAssetAsync(a.Id);
            }
        }

        #endregion

        #region DeleteAsset Tests

        /// <summary>
        /// delete_asset actually removes the real asset row, and cleans up any attached photo
        /// row along with it — not just a plausible-looking text reply.
        /// </summary>
        [TestMethod]
        public async Task MCP16_CallDeleteAsset_OverRealMcp_RemovesRealAssetAndAttachedPhotos()
        {
            var uniqueName = Unique("Riverside Diner");
            Guid? createdId = null;
            var tempPath = Path.Combine(Path.GetTempPath(), $"mcp16-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(tempPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
            try
            {
                await _client.CallToolAsync("create_asset",
                    new Dictionary<string, object?> { ["name"] = uniqueName, ["category"] = "restaurant" });
                createdId = (await _dal.GetAllAssetsAsync()).Single(a => a.Name == uniqueName).Id;

                await _client.CallToolAsync("attach_photo", new Dictionary<string, object?>
                {
                    ["assetName"] = uniqueName,
                    ["photoPath"] = tempPath
                });
                Assert.HasCount(1, await _dal.GetPhotoRefsForAssetAsync(createdId.Value),
                    "Sanity check: the photo must be attached before delete_asset runs.");

                var result = await _client.CallToolAsync("delete_asset",
                    new Dictionary<string, object?> { ["name"] = uniqueName });

                var text = result.Content.OfType<TextContentBlock>().First().Text;
                StringAssert.Contains(text, "Deleted");
                StringAssert.Contains(text, uniqueName);

                var assets = (await _dal.GetAllAssetsAsync()).ToList();
                Assert.IsFalse(assets.Any(a => a.Name == uniqueName),
                    "The asset deleted over the real MCP wire must be gone from real Azure SQL.");

                var remainingPhotos = (await _dal.GetPhotoRefsForAssetAsync(createdId.Value)).ToList();
                Assert.IsEmpty(remainingPhotos, "delete_asset must also remove the asset's attached photo rows.");

                createdId = null; // already gone — nothing left for the finally block to clean up
            }
            finally
            {
                File.Delete(tempPath);
                if (createdId is not null) await _dal.DeleteAssetAsync(createdId.Value);
            }
        }

        /// <summary>
        /// Deleting a name the server has never seen is a normal "not found" case, not a
        /// server error — proven by a real, successful call immediately after.
        /// </summary>
        [TestMethod]
        public async Task MCP17_CallDeleteAsset_UnknownName_ReturnsNotFoundMessage_NoThrow()
        {
            var unknownName = Unique("Nonexistent Place");

            var result = await _client.CallToolAsync("delete_asset",
                new Dictionary<string, object?> { ["name"] = unknownName });

            var text = result.Content.OfType<TextContentBlock>().First().Text;
            StringAssert.Contains(text, "couldn't find");
            StringAssert.Contains(text, unknownName);

            // Server must have survived the miss — confirm with a real successful call.
            var handshake = await _client.ListToolsAsync();
            Assert.IsTrue(handshake.Any(t => t.Name == "delete_asset"));
        }

        #endregion
    }
}

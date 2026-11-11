# PROMPTS.md - AI Usage Log
This document describes how Claude (Anthropic) was used as a coding and debugging assistant throughout multiple stages of this project. It documents the specific areas of implementation that were supported by AI, the rationale for using AI assistance at each stage, and how the generated outputs were subsequently reviewed, corrected, and validated by the development team. AI interactions were carried out using two interfaces: the Claude.ai chat interface, used by Kavya and Bavana, and Claude Code (CLI), used by Aishwarya with live access to the local repository. The assistant was used as a supportive tool for understanding system design, identifying implementation gaps, debugging runtime issues, and refining architectural decisions. While Claude contributed to rapid problem-solving and design exploration, all final implementations were verified and adjusted by the team to ensure correctness, consistency, and alignment with project requirements.
#
# AI Usage Table

## Main Files Implemented

| # | File | Prompt (summary) | Model | AI Output | Link |
|---|------|-------------------|-------|-----------|------|
| 1 | Program.cs — MCP server wiring | Build a real MCP server exposing tools over Streamable HTTP, stateless transport (not just a Web API named "mcp") | Claude Sonnet 4.5 (Claude Code CLI) | `AddMcpServer().WithHttpTransport().WithTools<ReminderTools>()` registration, `[Description]` attributes for tool discovery | used CLAUDE **CLI** |
| 2 | ReminderTools.cs | Design and implement the MCP tool methods (CreateAsset, SearchAssets, GetAssetDetails, etc.) exposed to the model | Claude Sonnet 4.5 (claude.ai) | ReminderTools class with `[McpServerTool]`/`[Description]`-decorated methods, calling into IDataAccessLayer | https://claude.ai/share/4b237a75-292a-45a5-a9d6-bfaede86bb50 |
| 3 | AzureQueueService.cs — Queue | Queue Storage service for async bulk-upload jobs | Claude Sonnet 4.5 (claude.ai) | IQueueService implementation, enqueue on MCP call, background worker polls every 5s | https://claude.ai/share/5ea46854-32b1-4890-af89-4da378a2fba7 |
| 4 | schema.sql + SqlDataAccessLayer.cs — SQL Database | Design Azure SQL schema (Assets, Photos tables) and implement CRUD against IDataAccessLayer to match it | Claude Sonnet 4.5 (claude.ai) | 2-table schema.sql (string Country column, Photos linked via AssetId) + full CRUD implementation in SqlDataAccessLayer.cs | https://claude.ai/share/77913111-6a1b-463a-b46e-a749210506a3 |
| 5 | AzureBlobPhotoStorageProvider.cs — Blob Storage | Implement IPhotoStorageProvider against Azure Blob Storage for photo upload/retrieval | Claude Sonnet 4.5 (claude.ai) | Blob upload/download implementation, container-based storage | https://claude.ai/share/a29f7ddb-cd3e-44b9-88a8-fd048b61c6e6 |
| 6 | ReminderTools.cs — AttachPhoto / DeletePhoto | Add tools to attach a photo to a saved asset and to remove a specific attached photo | Claude Sonnet 4.5 (claude.ai) | AttachPhoto method (validates and links photo reference) + DeletePhoto/DeleteImage method (resolves and removes target photo) | https://claude.ai/share/a29f7ddb-cd3e-44b9-88a8-fd048b61c6e6 |

## Issues We Faced

| # | Issue | Prompt (summary) | Model | AI Output | Link |
|---|-------|-------------------|-------|-----------|------|
| 1 | Logging infrastructure built from an empty stub, then scaled back | InitHelpers.cs was empty — implement logging based on a reference project; later, disable console output once debug-window logs were sufficient | Claude Sonnet 4.5 (Claude Code CLI) | Logging setup implemented, iterated on scope formatting, console logging later removed | used CLAUDE **CLI** |
| 2 | Attached-photo deletion removed every photo instead of one | Deleting one specific image deleted all attached images | Claude Sonnet 4.5 (Claude Code CLI) | Fixed to target and remove only the specified photo |https://claude.ai/share/a29f7ddb-cd3e-44b9-88a8-fd048b61c6e6 |
| 3 | Built as a plain Web API first, not actually MCP | McpServer project was just a Web API named "mcp" — needed to be rebuilt as a real ModelContextProtocol server | Claude Sonnet 4.5 (Claude Code CLI) | Migrated to real MCP implementation over HTTP; clarified no client-side Claude dependency needed for other users | used CLAUDE **CLI** |
| 4 | MCP toolset had 11 tools, several near-duplicates | 11 registered tools showing in Copilot, several redundant | Claude Sonnet 4.5 (Claude Code CLI) | Consolidated near-duplicate tools down to fewer, non-overlapping tools | used CLAUDE **CLI** |
| 5 | Azure MCP integration test suite: timeouts and wrong-assertion failures | Pasted raw test failure output — SQL connection timeouts, assertion mismatches on "unknown job id" and "valid upload" cases | Claude Sonnet 4.5 (Claude Code CLI) | Diagnosed timeout vs. real failure, corrected assertions to match expected non-error behavior | https://claude.ai/share/ee23de4e-c4fd-48b5-b952-26aa1cc461fc |
| 6 | "Update date logic": startup crash, wrong event dates, MCP couldn't answer "tomorrow" | JSON parse crash on startup; event dates not saving; MCP not calling SearchAssets correctly for relative-date queries | Claude Sonnet 4.5 (Claude Code CLI) | Fixed JSON parsing crash, event-date persistence, and date-filter tool-call routing for relative dates | https://claude.ai/share/a29f7ddb-cd3e-44b9-88a8-fd048b61c6e6 |
| 7 | Bulk uploads queued via MCP server never completed | Queue upload not working when triggered through the MCP server | Claude Sonnet 4.5 (Claude Code CLI) | Identified missing worker loop in McpServer's Program.cs vs. Cloud's Program.cs | https://claude.ai/share/ee23de4e-c4fd-48b5-b952-26aa1cc461fc |
| 8 | System-architecture diagrams (Artifact + `.drawio` files) | Design workflow diagram and subdiagrams, reviewed for clarity as if a professor were seeing it for the first time | Claude Sonnet 4.5 (claude.ai) | Iterated architecture diagram (overview + component subdiagrams) via Artifacts | https://claude.ai/code/artifact/c3cb1c50-1235-49ed-b84e-5f8bf9072f45 |  

## MCP Tools are conntected from Claude.ai
1. https://claude.ai/share/d17363e8-cc85-42f2-8ff2-0eb5908692d1
---

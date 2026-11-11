# ML25/26-11 Implement Reminder Agent — Cloud Computing

**Team:** TechTrio — ReminderAgent

**Module:** Cloud Computing, Software Engineering — Frankfurt University of Applied Sciences

ReminderAgent is a conversational AI system built in C# on the Microsoft Agent Framework. It accepts free-form natural language to save and retrieve personal assets (restaurants, books, travel, experiences) using GPT-4o-mini for intent routing and OpenAI `text-embedding-3-small` for semantic search.

This repository contains the **cloud migration** of ReminderAgent to Microsoft Azure.

---

## Project Overview

In the Software Engineering project, ReminderAgent was built as a **local .NET console application** on the **Microsoft Agent Framework (MAF)**. It accepted free-form natural language for both saving and retrieving personal assets such as restaurants, books, travel ideas, and experiences. Reminders were stored in a **`reminders.csv`** file, embeddings in an **`embeddings.json`** sidecar, and photos in a local **`Photos/`** folder, all on the developer's machine. The application had 7 tool functions and ran on a single machine with no network access.

This cloud migration keeps the same natural language experience and upgrades every local component to a cloud-hosted equivalent, moving from flat files to **Azure SQL Database**, from a local folder to **Azure Blob Storage**, and deploying the application as a **Docker container on Azure App Service** so any MCP-compatible AI client can connect over the network without cloning the repository.

---
## How This Project Works

A user sends a natural language message to an AI client such as GitHub Copilot Chat, Claude Desktop, Cursor, or VS Code. The AI client reads the available tools from the MCP Server, selects the right one, extracts the arguments from the message, and sends a structured tool call over HTTPS to the deployed MCP Server on Azure App Service. The server looks up the tool and executes it in-process. The tool reads from or writes to the Azure cloud services and returns a plain text result back to the AI client, which shows it to the user.

The user never sees any of the infrastructure. They just type in natural language and get a response.

### Step by Step

```
User types:  "Save Vapiano restaurant in Frankfurt, great Italian pasta"
                    ↓
AI Client (GitHub Copilot / Claude Desktop / Cursor)
  → reads 9 available tools from MCP Server
  → selects CreateAsset
  → extracts: name=Vapiano, category=restaurant, city=Frankfurt
                    ↓
MCP Tools (https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net)
  → receives structured JSON-RPC tool call over HTTPS
  → looks up CreateAsset method
  → executes it in-process
                    ↓
ReminderTools.CreateAsset()
  → inserts row into Azure SQL dbo.Assets
  → calls OpenAI text-embedding-3-small → float[1536] vector
  → saves vector to VECTOR(1536) column in Azure SQL
  → logs call to Azure Table Storage ActivityLog
                    ↓
User sees:  "Saved Vapiano (restaurant) in Frankfurt"
```
### What Happens for Each Operation

| User Says | Tool Called | What Happens in Azure |
|---|---|---|
| "Save a restaurant" | `CreateAsset` | Row inserted in SQL + embedding generated and stored |
| "Find Italian places" | `SearchAssets` | SQL query + optional OpenAI cosine similarity ranking |
| "Show details of Vapiano" | `GetAssetDetails` | SQL read + SAS URL generated for each photo |
| "Attach photo to Vapiano" | `AttachPhoto` | Photo uploaded to Blob Storage + row in dbo.Photos |
| "Delete Vapiano photo" | `DeleteImage` | Blob deleted + SQL Photos row removed |
| "Delete Vapiano" | `DeleteAsset` | SQL row deleted + all photos cascade deleted |
| "Upload my reminders CSV" | `BulkUploadAssets` | File uploaded to Blob + job queued in Queue Storage |
| "Check upload status" | `GetBulkUploadStatus` | Job summary read from Table Storage |
| "Show recent activity" | `GetRecentActivity` | ActivityLog read from Table Storage |

---

## System Architecture 
ReminderAgent is extended with a new Model Context Protocol (MCP) Server deployed to Azure, serving as the system's single production entry point. All business logic and Azure service calls run through the shared ReminderTools library, consumed directly by the MCP Server.

<img width="1180" height="940" alt="architecture_diagram_ReminderAgent" src="https://github.com/user-attachments/assets/8604b7bd-ffdf-4e5b-b3a5-cbe625ec0779" />

### Flow Explanation

1. **User's message** — the human hands off a request; this is where the flow starts
2. **Agent** — a component inside the MCP client application (GitHub Copilot, Claude Desktop, etc.) that brings its own LLM model and decides which tool to call and with what arguments this selection happens **in-process** on the client side
3. **MCP Client** — sends the already-structured tool call as a **network call over HTTPS** to the deployed server; this crosses a process boundary, so the call is serialized, sent over the wire, and a reply comes back
4. **MCP Tools (ReminderTools)** — running inside Azure App Service, this shared library receives the call, looks up the matching tool by name, and runs it **in-process**. It is not Azure-specific the same library could be called from any host. Every MCP call ends here and the response goes straight back to the Agent
5. **Azure** — ReminderTools reads from and writes to four Azure services:
   - **SQL Database** — Assets Table + Photos Table (Photos Table stores path only, plus embeddings as VECTOR)
   - **Blob Storage** — photo files + bulk-upload files
   - **Queue Storage** — bulk-upload jobs, processed by an async background worker
   - **Table Storage** — job status + activity log (status only)
     
## Tech Stack

| Component | Technology |
|---|---|
| Language / Runtime | C# / .NET 10.0 |
| Agent Framework | Microsoft Agent Framework (`IChatClient`, `AIFunctionFactory`) |
| MCP Protocol | ModelContextProtocol SDK 2.1.0 — Streamable HTTP |
| LLM | OpenAI GPT-4o-mini |
| Embedding Model | OpenAI `text-embedding-3-small` — 1,536-dim vectors |
| Database | Azure SQL Database — `dbo.Assets` + `dbo.Photos` |
| Photo Storage | Azure Blob Storage — private `photos/` container |
| Async Processing | Azure Queue Storage — `bulk-upload-queue` + poison queue |
| Logging and Job Status | Azure Table Storage — `ActivityLog` + `BulkUploadStatus` |
| Container Registry | Azure Container Registry (ACR) |
| Hosting | Azure App Service — Linux Docker container |

---

## Key Workflows

### Saving a Reminder

- User types e.g. *"Save Vapiano restaurant in Frankfurt, great Italian pasta"*
- Agent selects `CreateAsset`, inserts the asset row into `dbo.Assets`, generates a `float[1536]` embedding via OpenAI, and saves it to the `Embedding` column
- Every tool call is logged to Azure Table Storage `ActivityLog`

### Searching Reminders

- Agent uses `SearchAssets` it applies category, city, country, or timeline filters for concrete queries
- For vague queries the user can request semantic mode; OpenAI generates a query embedding and cosine similarity is computed against all stored vectors
- Results are returned ranked by filter match or similarity score

### Attaching a Photo

- Agent selects `AttachPhoto`, validates file extension (`.jpg`, `.png`, `.gif`, `.webp`), uploads to Azure Blob Storage `photos/` container
- The permanent **Blob Name** and a time-limited **SAS access token** are stored in `dbo.Photos`

### Deleting a Photo or Asset

- `DeleteImage` — accepts asset name or photo GUID; if multiple photos linked, asks for a filename to avoid accidental deletion
- `DeleteAsset` — removes the `dbo.Assets` row; linked `dbo.Photos` rows are deleted automatically via `ON DELETE CASCADE`

### Bulk Upload

- `BulkUploadAssets` uploads a CSV/JSON/ZIP file to Blob Storage and enqueues a job message — agent returns immediately with a Job ID
- The background worker picks up the job within 5 seconds, inserts each row as an Asset with embedding, and writes status to `BulkUploadStatus` in Azure Table Storage
- User checks progress with `GetBulkUploadStatus(jobId)`

---

## SE Project vs Cloud Migration

| Component | SE Project | Cloud Migration |
|---|---|---|
| **Reminder storage** | `reminders.csv` — flat file, atomic write via `File.Replace()` | Azure SQL Database `dbo.Assets` — relational, cloud-hosted |
| **Embeddings** | `embeddings.json` sidecar — persisted to disk, reloaded on startup | `VECTOR(1536)` column in `dbo.Assets` — stored in SQL alongside asset data |
| **Photos** | Local `Photos/` folder — file references only | Azure Blob Storage — private container, permanent Blob URL + SAS access |
| **Concurrency** | `SemaphoreSlim(1,1)` + `File.Replace()` — single process only | Azure SQL transactions — multi-client safe |
| **Tool count** | 7 tool functions | 9 MCP tools — unified + `BulkUploadAssets` + `GetBulkUploadStatus` added |
| **Deployment** | Local console — must clone and run locally | Docker container on Azure App Service — accessible over the network |
| **AI client access** | Console only — no remote access | MCP Server over Streamable HTTP — any AI client connects remotely |
| **Bulk import** | Not in scope | Azure Queue Storage + background worker |
| **Activity logging** | `FileLogger` — local log files | Azure Table Storage `ActivityLog` — permanent, queryable |
| **Job tracking** | Not in scope | Azure Table Storage `BulkUploadStatus` |
| **Search tools** | 4 separate tools (`SearchAssets`, `GetReminders`, `SearchRemindersSemantic`, `ListAssets`) | 1 unified `SearchAssets` tool |
| **Scalability** | Linear cosine scan — single user, single machine | In-memory cosine scan — planned replacement with Azure AI Search |
---

## System Requirements

### Local Development

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| Operating System | Windows, macOS, or Linux |
| Docker | Required for deployment only |
| Git | Any recent version |

### Azure Services

| Service | Minimum Tier |
|---|---|
| Azure SQL Database | Basic (S0) |
| Azure Storage Account | Standard LRS — covers Blob, Queue, and Table |
| Azure App Service | B1 — for Docker deployment |
| Azure Container Registry | Basic — for Docker deployment |

### External APIs

| API | Models Used |
|---|---|
| OpenAI API | `text-embedding-3-small` · `gpt-4o-mini` |

> **macOS:** Some networks block TCP port 1433. Add `TrustServerCertificate=True` to your SQL connection string and allowlist your IP in **Azure Portal → SQL Server → Networking → Firewall rules**. Run `curl -4 ifconfig.me` to find your IP.


## Project Structure

```
ReminderAgent_Cloud/
│
├── ReminderAgent.Common/                         ← Interfaces and entities
│   ├── Assest.cs                                 ← Asset entity
│   ├── IDataAccessLayer.cs                       ← SQL CRUD, embedding, photo operations
│   ├── IPhotoStorageProvider.cs                  ← Upload, download, delete, SAS URL
│   ├── IQueueService.cs                          ← Send, receive, delete queue messages
│   ├── ITableStatusService.cs                    ← Log activity, write and read job status
│   ├── IEmbeddingService.cs                      ← Generate asset and query embeddings
│   ├── ISimilarityService.cs                     ← Cosine similarity and top-K search
│   ├── BulkFileParser.cs                         ← CSV / JSON / ZIP parser
│   └── InitHelpers.cs                            ← Configuration and logging setup
│
├── ReminderAgent.Infrastructure/                 ← Azure implementations
│   ├── SqlDataAccessLayer.cs                     ← Azure SQL (dbo.Assets + dbo.Photos)
│   ├── AzureBlobPhotoStorageProvider.cs          ← Azure Blob Storage
│   ├── AzureQueueService.cs                      ← Azure Queue Storage
│   ├── AzureTableStatusService.cs                ← Azure Table Storage
│   ├── EmbeddingService.cs                       ← OpenAI embedding wrapper
│   ├── SimilarityService.cs                      ← In-memory cosine similarity
│   └── ReminderTools.cs                          ← 9 MCP tool methods
│
├── ReminderAgent.Cloud/                          ← Console REPL + background worker (SE Project code)
│   ├── Program.cs                                ← DI setup, chat loop, queue worker
│   ├── appsettings.json                          ← Placeholders (committed)
│   └── appsettings.dev.json                      ← Real secrets (gitignored)
│
├── ReminderAgent.McpServer/                      ← Real MCP server
│   ├── Program.cs                                ← Streamable HTTP /mcp endpoint
│   ├── appsettings.json                          ← Placeholders (commit to git)
│   └── appsettings.dev.json                      ← Real secrets (gitignored)
│
├── Tests/                                        ← 51 integration tests
│   ├── RemindertoolIntegrationtest.cs            ← 18 tests: business logic + SQL
│   ├── ReminderTools_PhotoOperations_
│   │   IntegrationTests.cs                      ← 8 tests: AttachPhoto + DeleteImage
│   ├── AzureBlob_PhotoStorageProvider_Tests.cs  ← 8 tests: Blob Storage provider
│   ├── McpServer_Protocol_IntegrationTests.cs   ← 17 tests: MCP wire protocol
│   ├── appsettings.json                          ← Placeholders (commit to git)
│   └── appsettings.dev.json                      ← Real secrets (gitignored)
│
└── schema.sql                                    ← dbo.Assets + dbo.Photos DDL
```

---
## Azure Resources

| Resource | Purpose |
|---|---|
| Azure SQL Server | Hosts the `ReminderAgentDb` database |
| Azure SQL Database | `dbo.Assets` and `dbo.Photos` tables |
| Azure Storage Account | Single account for all Blob, Queue, and Table storage |
| Blob Container `photos` (private) | Stores uploaded photo files |
| Queue `bulk-upload-queue` | Receives bulk upload job messages |
| Queue `bulk-upload-queue-poison` | Receives messages that failed more than 3 times |
| Table `ActivityLog` | Logs every tool invocation with timestamp |
| Table `BulkUploadStatus` | Per-row and job-level status for bulk uploads |
| Azure Container Registry | Stores Docker images |
| Azure App Service | Runs the Docker container |

### Setup Steps

- Run `schema.sql` in **Azure Portal → SQL Database → Query editor** to create all tables and indexes
- Add each team member's IP in **Azure Portal → SQL Server → Networking → Firewall rules** — find your IP with `curl -4 ifconfig.me`

---
## Getting Started

### 1 · Clone

```bash
git clone https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026.git
cd se-cloud-2025-2026/Source/ML25-26-11-ImplementReminderAgent-CloudComputing/ReminderAgent_Cloud
git checkout TechTrio-ReminderAgent
```

### 2 · Configure

Create `appsettings.dev.json` in `ReminderAgent.Cloud/`, `ReminderAgent.McpServer/`, and `Tests/` 

### 3 · Restore and Build

```bash
dotnet restore
dotnet build
```

---

## Configuration

### appsettings.json — placeholder structure, committed to git

```json
{
  "ConnectionStrings": {
    "SqlServer":    "YOUR_AZURE_SQL_CONNECTION_STRING_HERE",
    "BlobStorage":  "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE",
    "TableStorage": "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE",
    "QueueStorage": "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE"
  },
  "OpenAI": {
    "ApiKey":    "YOUR_OPENAI_API_KEY_HERE",
    "ModelName": "gpt-4o-mini"
  }
}
```

### appsettings.dev.json — real values, gitignored, never commit

```json
{
  "ConnectionStrings": {
    "SqlServer":    "<ADO.NET connection string — Azure Portal → SQL Database → Connection strings>",
    "BlobStorage":  "<Connection string — Azure Portal → Storage Account → Access keys → key1>",
    "TableStorage": "<Connection string — Azure Portal → Storage Account → Access keys → key1>",
    "QueueStorage": "<Connection string — Azure Portal → Storage Account → Access keys → key1>"
  },
  "OpenAI": {
    "ApiKey": "<Your key — platform.openai.com → API keys>"
  }
}
```

> `appsettings.dev.json` is in `.gitignore`. Never commit it.

### Configuration Loading Order

```
appsettings.json        →  appsettings.dev.json  →  Environment variables
(placeholders)             (real local secrets)      (Azure App Service)
```

---

## Running Locally

Use this for **development and testing**. The MCP Server runs on your machine and connects to your real Azure cloud services.

### Prerequisites
- .NET 10.0 SDK installed
- `appsettings.dev.json` created in `ReminderAgent.McpServer/` 
- Azure SQL schema deployed (run `schema.sql` in Azure Portal Query Editor)

### Start the MCP Server

You're already inside `ReminderAgent_Cloud` after the Clone step, so just:

```bash
dotnet run --project ReminderAgent.McpServer/ReminderAgent.McpServer.csproj
```

This uses the `ReminderAgent.McpServer` launch profile in `Properties/launchSettings.json`,
which binds Kestrel to **both** of these use either:
```
https://localhost:53100/mcp
http://localhost:53101/mcp
```

You should see something like:
```
Now listening on: https://localhost:53100
Now listening on: http://localhost:53101
```

### Connect an MCP Client to Local Server

**Claude Desktop** — edit `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "ReminderAgent": {
      "url": "https://localhost:53100/mcp"
    }
  }
}
```

**VS Code / GitHub Copilot** — edit `.vscode/mcp.json`:
```json
{
  "servers": {
    "reminderagent": {
      "type": "http",
      "url": "https://localhost:53100/mcp"
    }
  }
}
```

> Using the `https` URL against a local dev-certificate server may prompt your MCP client to
> trust a self-signed cert; use the plain `http://localhost:53101/mcp` URL instead if that's
> not convenient.


### Run Integration Tests Locally

```bash
dotnet test Test
```

---

## Running on Azure

The MCP Server is deployed and live. No local setup needed just connect your MCP client to the deployed endpoint.

### Live Deployment Details

| Resource | Value |
|---|---|
| App Service | `reminderagent` (North Europe) |
| MCP Endpoint | `https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/mcp` |
| Verify Server is Running | `https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net` |
| Container Registry | `mcpserverreminderagent` (Germany West Central) |
| App Service Plan | `TechTrio` |

### Verify Server is Running

Verify the server is running by opening in browser:
```
https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net
```

Expected response:
```
ReminderAgent MCP server is running. MCP endpoint: /mcp
```
### Connect an MCP Client to Azure

**Claude Desktop** — edit `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "ReminderAgent": {
      "url": "https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/mcp"
    }
  }
}
```

**VS Code / GitHub Copilot** — edit `.vscode/mcp.json`:
```json
{
  "servers": {
    "reminderagent": {
      "type": "http",
      "url": "https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/mcp"
    }
  }
}
```

Restart your MCP client after saving. You will see all 9 ReminderAgent tools available.

### Test the Live Endpoint

Type in your connected MCP client:
```
Save Vapiano restaurant in Frankfurt, amazing Italian pasta
```

Expected response:
```
Saved 'Vapiano' (restaurant) in Frankfurt 
```

---

### Project Containerised

Only **ReminderAgent.McpServer** is containerised and deployed. ReminderAgent.Cloud exists in the solution for local development and testing but is not deployed — the production system exposes a single MCP Server endpoint as required.

### Build

```bash
docker build -t reminderagent-mcpserver \
  -f ReminderAgent.McpServer/Dockerfile .
```

### Push to Azure Container Registry

```bash
az acr login --name mcpserverreminderagent

docker tag reminderagent-mcpserver \
  mcpserverreminderagent.azurecr.io/reminderagent-mcpserver:latest

docker push \
  mcpserverreminderagent.azurecr.io/reminderagent-mcpserver:latest
```

### Deploy to Azure App Service

```bash
az webapp create \
  --resource-group RG-TechTrio-ReminderAgent \
  --plan TechTrio \
  --name reminderagent \
  --deployment-container-image-name \
    mcpserverreminderagent.azurecr.io/reminderagent-mcpserver:latest
```

### Set Environment Variables

```bash
az webapp config appsettings set \
  --resource-group RG-TechTrio-ReminderAgent \
  --name reminderagent \
  --settings \
    ConnectionStrings__SqlServer="<your-sql-connection-string>" \
    ConnectionStrings__BlobStorage="<your-storage-connection-string>" \
    ConnectionStrings__TableStorage="<your-storage-connection-string>" \
    ConnectionStrings__QueueStorage="<your-storage-connection-string>" \
    OpenAI__ApiKey="<your-openai-key>"
```

### Deployed Resources

| Resource | Name | Location |
|---|---|---|
| Container Registry | `mcpserverreminderagent` | Germany West Central |
| App Service | `reminderagent` | North Europe |
| App Service Plan | `TechTrio` | North Europe |
| SQL Server | `sqlserverreminderagent` | Germany West Central |
| SQL Database | `ReminderAgentDb` | Germany West Central |
| Storage Account | `storagereminderagent` | Germany West Central |

### MCP Endpoint — Live

The deployed MCP endpoint is confirmed live and accessible:

```
https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/mcp
```

Verify Server is Running:
```
GET  https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/
Response: "ReminderAgent MCP Server is running. MCP endpoint: /mcp"
```

Connect from any MCP-compatible client by adding this URL:

| Client | Where to Add |
|---|---|
| Claude Desktop | Settings → Developer → MCP Servers → paste URL |
| GitHub Copilot Chat | VS Code → Command Palette → MCP: Add Server → paste URL |
| Cursor | Settings → MCP → Add server → paste URL |
| VS Code | MCP extension → Add server → paste URL |

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Data.SqlClient` | 7.1.0-preview2 | Azure SQL Database access |
| `Azure.Storage.Blobs` | 12.30.0-beta.1 | Blob Storage — upload, download, delete, SAS |
| `Azure.Storage.Queues` | 12.28.0-beta.1 | Queue Storage — send, receive, delete |
| `Azure.Data.Tables` | 12.11.0 | Table Storage — ActivityLog and BulkUploadStatus |
| `OpenAI` | 2.12.0 | OpenAI API client |
| `Microsoft.Extensions.AI.OpenAI` | 9.3.0-preview | `AsIEmbeddingGenerator()` extension |
| `Microsoft.Extensions.AI` | 9.3.0-preview | `IEmbeddingGenerator`, `IChatClient` |
| `ModelContextProtocol` | 2.1.0 | MCP protocol SDK |
| `ModelContextProtocol.AspNetCore` | 2.1.0 | Streamable HTTP transport |
| `System.ClientModel` | 1.15.0 | `ApiKeyCredential` |
| `Microsoft.Extensions.Configuration` | 11.0.0-preview | Configuration loading |
| `Microsoft.Extensions.Logging` | 11.0.0-preview | Logging infrastructure |
| `CsvHelper` | 33.1.0 | CSV parsing in BulkFileParser |
| `MSTest.TestFramework` | 3.3.1 | Integration test framework |
| `MSTest.TestAdapter` | 3.3.1 | Test runner |
| `Moq` | 4.x | Mocking in photo operation tests |

---

## Usage and Testing

### Console REPL

```bash
dotnet run --project ReminderAgent.Cloud
```

```
You: Save Vapiano restaurant in Frankfurt, amazing Italian pasta
Agent: Saved 'Vapiano' (restaurant) in Frankfurt 

You: Show my restaurants in Frankfurt
Agent: 1. Vapiano — Frankfurt (restaurant)

You: Find me a cosy Italian place with great pasta
Agent: Closest match: 1. Vapiano — Frankfurt (Score: 0.91)

You: Attach /Users/me/photo.jpg to Vapiano
Agent: Attached photo to Vapiano 

You: Delete the photo from Vapiano
Agent: Deleted photo from Vapiano 
```

### MCP Server

```bash
dotnet run --project ReminderAgent.McpServer
# MCP endpoint: http://localhost:5000/mcp
```

Connect from any MCP-compatible client by adding the MCP endpoint URL:

| Client | Where to Add |
|---|---|
| GitHub Copilot Chat | Settings → MCP Servers → Add server |
| Claude Desktop | Settings → Developer → MCP Servers |
| Cursor | Settings → MCP → Add server |
| VS Code | MCP extension → Add server |

### Running Tests

```bash
# All tests (51 total)
dotnet test Tests

# Filter by test class
dotnet test Tests --filter "FullyQualifiedName~RemindertoolIntegrationtest"
dotnet test Tests --filter "FullyQualifiedName~ReminderTools_PhotoOperations"
dotnet test Tests --filter "FullyQualifiedName~AzureBlobPhotoStorageProvider"
dotnet test Tests --filter "FullyQualifiedName~McpServer_Protocol"
```

| Test Class | Test Count | What It Covers |
|---|---|---|
| `RemindertoolTests` | 18 | ReminderTools operations and Azure SQL |
| `ReminderTools_PhotoOperations_Tests` | 8 | AttachPhoto and DeleteImage tools |
| `AzureBlobPhotoStorageProviderTests` | 8 | Azure Blob Storage provider |
| `McpServer_Protocol_Tests` | 17 | MCP wire protocol end-to-end |

### Verify Data in Azure Portal

```sql
-- View saved reminders
SELECT Id, Name, Category, City, TimelineState, CreatedAt
FROM dbo.Assets ORDER BY CreatedAt DESC;

-- Confirm embeddings stored (6144 bytes = 1536 floats × 4 bytes)
SELECT Name, DATALENGTH(Embedding) AS EmbeddingBytes
FROM dbo.Assets WHERE Embedding IS NOT NULL;

-- View photo references
SELECT p.FileName, p.BlobName, a.Name AS AssetName
FROM dbo.Photos p JOIN dbo.Assets a ON a.Id = p.AssetId;
```

---

## Agent Plugins

All 9 tools are decorated with `[McpServerTool]` and `[Description]` — AI clients discover and call them automatically.

| Tool | Parameters | What It Does |
|---|---|---|
| `CreateAsset` | `name`, `category`, `tags`, `city`, `country`, `region`, `eventDate`, `userExperience`, `userInput` | Saves a reminder to Azure SQL with a 1536-dim OpenAI embedding |
| `SearchAssets` | `query`, `category`, `city`, `country`, `timeline`, `semantic`, `topK` | Filter reminders by metadata or optionally rank by semantic similarity |
| `GetAssetDetails` | `assetName` | Full reminder details including attached photos with Blob URLs and SAS links |
| `AttachPhoto` | `assetName`, `photoPath` | Uploads photo to Azure Blob Storage and links it to a reminder |
| `DeleteImage` | `assetNameOrPhotoId` | Deletes a photo from Blob Storage and removes the `dbo.Photos` row |
| `DeleteAsset` | `assetName` | Removes a reminder — linked photos deleted automatically via `ON DELETE CASCADE` |
| `BulkUploadAssets` | `filePath` | Uploads CSV/JSON/ZIP to Blob and enqueues an async processing job |
| `GetBulkUploadStatus` | `jobId` | Returns job summary from Azure Table Storage `BulkUploadStatus` |
| `GetRecentActivity` | `count` | Returns recent tool calls from Azure Table Storage `ActivityLog` |

> `SearchAssets` applies keyword and filter matching by default. Set `semantic=true` only for vague or conceptual queries where keyword matching is not sufficient.

### Timeline Classification

| State | Condition |
|---|---|
| `Past` | `EventDate` is before today |
| `Present` | No `EventDate` provided |
| `Future` | `EventDate` is after today |

---


<div align="center">

*ReminderAgent_Cloud · Branch: TechTrio-ReminderAgent · ML25/26-11 · Frankfurt UAS · 2026*

</div>

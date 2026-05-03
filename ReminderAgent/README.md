# ML25/26-11 - Implement Reminder Agent

**Group:** TechTrio · Kavya Sree Thabjul – 1633324 · Bavana Jadala – 1629828 · Aishwarya Rajendra Sawant – 1629688

**Branch:** [`TechTrio-ReminderAgent`](https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/TechTrio-ReminderAgent)

---

### Abstract

A restaurant name heard at lunch is gone by dinner.A book title from a conversation vanishes before there is time to write it down. The tools built to solve this problem mostly make it worse, demanding categories, tags, and exact phrasing at the moment when a person just wants to say one sentence and move on. Reminder Agent takes a different approach. Users type whatever comes naturally, and the system figures out the rest. Built in C# on the Microsoft Agent Framework, it extracts structure from plain language, stores personal assets like places, books, and travel ideas in lightweight files, and retrieves them later by date, location, category, or meaning. What makes the retrieval design stand out is that it adapts to the query: when a concrete filter such as a date or location is present the system uses a metadata pipeline, and when the query is vague or feeling-based it falls back to cosine similarity over 1,536-dimensional OpenAI embeddings, meaning a search for “something cosy I visited last winter” can still find the right result. No database is needed. No setup is required. Unit and integration tests run against real files on disk confirmed that storage, retrieval, and duplicate detection all behave correctly across the kinds of varied, real-world input that users actually produce.  

---

### Introduction

This project focuses on simplifying how users capture and manage personal reminders by removing the need for structured input and rigid workflows. It demonstrates how a conversational system can automatically interpret user intent, organize information into meaningful components, and store it in a way that supports both flexibility and reliability. The architecture follows a modular, interface-driven design with clear separation of concerns across storage, embedding, and similarity layers, enabling scalability and maintainability. A lightweight persistence approach is used to ensure portability while still supporting safe concurrent operations and data consistency . By combining clean system design with AI-driven understanding, the project highlights how modern software engineering practices can be applied to build practical, user-friendly intelligent systems.

---

### System Architecture

ReminderAgent is structured as a four-tier .NET application. A user message enters at the **Presentation tier** (`Program.cs` REPL, `List<ChatMessage>` history). At the **Orchestration tier**, GPT-4o-mini selects the correct tool via `UseFunctionInvocation()`. The **Tool tier** (`ReminderTools.cs`, 7 methods) executes business logic and calls the **Infrastructure tier** for storage, embeddings, and similarity.

<img width="894" height="1200" alt="Gemini_Generated_Image_45gnkz45gnkz45gn" src="https://github.com/user-attachments/assets/7ce183b7-7aee-48d3-ac3e-11d62b748eb1" />


*Figure 1: Layered architecture — Presentation → Orchestration → Tool Layer → Infrastructure. `FileLogger` is a static cross-cutting class used by all infrastructure components.*

| Layer | Component | Responsibility |
|---|---|---|
| Presentation | `Program.cs` | REPL, `ChatMessage` history, keyword intent detector |
| Orchestration | `IChatClient` + GPT-4o-mini | Tool dispatch via `AIFunctionFactory` + `[Description]` routing; RAG second-call |
| Tool Layer | `ReminderTools.cs` | 7 AI-callable methods, date parsing, duplicate detection |
| Infrastructure | `CsvStorageProvider` · `JsonEmbeddingStore` · `EmbeddingService` · `SimilarityService` · `FileLogger` | Persistence, vectors, cosine scoring, logging |
| External | OpenAI API | `gpt-4o-mini` + `text-embedding-3-small` (1,536-dim) |

---

### Methodology

#### Data Model - Asset.cs

All reminders share a single `Asset` entity, uniquely identified by a GUID.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key — used in both CSV and JSON stores |
| `Name` / `Category` | `string` | Display name and classification |
| `TimelineState` | `enum` | Past / Present / Future — **computed at query time** from `EventDate`, never stored |
| `EventDate` | `DateTime?` | Used for date filtering and timeline classification |
| `UserInput` | `string` | Original verbatim sentence from the user |
| `UserExperience` | `string` | Mood descriptor extracted by LLM (e.g. `"wonderful"`) |
| `Tags` | `List<string>` | Semicolon-delimited keywords |
| `Metadata` | `Dictionary<string,string>` | Holds `City`, `Country`, `Region`, and free-form notes |
| `PhotoRefs` | `List<string>` | Relative paths to attached photos |
| `Embedding` | `float[]?` | 1,536-dim vector — **stored separately in JSON**, populated after deserialisation; may be `null` before hydration |


---

#### CsvStorageProvider - Structured Asset Storage

`CsvStorageProvider` handles all CRUD on `reminders.csv` using a custom CsvHelper `AssetMap` class with four TypeConverters: `TagsConverter` (semicolon list), `MetadataConverter` (`key=value` pairs), `DateConverter` (`dd-MM-yyyy`), and `PhotoRefsConverter` (semicolon list). Before any write it checks whether the file is held open by Excel and raises a descriptive error if so. A timestamped `.bak` snapshot is created in `UserData/backup/` after each successful write.

<img width="571" height="302" alt="image" src="https://github.com/user-attachments/assets/24a4e715-805b-49c3-a911-f96cfebd1158" />


---

#### JsonEmbeddingStore - Vector Storage

`JsonEmbeddingStore` persists `float[1536]` vectors in `embeddings.json`, keyed by asset GUID. Separating vectors from the CSV keeps the CSV human-readable and allows the embedding model to be upgraded without touching asset data. The **write ordering invariant**: `JsonEmbeddingStore` is always written before `CsvStorageProvider` if the JSON write fails, the CSV is never touched; if the CSV write fails, the orphaned embedding is cleaned up on the next load cycle.

All writes use the **atomic write pattern**: write to `.tmp` → `File.Replace()` atomic OS rename + `SemaphoreSlim(1,1)` concurrency gate.

```
Algorithm: Atomic File Write
─────────────────────────────────────────────
1: ACQUIRE SemaphoreSlim(1,1)
2: WRITE data → temp file (.tmp)
3: REPLACE original WITH temp  ← atomic OS rename
4: RELEASE SemaphoreSlim
```


---

#### EmbeddingService - Vector Generation

`EmbeddingService.GenerateEmbeddingAsync(string text)` calls OpenAI's `text-embedding-3-small` and returns a `float[1536]` vector. At **save time**, `CreateAsset` builds a 5-field composite string before calling this method:

```
Name: {name}
Category: {category}
Experience: {userExperience}
Details: {userInput}. Location: {city, region, country}
Tags: {tags}
```

At **search time**, the raw user query is passed directly no composite. This **asymmetric strategy** means a short query is compared against a fully contextualised asset representation, which is the primary driver of retrieval quality.

<img width="432" height="176" alt="image" src="https://github.com/user-attachments/assets/39046bf4-075e-4e8a-8502-8a9997669020" />


---

#### SimilarityService - Cosine Similarity and Top-K Ranking

`SimilarityService` computes cosine similarity and ranks all stored embeddings against a query vector.

**Cosine similarity formula:**

```
sim(q, aᵢ) = (q · aᵢ) / (‖q‖ × ‖aᵢ‖)
```

Score ranges from 0 to 1. The formula is magnitude-independent, it measures only directional alignment. `GetTopKSimilarAsync` loads all vectors from `JsonEmbeddingStore`, scores each against the query, filters by threshold, and returns the top-K sorted descending.

| Threshold | Used In | Reason |
|---|---|---|
| `0.75` | `SearchAssets` semantic fallback | High-precision - user has a concrete intent |
| `0.25` | `SearchRemindersSemantic` | High-recall - user has a vague, feeling-based query |

Edge cases handled: identical vectors → `1.0`; opposite vectors → clamped to `0.0`; zero vector → epsilon guard returns `0.0`; mismatched lengths → `ArgumentException`.

<img width="371" height="222" alt="image" src="https://github.com/user-attachments/assets/160c1b34-3220-462e-a9d8-b95ab6b5b31a" />

---

#### FileLogger - Structured Observability

`FileLogger` is a static class used by all infrastructure components. It writes date-stamped entries to `UserData/app-yyyy-MMdd.log`:

```
{timestamp} | {level} | {message}
```

Levels: `INFO` (saves, searches, embedding calls) · `WARNING` (missing files, file-lock detection) · `ERROR` (API failures, write errors).


---

#### ReminderTools - Tool Functions

Seven public methods are registered as `AIFunction`s via `AIFunctionFactory.Create`. GPT-4o-mini reads the `[Description]` attribute on each method to select the correct tool and extract arguments.

| Tool | Key Parameters | Trigger Condition |
|---|---|---|
| `CreateAsset` | Name, Category, UserInput, EventDate, City, Country, Region, Tags | New reminder creation |
| `SearchAssets` | query, category, dateRange, location, topK | Query with time, category, or location filters |
| `GetReminders` | state (Past / Present / Future) | Timeline-scoped recall |
| `SearchRemindersSemantic` | query, topK | Descriptive / feeling-based query without filters |
| `AttachPhoto` | assetName, photoReference | Attach a photo to an existing asset |
| `ListAssets` | category (optional) | Browse all stored assets |
| `GetAssetDetails` | assetName | Full detail lookup (4-step chain: exact → partial → keyword → GUID) |


---

#### CreateAsset - Ingestion Pipeline

`CreateAsset` runs phases in strict sequence. Each step has a single responsibility and fails independently with a clear error message:

```
1. LLM argument extraction
2. Date resolution via ResolveRelativeDate
3. Build 5-field composite embedding string
4. EmbeddingService.GenerateEmbeddingAsync() → float[1536]
5. Fuzzy duplicate detection (2-level guard — see below)
6. JsonEmbeddingStore.SaveEmbeddingAsync()   ← JSON first
7. CsvStorageProvider.SaveAssetAsync()        ← CSV second
```

**Duplicate detection** — two-level guard in `SaveAssetAsync`:

Level 1: blocks if exact `(Name, Category)` pair already exists.
Level 2: extracts significant keywords `K(s) = { w ∈ tokens(s) | |w| > 3 }` and blocks if overlap ≥ 2:

```
|K(s_new) ∩ K(sᵢ)| ≥ 2  →  blocked
```

Example: `"Vapiano Italian restaurant"` and `"Vapiano restaurant Frankfurt"` share `"Vapiano"` + `"restaurant"` → blocked. `"Vapiano"` and `"Vapiano Sachsenhausen"` share only `"Vapiano"` → allowed.

<img width="371" height="214" alt="image" src="https://github.com/user-attachments/assets/de9baa0e-ca3e-493b-89e9-d51d278a9bf1" />


---

#### SearchAssets - Hybrid Retrieval Pipeline

`SearchAssets` switches modes based on what filters the query contains:

```
Algorithm: Hybrid Retrieval
──────────────────────────────────────────────────────────────
1: if structuredFilter EXISTS (category / dateRange / location) then
2:     return topK items sorted by EventDate
3: else
4:     embedding ← EmbeddingService.GenerateEmbeddingAsync(query)
5:     results  ← SimilarityService.GetTopKSimilarAsync(
                   embedding, assets, topK, threshold=0.75)
6:     if results EMPTY then return topK sorted by EventDate
7:     else return results
8: end if
```

**Location matching is bidirectional:** `"Frankfurt"` matches city field `"Frankfurt am Main"` (forward); `"Germany"` matches country field (reverse). `ParseDateRange` converts relative expressions (`"last week"`, `"next summer"`) to calendar-boundary `(DateTime? start, DateTime? end)` pairs using:

```
t_start = StartOfMonth(t₀ − n months)
t_end   = EndOfMonth(t₀ − 1 month)
```

A regex pre-check detects 4-digit year literals before relative-unit parsing, preventing incorrect computation for phrases like `"books from 2023"`.

<img width="598" height="271" alt="image" src="https://github.com/user-attachments/assets/582b875e-4dc1-44af-8f60-1121a380a164" />


---

#### Program.cs - DI Wiring and REPL

`Program.cs` is the **single composition root**. Registration order matters: `JsonEmbeddingStore` must be registered before `CsvStorageProvider` because the storage provider depends on the embedding store at construction time.

```csharp
services.AddSingleton<IEmbeddingStore,    JsonEmbeddingStore>();
services.AddSingleton<IStorageProvider,   CsvStorageProvider>();  // depends on IEmbeddingStore
services.AddSingleton<IEmbeddingService,  EmbeddingService>();
services.AddSingleton<ISimilarityService, SimilarityService>();

IChatClient client = new OpenAIClient(apiKey)
    .GetChatClient("gpt-4o-mini")
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();
```

The REPL loop maintains a growing `List<ChatMessage>` passed to `IChatClient` on every turn. A **keyword intent detector** catches creation-intent phrases that GPT-4o-mini would otherwise misroute as queries, and re-sends them with an explicit system instruction forcing a `CreateAsset` call.

<img width="415" height="325" alt="image" src="https://github.com/user-attachments/assets/1a20bd63-98a0-493a-aa30-1b2f841e70dc" />
<img width="361" height="293" alt="image" src="https://github.com/user-attachments/assets/0ae6029c-16c0-4d05-8a70-fa60e933b0b4" />


---

### Test Cases

The test suite is organised into two tiers. The first tier comprises **unit tests** that verify individual components in isolation, using Moq to mock all infrastructure interfaces and an InMemoryEmbeddingStore to eliminate file system dependency. The second tier comprises **integration tests** that exercise the full tool stack against real CSV and JSON files on disk.

```bash
cd ReminderAgent_Test && dotnet test
# Expected: Passed: 182, Failed: 0, Skipped: 0
```

<img width="572" height="326" alt="image" src="https://github.com/user-attachments/assets/d0409f25-3ada-46f8-b22c-5f80375e532a" />



**Unit test coverage:**

| Component | Tests | What Is Verified |
|---|---|---|
| `CsvStorageProvider` | ~20 | Save/load round-trips, update, delete, backup, Excel-lock detection, full field fidelity |
| `JsonEmbeddingStore` | ~15 | Round-trips, concurrent writes (20 parallel), 1,536-dim float precision |
| `SimilarityService` | ~10 | All 6 cosine edge cases, top-K threshold filtering |
| `ReminderTools` | ~20 | All 7 tools via Moq — date parsing, location matching, duplicate detection, photo validation |
| Interface Contracts | ~6 | Reflection-based — method names, param types, return types for all 4 interfaces |
| `Program.cs` (intent detector) | ~6 | Correct retry triggering for creation-intent phrases; no false positives on query-intent |


**Integration tests — 3 sets:**

| Set | Focus |
|---|---|
| ReminderTools_ToolChain_IntegrationTests.cs | Attachment workflows, date format round-trips |
| ReminderTools_CsvPersistence_IntegrationTests.cs | Location queries (bidirectional), relative time expressions, `TimelineState` reclassification |
| EmbeddingService_CsvProvider_IntegrationTests.cs | Special characters, concurrent write atomicity, embedding survival across provider restart |

**Concurrent write safety test:**
```csharp
[TestMethod]
public async Task ConcurrentSaves_DoNotCorruptStore()
{
    var tasks = Enumerable.Range(0, 20)
        .Select(i => _store.SaveEmbeddingAsync(
            Guid.NewGuid().ToString(), new float[1536]));
    await Task.WhenAll(tasks);
    var all = await _store.LoadAllAsync();
    Assert.AreEqual(20, all.Count);
}
```


---

### Results

All 182 tests pass. `CreateAsset` successfully infers location from implicit context — `"visited the Colosseum last summer"` produces `city=Rome, country=Italy, region=Lazio` with no explicit location fields. `GetReminders` accurately reclassifies stale `"Present"` records as `"Past"` by computing `TimelineState` dynamically at query time, requiring no data migration. Storage round-trips preserve all field types: multi-value lists, the `Metadata` dictionary, dates, and full 1,536-dimensional embedding vectors with no precision loss. The in-memory cosine scan scores 10,000 assets against a query embedding in **under three seconds** on commodity hardware.

<img width="959" height="422" alt="SearchingForLastMonthReminders" src="https://github.com/user-attachments/assets/25102211-75e8-4c3b-9c50-c4c61d9d7c58" />


---

### Conclusion

ReminderAgent demonstrates that conversational capture and retrieval of personal assets is achievable without a database, without structured forms, and without requiring users to remember exact keywords. The hybrid retrieval design routes queries to a metadata pipeline or a cosine similarity scan depending on their content both modes were necessary, since structured filtering missed vague queries while semantic search alone could not handle date or location constraints efficiently. The interface-first architecture means the most valuable future improvements database-backed storage, HNSW approximate nearest-neighbour indexing, multi-user support would each require changes only to the infrastructure layer, leaving tool logic and orchestration untouched.

---

### Setup and Run

**Requirements:** .NET SDK 10.0 · OpenAI API key (`gpt-4o-mini` + `text-embedding-3-small`)

```bash
git clone https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026.git
git checkout TechTrio-ReminderAgent
dotnet restore && dotnet build

## Setup API key Instructions
 
# 1. Open appsettings.json
# 2. Replace:
 
   "ApiKey": "YOUR_API_KEY_HERE"
 # with your own OpenAI API key
 
# OR run:
 
   dotnet run --OpenAI:ApiKey=your-key-here

# Run agent
cd ReminderAgent && dotnet run

# Run tests (no API key needed for unit + mocked tests)
cd ReminderAgent_Test && dotnet test
```

---

# ReminderAgent Blueprint

Two front doors, one shared core. ReminderAgent can be reached two ways — a console
app with its own built-in agent, or any MCP client talking to
`ReminderAgent.McpServer`. Both paths use the same tools and the same Azure backend.
One diagram shows that shape; the rest zoom into each piece.

**Color key**, used consistently below:
- 🟣 violet — a process that hosts an agent (Console App, App Host)
- 🔵 blue — an Azure-managed resource
- 🟢 green — OpenAI (external)
- 🟠 amber — runs in the background / asynchronously

## 1. Overview — how a request reaches Azure, either way

The console has its own built-in agent and calls tools directly. An MCP client
instead sends its request over MCP/HTTP to the MCP server; the server itself has no
agent — it just runs the tool the client already chose. Either way, there is exactly
**one** path into the tool library. OpenAI is a separate, independent conversation on
the side: the console asks it which tool to call, and — shown as a note on the tool
library itself, not a second arrow in — the tool library separately asks it for
embeddings once it's already running. The App Host does **not** call OpenAI at all;
its agent brings its own model, which may not be OpenAI's.

```mermaid
flowchart TD
    User(["User"])

    subgraph ConsoleApp["Console App — ReminderAgent.Cloud"]
        ChatAgent["Chat + Built-in Agent<br/>GPT-4o-mini decides which tool to call"]
    end

    subgraph AppHost["App Host — Claude Desktop / VS Code / any MCP client"]
        AgentLLM["Agent or LLM<br/>brings its own model, picks a tool"]
    end

    OpenAI(["OpenAI API<br/>chat completions"])
    McpServer["ReminderAgent.McpServer<br/>looks up that tool by name · no LLM here"]

    subgraph ToolsBand["ReminderTools — one shared library"]
        Tools["Create &amp; Search Assets · Photos<br/>Bulk Upload &amp; Status · Delete &amp; Activity Log<br/>— also calls OpenAI on its own, for embeddings —"]
    end

    subgraph AzureBoundary["Azure"]
        SQL[("Azure SQL<br/>assets & photos")]
        Blob[("Blob Storage<br/>photos & files")]
        Queue[("Queue Storage<br/>bulk-upload jobs")]
        Table[("Table Storage<br/>status & logs")]
    end

    User -->|user's message| ConsoleApp
    User -->|user's message| AppHost

    ChatAgent <-->|chat: picks a tool| OpenAI
    ChatAgent -->|calls the tool directly, in-process| ToolsBand

    AgentLLM -->|tool name + arguments, MCP/HTTP| McpServer
    McpServer -->|calls the tool directly| ToolsBand

    ToolsBand -->|reads & writes data| AzureBoundary

    classDef appHost fill:#efeaff,stroke:#6a56c9,color:#3b2f80,stroke-width:2px;
    classDef azure fill:#e8f2fb,stroke:#0f6cbd,color:#0a4d87,stroke-width:2px;
    classDef openai fill:#ffffff,stroke:#12875a,color:#0d5c3d,stroke-width:2px;
    classDef tools fill:#eaf5f3,stroke:#2c7a72,color:#1c4f4a,stroke-width:2px;
    class ConsoleApp,AppHost appHost;
    class AzureBoundary azure;
    class OpenAI openai;
    class ToolsBand tools;
```

## 2. Console app — one process, two things happening

The console runs a chat loop for the person typing, and a background worker for bulk
uploads, at the same time, in the same process. Neither blocks the other: a slow bulk
import never freezes the chat, and a long conversation never delays the queue.

```mermaid
flowchart LR
    subgraph ChatLoop["Chat Loop — repeats every turn"]
        direction TB
        C1["Read user message"] --> C2["Ask GPT-4o-mini<br/>it calls tools as needed"] --> C3["Show the reply<br/>+ live Azure SQL asset count"]
        C3 -.->|next turn| C1
    end

    subgraph BackgroundWorker["Background Worker — repeats every 5 seconds"]
        direction TB
        B1["Poll the upload queue"] -->|"job: jobId, blob name"| B2["Download &amp; parse the file"] -->|rows to import| B3["Insert row + generate embedding"] -->|success/failure, per row| B4["Save row status<br/>+ job totals, to Table Storage"]
        B4 -.->|next poll| B1
    end

    classDef chat fill:#ffffff,stroke:#6a56c9,color:#3b2f80,stroke-width:1.5px;
    classDef worker fill:#fdf3e3,stroke:#b5720a,color:#7a4c06,stroke-width:1.5px;
    class C1,C2,C3 chat;
    class B1,B2,B3,B4 worker;
```

`Insert row + generate embedding` really does write to Azure SQL — it calls the same
data-access method the `CreateAsset` tool calls internally, just without going through
the tool itself. What it skips is everything the tool wraps around that call:
validating the row, parsing an event date, and logging the action.

## 3. MCP server — looks up the tool, doesn't choose it

The client's own LLM is what reads the user's request and picks a tool — the server
never sees plain language. It only ever sees an exact tool name and gets the
arguments ready. "Looks up" in the middle step means matching an exact name, the way a
dictionary lookup does — not understanding.

```mermaid
flowchart TD
    A["Client's LLM already chose the tool<br/>e.g. CreateAsset(name, category, …)"] -->|"tool name + arguments (JSON-RPC)"| B["Server looks up that tool by name<br/>a dictionary lookup — not AI"]
    B -->|same arguments, passed straight through| C["Tool runs, result goes back<br/>client's LLM reads it and decides what's next"]

    classDef step fill:#ffffff,stroke:#444,color:#111,stroke-width:1.5px;
    classDef lookup fill:#e8f2fb,stroke:#0f6cbd,color:#0a4d87,stroke-width:1.5px;
    class A,C step;
    class B lookup;
```

> **Note:** the background worker from diagram 2 only exists inside the console app —
> a bulk upload started through MCP alone will queue but never finish unless the
> console is also running.

## 4. Bulk upload — reply now, finish later

Importing hundreds of rows takes too long to make the user wait. So the tool replies
immediately, and the real work happens in the background. The job id is the thread
that ties the quick reply to the slow work happening behind it.

```mermaid
flowchart TD
    A["User: 'Import these 200 places from a file'"] -->|file path| B["Tool queues the job and replies right away<br/>'Started job #1234 — ask me later'"]
    B -->|job id, any time after| D["Later, user checks in<br/>'How did that import go?'"]
    B -.->|automatic| C["Meanwhile — background worker<br/>downloads the file, creates each<br/>asset + embedding, and saves<br/>progress as it goes"]
    D -->|job id| E["Status tool reads the saved progress<br/>'196 succeeded, 4 failed'"]
    C -.->|saved progress| E

    classDef normal fill:#ffffff,stroke:#444,color:#111,stroke-width:1.5px;
    classDef async fill:#fdf3e3,stroke:#b5720a,color:#7a4c06,stroke-width:1.5px;
    class A,B,D,E normal;
    class C async;
```

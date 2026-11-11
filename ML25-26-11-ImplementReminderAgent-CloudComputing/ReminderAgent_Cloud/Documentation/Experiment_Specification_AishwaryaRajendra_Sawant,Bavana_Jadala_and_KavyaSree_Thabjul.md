# ML25/26-11Imp lement Reminder Agent - Azure Cloud Implementation

## What problem does it solve?

Imagine you visit a restaurant you loved, read a great book, or plan a trip, but a few months later, you can't remember the name, or where it was, or what you thought about it. ReminderAgent is a computer system that remembers these things for you. You just tell it in plain sentences, like you're talking to a friend - "I ate at a really nice Italian place in Frankfurt called Trattoria Roma last June" and it saves that as an organized record you can search for later.

## How does it work, step by step?

1. **You type a message.** No special commands needed just normal sentences, like "find restaurants I saved in Frankfurt" or "remind me what I said about that book."
2. **An AI "brain" figures out what you want.** A language model (similar to the one you're talking to right now) reads your sentence and decides which of 9 built-in tools to use one tool for saving something new, one for searching, one for attaching a photo, one for deleting, and so on.
3. **The tool does the actual work.** Each tool is a small, specific program. For example, the "save" tool takes what you said, cleans it up (fixes the date format, removes extra spaces), and stores it safely.
4. **Everything lives in the cloud, not on one computer.** Instead of saving data on a single laptop that could break or get lost, the system uses Microsoft Azure, a cloud computing platform think of it as a set of very reliable, always-on computers rented over the internet. Different Azure services handle different jobs:
   - A **database** stores the actual facts (name, category, date, notes)
   - A **file storage service** stores photos and uploaded files
   - A **queue** (like a to-do list) handles big batches of data patiently, one at a time, so nothing gets overwhelmed or lost
   - A **table** keeps a log of what happened and whether it worked
5. **It can even understand meaning, not just exact words.** Using something called an "embedding," the system can find things you're looking for even if you don't use the exact same words you originally used similar to how you might remember "that cozy pasta place" without recalling its exact name.

**Reference to SE Project Documentation:** https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/tree/TechTrio-ReminderAgent/Source/ML25-26-11-ImplementReminderAgent/Documentation

## 1. What is the input?

The input is a normal sentence typed by a person nothing technical, no special format. It could be something like "Save Trattoria Roma, an Italian restaurant in Frankfurt I visited in June," or "Show me all the books I saved," or even a whole file full of many entries at once (a CSV, JSON, or ZIP file), if someone wants to add lots of reminders in one go instead of typing them one by one.

## 2. What is the output?

The output is two things happening together:

- A **saved, updated, or found record** the piece of information itself (a restaurant, book, trip, etc.) safely stored in the cloud database, or returned back to the user if they searched for something.
- A **plain-English reply** a short confirmation message sent back to the user, like "Saved Trattoria Roma" or "I found 3 restaurants in Frankfurt," so the person immediately knows what happened, without needing to understand anything technical underneath.

## 3. What does algorithm do? How?

The algorithm is built around **MCP (Model Context Protocol)** a standard way for an AI agent to talk to a set of external tools. Instead of the AI trying to do everything itself, MCP gives it a fixed toolbox of 9 specific tools it's allowed to call, each with a clear name and a clear job. This keeps the "thinking" and the "doing" cleanly separated.

- **The understanding layer (the agent)** is an AI language model acting as the agent. It reads the sentence the person typed and figures out what they actually want are they trying to save something new, search for something old, attach a photo, or delete an entry? Using MCP, it then picks the correct one of the 9 available tools and pulls out the useful details from the sentence (the name, the date, the city, and so on) to pass along as that tool's input.
- **The execution layer (the tools)** is where the actual work happens. Each MCP tool is a small, precise program that does exactly one job well and nothing more:
  1. **CreateAsset** - saves a brand-new reminder (restaurant, book, hotel, trip, etc.)
  2. **SearchAssets** - searches, lists, or filters saved reminders, including "meaning-based" search
  3. **AttachPhoto** - attaches a photo to an existing saved reminder
  4. **GetAssetDetails** - looks up the full details of one saved reminder
  5. **DeletePhoto** - removes a specific photo from a reminder
  6. **DeleteAsset** - deletes a reminder entirely, including any attached photos
  7. **BulkUploadAssets** - imports many reminders at once from an uploaded file (CSV, JSON, or ZIP)
  8. **GetBulkUploadStatus** - checks how a large import job is progressing
  9. **GetRecentActivity** - shows a log of what the agent has done recently

  For example, when someone saves a new item, `CreateAsset` cleans up the details (making sure the date is in the right format, removing extra spaces), checks whether it's a duplicate of something already saved, and then stores it in the database along with a special "meaning fingerprint" (an embedding) that lets it be found later even by different words. For very large batches of data, `BulkUploadAssets` doesn't do everything immediately, it hands the big job off to a queue, a waiting line, so a background helper can work through it patiently, one entry at a time, without slowing anything down for the user.

## How to run experiment

### The core idea

Running the experiment doesn't involve running a script or triggering a pipeline manually, it's a live conversation. You connect a chat client to the deployed MCP server, and every message you type becomes a "run" of the experiment: the agent interprets it, picks one of the 9 tools, and executes it against the live Azure backend.

### Two ways we connected and tested this

1. **VS Code (Copilot Chat, Agent mode)** - used mainly for development/debugging, since VS Code shows the raw tool calls being made, which is useful to verify the agent picked the right tool with the right parameters.
2. **Claude.ai (web)** - used as the "real" end-user experience, since this is closer to how someone would actually use ReminderAgent day to day, without seeing any of the technical machinery underneath.

### Option 1: VS Code (Copilot Chat, Agent mode)
1. Open VS Code.
2. Press **Ctrl+Shift+P** to open the Command Palette.
3. Type and select **MCP: Add Server.**
4. Choose **HTTP (Streamable HTTP)** as the server type.
5. Enter the deployed MCP server URL - https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/mcp
6. Give the server a name (e.g. ReminderAgent) - this saves the config into .vscode/mcp.json under the servers key.
<img width="677" height="379" alt="Image" src="https://github.com/user-attachments/assets/e632a8ab-3b95-4871-827c-061d6e85a488" />

7. click on the start, just above the name you gave in step 6.
8. Open the **Copilot Chat panel** (Ctrl+Alt+I or the chat icon in the sidebar).
9. Switch the chat mode dropdown from "Ask" to Agent.
10. VS Code detects the configured MCP server and lists all 9 ReminderAgent tools as available.
11. Type a request in plain language, e.g. "Save the attached file reminders."
12. Copilot shows which tool it's about to call (e.g. BulkUploadAssets) with the parameters it extracted — click Continue/Allow to run it.
13. The result appears in the chat, and the record is saved in Azure SQL.
<img width="689" height="317" alt="Image1" src="https://github.com/user-attachments/assets/6fd83dca-2467-4086-9ebb-331fb5f6f892" />
<img width="681" height="257" alt="Image2" src="https://github.com/user-attachments/assets/f44531b8-fc58-4c65-b965-df4be156b47b" />


### Option 2: Claude.ai (web)

> **Note:** Custom connectors (MCP support) are generally only available on **paid** versions of LLM chat clients. This applies to Claude.ai (Pro, Max, Team, or Enterprise) and similarly to other LLM providers that support MCP, such as ChatGPT (Plus/Pro/Team/Enterprise), if used as an alternative client.

1. Go to [claude.ai](https://claude.ai) and log in.
2. Click your profile icon (bottom-left) → **Settings**.
3. Go to the **Connectors** tab.
4. Click **Add custom connector** (or **Browse connectors** if searching a directory).
5. Enter a name (e.g. `ReminderAgent`) and the deployed MCP server URL - https://reminderagent-cta7e8fegycye8gh.northeurope-01.azurewebsites.net/mcp
6. Click **Add / Connect** — Claude verifies the connection and lists the 9 available tools.
7. Start a new chat.
8. Click the **tools/connector icon** below the message box and make sure `ReminderAgent` is toggled on for this chat.
9. Type a request in plain language, e.g. *"Show me all the restaurants I saved in Frankfurt."*
10. Claude calls the matching tool (`SearchAssets`) behind the scenes and replies in plain English with the results.
<img width="845" height="378" alt="Image3" src="https://github.com/user-attachments/assets/89bc0929-6d76-44e2-9c7c-d137319193fc" />
<img width="666" height="475" alt="Image4" src="https://github.com/user-attachments/assets/6f7ae5d8-a43b-49a1-b93f-b9dc3249a64b" />

Claude chat link: https://claude.ai/share/d17363e8-cc85-42f2-8ff2-0eb5908692d1

---

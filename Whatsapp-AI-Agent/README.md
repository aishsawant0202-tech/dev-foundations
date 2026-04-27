# 📱 WhatsApp AI Agent — powered by OpenClaw

Turn your own WhatsApp number into a personal AI assistant. No prefix. No commands. Just talk to it like a person and it figures out what you need.

> Built by [@AishwaryaSawant](https://github.com/aishsawant0202-tech/dev-foundations) · Master's student, Frankfurt UAS 🇩🇪  
> _Still exploring and improving this — PRs and suggestions welcome!_

---

## What this actually does

You send a normal WhatsApp message to yourself. The agent decides what to do.

| What you send                        | What comes back              |
| ------------------------------------ | ---------------------------- |
| `create a butterfly image`           | Generated image, in WhatsApp |
| `build a shopping website`           | Full HTML + CSS code         |
| `integration formula for sin(x)`     | Step-by-step math solution   |
| `explain recursion simply`           | Plain English explanation    |
| `debug this: [paste code]`           | Fixed code + explanation     |
| `write an email declining a meeting` | Draft, ready to copy         |
| `spent 12€ on coffee`                | Logged + budget status       |
| `how am I doing this week?`          | Weekly spending breakdown    |

No special syntax. No app to open. Just a message.

---

## How it works

```
You type anything in WhatsApp
            ↓
OpenClaw (running locally on your PC)
            ↓
Reads your AGENTS.md personality + instructions
            ↓
OpenRouter free model decides what to do
(generate image / write code / answer / run skill)
            ↓
Response lands back in WhatsApp
```

**OpenClaw** is the local gateway. It runs on your PC, connects to your WhatsApp number, and passes every message to the AI agent.

**AGENTS.md** is where the agent's personality and behaviour come from. It tells the agent how to respond on WhatsApp — tone, format, when to activate specific skills, what not to do.

**OpenRouter free model** (`openrouter/openrouter/free`) is the brain. It handles image generation, code, math, writing, and general questions all on the free tier, no billing needed.

**BudgetClaw skill** is a custom Python backend wired in for finance tracking. Spending data lives in a local DuckDB file and never leaves your machine.

## Setup

### 1. Install OpenClaw

https://docs.openclaw.ai/

The wizard connects WhatsApp (scan QR code) and configures your model.

### 2. Clone this repo

```bash
git clone https://github.com/yourusername/whatsapp-ai-agent
cd whatsapp-ai-agent
```

### 3. Copy workspace files

**Mac/Linux:**

```bash
cp openclaw/AGENTS.md ~/.openclaw/workspace/AGENTS.md
cp openclaw/TOOLS.md ~/.openclaw/workspace/TOOLS.md
cp -r openclaw/skills ~/.openclaw/workspace/skills
```

**Windows (PowerShell):**

```powershell
copy openclaw\AGENTS.md "$env:USERPROFILE\.openclaw\workspace\AGENTS.md"
copy openclaw\TOOLS.md "$env:USERPROFILE\.openclaw\workspace\TOOLS.md"
xcopy openclaw\skills "$env:USERPROFILE\.openclaw\workspace\skills" /E /I
```

### 4. Set free model in openclaw.json

```json
"model": {
  "primary": "openrouter/openrouter/free"
}
```

### 5. Start the gateway

```bash
openclaw gateway start
```

### 6. Message yourself on WhatsApp

```
create a butterfly image
```

That's it. No prefix. No commands.

---

## What's in the repo

### AGENTS.md — the agent's brain

This is the most important file. It defines:

- How the agent behaves on WhatsApp (no markdown, short replies, line breaks not bullets)
- When to activate BudgetClaw vs answer directly
- Memory conventions — what to log, what to keep long-term
- Group chat behaviour, heartbeat checks, proactive tasks

Paste it into `~/.openclaw/workspace/AGENTS.md` and the agent picks it up on next start.

### BudgetClaw skill — finance tracker

A Python-backed skill for personal finance. No bank login needed — just drop your weekly export in a folder.

**Log expenses by typing naturally:**

```
spent 12€ on coffee
rewe 47 euros
bus ticket 2.90
```

**Ask about your spending:**

```
how am I doing this week?
compare this week to last
where is my money going?
set my grocery budget to 90
```

**Drop your bank export:**

```
Same like we attach pdf in whatsapp chat
```

**Weekly letter every Monday — automatically.**

Setup:

```bash
pip install duckdb httpx pdfplumber
python3 ~/budgetclaw/db.py
```

### TOOLS.md — your local notes

Template for environment-specific notes — SSH hosts, device names, preferences. Stays local, never committed.

---

## Project structure

```
whatsapp-ai-agent/
├── README.md
├── openclaw/
│   ├── AGENTS.md                  # agent personality (template)
│   └── skills/
│       └── examples/
│           └── SKILL.md           # finance tracker skill
└── examples/
    └── budgetclaw/
        ├── db.py                  # database setup
        ├── ingest.py              # CSV + PDF + WhatsApp parser
        ├── tools.py               # query functions
        └── letter.py              # weekly letter generator
```

**Windows log location:**

```
C:\Users\YourName\AppData\Local\Temp\openclaw\openclaw-YYYY-MM-DD.log
```

## Security — never commit these

```
~/.openclaw/openclaw.json        # API key + phone number + token
~/.openclaw/workspace/MEMORY.md  # personal memory
~/budgetclaw/data/               # transaction database
~/budgetclaw/exports_drop/       # bank files
~/budgetclaw/letters/            # weekly letters
```

---

## Tested on

- Windows 11 · OpenClaw 2026.4.22 · Node 25.9.0
- OpenRouter free model (`openrouter/openrouter/free`)
- Confirmed: image generation · HTML/CSS · math · general Q&A · finance tracking
- Sparkasse CSV + PDF exports

---

---
name: budgetclaw
description: Personal finance tracker. Use this skill when the user mentions spending money, asks about their budget, weekly spending, transactions, categories, or says anything like "spent X euros on Y", "how am I doing this week", "compare my spending", or "send my letter". Also triggers on any message containing a euro amount (€ or "euros" or "eur").
---

# BudgetClaw skill

BudgetClaw tracks spending from WhatsApp messages and bank exports (CSV or PDF). All data lives in a local DuckDB file. No external finance API is used.

## Script location

All scripts are in `~/budgetclaw/` (adjust path if user placed it elsewhere).

## Tool: log a WhatsApp expense

When user sends a spending message, run:

```
python3 ~/budgetclaw/ingest.py msg "EXACT USER MESSAGE"
```

Pass the user's exact message as the argument. The script parses the amount and category automatically. Output is JSON — read the `reply` field and send it as-is to the user. Do not paraphrase it.

## Tool: weekly summary

```
python3 ~/budgetclaw/tools.py summary
```

Returns JSON with spending by category and budget warnings. Convert to plain text reply (no markdown, 3–5 lines).

## Tool: compare weeks

```
python3 ~/budgetclaw/tools.py compare
```

Returns JSON comparing this week vs last week by category.

## Tool: top merchants

```
python3 ~/budgetclaw/tools.py merchants
```

Returns top 5 merchants by spend this week.

## Tool: daily burn

```
python3 ~/budgetclaw/tools.py daily
```

Returns daily totals for the last 7 days.

## Tool: set budget

```
python3 ~/budgetclaw/tools.py set-budget "Category" AMOUNT
```

Example: `python3 ~/budgetclaw/tools.py set-budget "Groceries" 90`

## Tool: ingest bank file

```
python3 ~/budgetclaw/ingest.py ~/budgetclaw/exports_drop/FILENAME.csv
python3 ~/budgetclaw/ingest.py ~/budgetclaw/exports_drop/FILENAME.pdf
```

Both CSV and PDF exports from German banks are supported.

## Tool: weekly letter

```
python3 ~/budgetclaw/letter.py
```

Generates a plain-text weekly summary letter using OpenRouter. Send the output directly to the user.

## Reply format rules

- No markdown in replies. No **, no #, no -.
- Max 5 lines unless user asks for more.
- Always use € not EUR.
- If a category is over 85% of budget, mention it.
- If a category is over 100%, say so clearly but calmly.

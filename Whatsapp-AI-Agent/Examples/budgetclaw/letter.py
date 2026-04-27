"""
BudgetClaw — weekly letter generator
Uses OpenRouter (your existing setup) — no separate Anthropic API key needed.

Run manually:   python letter.py
Auto every Mon: add to crontab → 0 8 * * 1 cd /path/to/budgetclaw && python letter.py
"""

import os, json, httpx
from datetime import datetime, timedelta
from db import get_conn, init_db
from tools import get_weekly_summary, get_top_merchants, compare_weeks

LETTERS_DIR = os.path.join(os.path.dirname(__file__), "letters")
os.makedirs(LETTERS_DIR, exist_ok=True)

# ── OpenRouter config ──────────────────────────────────────────────────────────
# Uses the same key you already have for OpenClaw
OPENROUTER_API_KEY = os.environ.get("OPENROUTER_API_KEY", "")
OPENROUTER_MODEL   = os.environ.get("OPENROUTER_MODEL", "anthropic/claude-3.5-haiku")
# Other good options: "openai/gpt-4o-mini", "mistralai/mistral-7b-instruct"
# Haiku is fast, cheap, and writes letters well.


def last_week_str() -> str:
    return (datetime.now() - timedelta(weeks=1)).strftime('%Y-W%W')


def call_openrouter(prompt: str) -> str:
    if not OPENROUTER_API_KEY:
        raise EnvironmentError(
            "Set your OpenRouter key:\n"
            "  export OPENROUTER_API_KEY=sk-or-...\n"
            "This is the same key you use for OpenClaw."
        )

    response = httpx.post(
        "https://openrouter.ai/api/v1/chat/completions",
        headers={
            "Authorization": f"Bearer {OPENROUTER_API_KEY}",
            "Content-Type": "application/json",
            "HTTP-Referer": "https://github.com/budgetclaw",  # optional but good practice
            "X-Title": "BudgetClaw"
        },
        json={
            "model": OPENROUTER_MODEL,
            "max_tokens": 600,
            "messages": [{"role": "user", "content": prompt}]
        },
        timeout=30
    )
    response.raise_for_status()
    return response.json()["choices"][0]["message"]["content"].strip()


def build_prompt(summary, merchants, comparison, tone) -> str:
    return f"""You are writing a weekly personal finance letter. Tone: {tone}.

Rules:
- Write in second person ("you"), plain flowing paragraphs only
- No bullet points, no headers, no markdown, no emojis
- 3 paragraphs maximum. Be specific about actual euro amounts.
- End with exactly one gentle, concrete suggestion for next week
- If spending is genuinely fine this week, say so — do not manufacture concern

Data for {summary['period']}:
Total spent: €{summary['total_spent']:.2f}

By category:
{json.dumps(summary['categories'], indent=2)}

Top merchants:
{json.dumps(merchants['top_merchants'], indent=2)}

vs previous week — previous: €{comparison['total_previous']:.2f}, this week: €{comparison['total_current']:.2f}
Category changes: {json.dumps(comparison['breakdown'], indent=2)}

Budget warnings triggered: {summary['warnings'] or 'none'}

Write the letter now. Start directly — no preamble, no "Dear" salutation."""


def generate_letter(week: str = None, tone: str = "warm and honest, like a financially-literate friend") -> str:
    init_db()
    week = week or last_week_str()

    summary    = get_weekly_summary(week)
    merchants  = get_top_merchants(week)
    comparison = compare_weeks()

    prompt = build_prompt(summary, merchants, comparison, tone)
    letter = call_openrouter(prompt)

    # Save to file
    filename = os.path.join(LETTERS_DIR, f"letter_{week}.txt")
    with open(filename, "w", encoding="utf-8") as f:
        f.write(f"Week: {week}\n")
        f.write(f"Period: {summary['period']}\n")
        f.write(f"Model: {OPENROUTER_MODEL}\n")
        f.write(f"Generated: {datetime.now().isoformat()}\n")
        f.write("-" * 48 + "\n\n")
        f.write(letter)

    # Save to DB
    con = get_conn()
    con.execute("""
        INSERT OR REPLACE INTO weekly_letters (week, generated_at, letter_text)
        VALUES (?, ?, ?)
    """, [week, datetime.now().isoformat(), letter])
    con.close()

    print(f"\nSaved → {filename}\n")
    print("=" * 48)
    print(letter)
    print("=" * 48)
    return letter


if __name__ == "__main__":
    import sys
    week = sys.argv[1] if len(sys.argv) > 1 else None
    generate_letter(week)

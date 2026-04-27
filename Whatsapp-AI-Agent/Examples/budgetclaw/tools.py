"""
BudgetClaw — OpenClaw tool definitions
Register these as tools in your openclaw config.

These are the functions OpenClaw calls when you message on WhatsApp.
Each function returns a JSON-serialisable dict.
"""

import json
from datetime import datetime, timedelta
from db import get_conn, init_db


# ── Helper ────────────────────────────────────────────────────────────────────

def current_week() -> str:
    return datetime.now().strftime('%Y-W%W')

def week_dates(week_str: str) -> tuple[str, str]:
    """Return Monday and Sunday of a given week string."""
    year, wk = week_str.split('-W')
    monday = datetime.strptime(f'{year} {wk} 1', '%Y %W %w')
    sunday = monday + timedelta(days=6)
    return monday.strftime('%Y-%m-%d'), sunday.strftime('%Y-%m-%d')


# ── Tool 1: get_weekly_summary ────────────────────────────────────────────────

def get_weekly_summary(week: str = None) -> dict:
    """
    Returns spending by category for a given week.
    OpenClaw calls this when user asks 'how am I doing this week?'

    Args:
        week: ISO week string like '2025-W12'. Defaults to current week.
    """
    init_db()
    week = week or current_week()
    start, end = week_dates(week)
    con = get_conn()

    rows = con.execute("""
        SELECT
            t.category,
            ROUND(SUM(t.amount), 2) AS spent,
            b.weekly_limit,
            ROUND(SUM(t.amount) / b.weekly_limit * 100, 0) AS pct
        FROM transactions t
        LEFT JOIN budgets b ON t.category = b.category
        WHERE t.date BETWEEN ? AND ?
        GROUP BY t.category, b.weekly_limit
        ORDER BY spent DESC
    """, [start, end]).fetchall()

    total = con.execute("""
        SELECT ROUND(SUM(amount), 2) FROM transactions
        WHERE date BETWEEN ? AND ?
    """, [start, end]).fetchone()[0] or 0.0

    con.close()

    categories = []
    warnings = []
    for cat, spent, limit, pct in rows:
        status = "ok"
        if limit and pct >= 90:
            status = "warning"
            warnings.append(f"{cat}: {pct}% of weekly budget used (€{spent} / €{limit})")
        elif limit and pct >= 100:
            status = "over"
            warnings.append(f"{cat}: OVER budget — €{spent} spent vs €{limit} limit")
        categories.append({
            "category": cat,
            "spent": float(spent),
            "limit": float(limit) if limit else None,
            "pct_used": float(pct) if pct else None,
            "status": status
        })

    return {
        "week": week,
        "period": f"{start} to {end}",
        "total_spent": float(total),
        "categories": categories,
        "warnings": warnings,
        "has_warnings": len(warnings) > 0
    }


# ── Tool 2: get_top_merchants ─────────────────────────────────────────────────

def get_top_merchants(week: str = None, limit: int = 5) -> dict:
    """
    Returns top merchants by spend this week.
    OpenClaw calls this when user asks 'where am I spending most?'
    """
    init_db()
    week = week or current_week()
    start, end = week_dates(week)
    con = get_conn()

    rows = con.execute("""
        SELECT merchant, category, ROUND(SUM(amount), 2) AS total, COUNT(*) AS visits
        FROM transactions
        WHERE date BETWEEN ? AND ?
        GROUP BY merchant, category
        ORDER BY total DESC
        LIMIT ?
    """, [start, end, limit]).fetchall()

    con.close()
    return {
        "week": week,
        "top_merchants": [
            {"merchant": r[0], "category": r[1], "total": float(r[2]), "visits": r[3]}
            for r in rows
        ]
    }


# ── Tool 3: compare_weeks ─────────────────────────────────────────────────────

def compare_weeks(week_a: str = None, week_b: str = None) -> dict:
    """
    Compares spending between two weeks.
    Defaults: week_a = last week, week_b = this week.
    """
    init_db()
    now = datetime.now()
    last_week = (now - timedelta(weeks=1)).strftime('%Y-W%W')
    week_a = week_a or last_week
    week_b = week_b or current_week()

    con = get_conn()
    results = {}
    for label, week in [("previous", week_a), ("current", week_b)]:
        start, end = week_dates(week)
        rows = con.execute("""
            SELECT category, ROUND(SUM(amount), 2)
            FROM transactions WHERE date BETWEEN ? AND ?
            GROUP BY category
        """, [start, end]).fetchall()
        results[label] = {r[0]: float(r[1]) for r in rows}

    con.close()

    all_cats = set(results["previous"]) | set(results["current"])
    diff = []
    for cat in sorted(all_cats):
        prev = results["previous"].get(cat, 0.0)
        curr = results["current"].get(cat, 0.0)
        change = round(curr - prev, 2)
        diff.append({
            "category": cat,
            "previous": prev,
            "current": curr,
            "change": change,
            "direction": "up" if change > 0 else "down" if change < 0 else "same"
        })

    return {
        "week_a": week_a,
        "week_b": week_b,
        "breakdown": diff,
        "total_previous": round(sum(results["previous"].values()), 2),
        "total_current":  round(sum(results["current"].values()), 2)
    }


# ── Tool 4: set_budget ────────────────────────────────────────────────────────

def set_budget(category: str, weekly_limit: float) -> dict:
    """
    Update a budget limit for a category.
    OpenClaw calls this when user says 'set my grocery budget to €90 a week'.
    """
    init_db()
    con = get_conn()
    con.execute("""
        INSERT INTO budgets (category, weekly_limit) VALUES (?, ?)
        ON CONFLICT(category) DO UPDATE SET weekly_limit = excluded.weekly_limit
    """, [category, weekly_limit])
    con.close()
    return {"updated": category, "weekly_limit": weekly_limit}


# ── Tool 5: get_daily_burn ────────────────────────────────────────────────────

def get_daily_burn(days: int = 7) -> dict:
    """
    Returns daily spending for the last N days.
    Used to plot the dashboard sparkline.
    """
    init_db()
    con = get_conn()
    rows = con.execute("""
        SELECT date, ROUND(SUM(amount), 2)
        FROM transactions
        WHERE date >= CURRENT_DATE - INTERVAL (?) DAY
        GROUP BY date ORDER BY date
    """, [days]).fetchall()
    con.close()
    return {
        "days": days,
        "daily": [{"date": str(r[0]), "spent": float(r[1])} for r in rows],
        "avg_daily": round(
            sum(r[1] for r in rows) / len(rows), 2
        ) if rows else 0.0
    }


# ── OpenClaw tool manifest ────────────────────────────────────────────────────
# Paste this block into your openclaw tools config (tools.yaml or similar)

OPENCLAW_TOOLS = [
    {
        "name": "get_weekly_summary",
        "description": "Get this week's spending by category with budget warnings",
        "function": get_weekly_summary,
        "parameters": {
            "week": {"type": "string", "description": "ISO week e.g. 2025-W12. Omit for current week."}
        }
    },
    {
        "name": "get_top_merchants",
        "description": "Get top merchants by spend this week",
        "function": get_top_merchants,
        "parameters": {
            "week": {"type": "string", "description": "ISO week. Omit for current week."},
            "limit": {"type": "integer", "description": "How many merchants to return. Default 5."}
        }
    },
    {
        "name": "compare_weeks",
        "description": "Compare spending between this week and last week",
        "function": compare_weeks,
        "parameters": {}
    },
    {
        "name": "set_budget",
        "description": "Update the weekly budget for a spending category",
        "function": set_budget,
        "parameters": {
            "category": {"type": "string"},
            "weekly_limit": {"type": "number"}
        }
    },
    {
        "name": "get_daily_burn",
        "description": "Get daily spending totals for the last N days",
        "function": get_daily_burn,
        "parameters": {
            "days": {"type": "integer", "description": "Number of days to look back. Default 7."}
        }
    }
]


if __name__ == "__main__":
    # Quick smoke test
    print(json.dumps(get_weekly_summary(), indent=2))


# ── CLI wrapper — called by OpenClaw agent via exec tool ─────────────────────
# Usage:
#   python3 tools.py summary
#   python3 tools.py compare
#   python3 tools.py merchants
#   python3 tools.py daily
#   python3 tools.py set-budget "Groceries" 90

if __name__ == "__main__":
    import sys, json
    cmd = sys.argv[1] if len(sys.argv) > 1 else "summary"

    if cmd == "summary":
        print(json.dumps(get_weekly_summary(), indent=2))
    elif cmd == "compare":
        print(json.dumps(compare_weeks(), indent=2))
    elif cmd == "merchants":
        print(json.dumps(get_top_merchants(), indent=2))
    elif cmd == "daily":
        print(json.dumps(get_daily_burn(), indent=2))
    elif cmd == "set-budget" and len(sys.argv) == 4:
        print(json.dumps(set_budget(sys.argv[2], float(sys.argv[3])), indent=2))
    else:
        print(f"Unknown command: {cmd}")
        print("Commands: summary | compare | merchants | daily | set-budget <category> <amount>")
        sys.exit(1)

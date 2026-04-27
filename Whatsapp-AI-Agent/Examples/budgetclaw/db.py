"""
BudgetClaw — database layer
DuckDB: zero server, single file, fast SQL.
"""

import duckdb
import os

DB_PATH = os.path.join(os.path.dirname(__file__), "data", "budgetclaw.duckdb")
os.makedirs(os.path.dirname(DB_PATH), exist_ok=True)


def get_conn():
    return duckdb.connect(DB_PATH)


def init_db():
    con = get_conn()
    con.execute("""
        CREATE TABLE IF NOT EXISTS transactions (
            id          VARCHAR PRIMARY KEY,
            date        DATE NOT NULL,
            amount      DECIMAL(10,2) NOT NULL,
            merchant    VARCHAR,
            category    VARCHAR,
            raw_text    VARCHAR,
            source      VARCHAR DEFAULT 'csv',
            week        VARCHAR GENERATED ALWAYS AS (strftime(date, '%Y-W%W')) VIRTUAL
        )
    """)
    con.execute("""
        CREATE TABLE IF NOT EXISTS budgets (
            category    VARCHAR PRIMARY KEY,
            weekly_limit DECIMAL(10,2) NOT NULL
        )
    """)
    con.execute("""
        CREATE TABLE IF NOT EXISTS weekly_letters (
            week        VARCHAR PRIMARY KEY,
            generated_at TIMESTAMP,
            letter_text TEXT
        )
    """)
    # Seed sensible default budgets (edit these to match your life)
    con.execute("""
        INSERT OR IGNORE INTO budgets VALUES
            ('Groceries',    100.00),
            ('Eating out',    40.00),
            ('Transport',     20.00),
            ('Subscriptions', 15.00),
            ('Health',        25.00),
            ('Misc',          30.00)
    """)
    con.close()
    print("DB ready:", DB_PATH)


if __name__ == "__main__":
    init_db()

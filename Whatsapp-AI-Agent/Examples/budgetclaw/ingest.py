"""
BudgetClaw — unified ingestor
Handles three input types:
  1. Bank CSV export  →  python ingest.py mybank.csv
  2. Bank PDF export  →  python ingest.py mybank.pdf
  3. WhatsApp text    →  log_whatsapp_expense("spent 10 euros on shopping")
                         (called by OpenClaw tool when you message it)
"""

import os, re, csv, hashlib, json
from datetime import datetime, date
from db import get_conn, init_db


# ── Category rules ─────────────────────────────────────────────────────────────
CATEGORY_RULES = [
    (["rewe", "edeka", "lidl", "aldi", "penny", "netto", "kaufland",
      "tegut", "norma", "lebensmittel", "supermarkt", "denn's",
      "grocery", "groceries", "supermarket"],                          "Groceries"),
    (["restaurant", "pizza", "döner", "sushi", "mcdonalds", "burger",
      "lieferando", "wolt", "uber eats", "cafe", "bäckerei", "imbiss",
      "eating out", "food", "lunch", "dinner", "breakfast", "snack",
      "coffee", "kaffee"],                                              "Eating out"),
    (["rmv", "bvg", "db ", "deutsche bahn", "mvv", "nextbike",
      "tier ", "lime ", "flixbus", "fernbus", "tankstelle",
      "transport", "bus", "train", "ticket", "fahrkarte", "taxi",
      "uber", "bolt"],                                                  "Transport"),
    (["netflix", "spotify", "amazon prime", "disney", "apple.com",
      "google one", "github", "chatgpt", "openai", "rundfunk",
      "subscription", "abo"],                                           "Subscriptions"),
    (["apotheke", "pharmacy", "arzt", "doctor", "dm ", "rossmann",
      "mueller", "müller", "drogerie", "health", "medicine",
      "gym", "sport", "fitness"],                                       "Health"),
    (["miete", "rent", "nebenkosten", "strom", "gas ", "wasser",
      "internet", "phone", "handy", "o2 ", "vodafone", "telekom"],     "Rent & utilities"),
    (["shopping", "clothes", "kleidung", "h&m", "zara", "zalando",
      "amazon", "saturn", "mediamarkt", "kaufhof"],                    "Shopping"),
]
FALLBACK_CATEGORY = "Misc"


def categorise(text: str) -> str:
    t = text.lower()
    for keywords, cat in CATEGORY_RULES:
        if any(k in t for k in keywords):
            return cat
    return FALLBACK_CATEGORY


def make_id(*parts) -> str:
    return hashlib.md5("".join(str(p) for p in parts).encode()).hexdigest()


# ════════════════════════════════════════════════════════════
# INPUT TYPE 1 — CSV
# ════════════════════════════════════════════════════════════

def parse_amount(val: str) -> float:
    val = re.sub(r'[^\d,.\-]', '', val.strip().replace('"', ''))
    if ',' in val and '.' in val:
        val = val.replace('.', '').replace(',', '.')
    elif ',' in val:
        val = val.replace(',', '.')
    return float(val) if val else 0.0


def parse_date(val: str) -> str:
    val = val.strip().replace('"', '')
    for fmt in ('%d.%m.%Y', '%Y-%m-%d', '%d/%m/%Y', '%d.%m.%y'):
        try:
            return datetime.strptime(val, fmt).strftime('%Y-%m-%d')
        except ValueError:
            continue
    raise ValueError(f"Unrecognised date: {val}")


def ingest_csv(filepath: str) -> dict:
    init_db()
    con = get_conn()
    inserted = skipped = 0

    with open(filepath, encoding='utf-8-sig', errors='replace') as f:
        sample = f.read(2048); f.seek(0)
        delimiter = ';' if sample.count(';') > sample.count(',') else ','
        reader = csv.DictReader(f, delimiter=delimiter)

        for row in reader:
            try:
                date_val = merchant = amount_val = None
                for k, v in row.items():
                    kl = k.lower()
                    if any(x in kl for x in ['buchung', 'datum', 'date', 'valuta']):
                        try: date_val = parse_date(v)
                        except: pass
                    if any(x in kl for x in ['beguenstigter', 'empfaenger', 'empfänger',
                                              'auftraggeber', 'merchant', 'payee', 'name',
                                              'verwendungszweck']):
                        if v.strip(): merchant = v.strip()
                    if any(x in kl for x in ['betrag', 'amount', 'umsatz', 'wert']):
                        try:
                            a = parse_amount(v)
                            if a < 0: amount_val = abs(a)
                        except: pass

                if not date_val or not amount_val:
                    skipped += 1; continue

                merchant = merchant or "Unknown"
                category = categorise(merchant)
                row_id   = make_id(date_val, amount_val, merchant)

                con.execute("""
                    INSERT OR IGNORE INTO transactions
                        (id, date, amount, merchant, category, raw_text, source)
                    VALUES (?, ?, ?, ?, ?, ?, 'csv')
                """, [row_id, date_val, amount_val, merchant, category, merchant])
                inserted += 1
            except Exception:
                skipped += 1

    con.close()
    return {"inserted": inserted, "skipped": skipped, "format": "csv"}


# ════════════════════════════════════════════════════════════
# INPUT TYPE 2 — PDF
# German bank PDFs are usually text-based (not scanned).
# pdfplumber handles them cleanly. Falls back to PyMuPDF.
# ════════════════════════════════════════════════════════════

def _extract_pdf_text(filepath: str) -> str:
    try:
        import pdfplumber
        text = ""
        with pdfplumber.open(filepath) as pdf:
            for page in pdf.pages:
                t = page.extract_text()
                if t: text += t + "\n"
        return text
    except ImportError:
        pass
    try:
        import fitz
        doc = fitz.open(filepath)
        return "\n".join(page.get_text() for page in doc)
    except ImportError:
        raise ImportError(
            "Install pdfplumber:  pip install pdfplumber\n"
            "Or PyMuPDF:          pip install pymupdf"
        )


def _parse_pdf_lines(text: str) -> list:
    rows = []
    # Primary: date + description + negative German-format amount
    pattern = re.compile(
        r'(\d{2}\.\d{2}\.\d{2,4})\s+(.+?)\s+(-\d{1,3}(?:\.\d{3})*,\d{2})'
    )
    for line in text.splitlines():
        m = pattern.search(line.strip())
        if m:
            try:
                rows.append((
                    parse_date(m.group(1)),
                    abs(parse_amount(m.group(3))),
                    m.group(2).strip()
                ))
            except Exception:
                continue

    # Fallback: looser matching
    if not rows:
        for line in text.splitlines():
            dm = re.search(r'\b(\d{2}\.\d{2}\.\d{2,4})\b', line)
            am = re.search(r'-(\d+[,\.]\d{2})\b', line)
            if dm and am:
                try:
                    merchant = line[dm.end():am.start()].strip()
                    merchant = re.sub(r'\s+', ' ', merchant) or "Unknown"
                    rows.append((parse_date(dm.group(1)), abs(parse_amount(am.group(1))), merchant))
                except Exception:
                    continue
    return rows


def ingest_pdf(filepath: str) -> dict:
    init_db()
    text = _extract_pdf_text(filepath)

    if not text.strip():
        return {
            "inserted": 0, "skipped": 0,
            "error": "PDF appears to be scanned/image-based. "
                     "Export as CSV instead, or send me a CSV."
        }

    rows = _parse_pdf_lines(text)
    if not rows:
        return {
            "inserted": 0, "skipped": 0,
            "error": "Could not parse transactions from this PDF layout. "
                     "Try CSV export — every German bank supports it.",
            "raw_preview": text[:400]
        }

    con = get_conn()
    inserted = skipped = 0
    for date_str, amount, merchant in rows:
        row_id = make_id(date_str, amount, merchant)
        try:
            con.execute("""
                INSERT OR IGNORE INTO transactions
                    (id, date, amount, merchant, category, raw_text, source)
                VALUES (?, ?, ?, ?, ?, ?, 'pdf')
            """, [row_id, date_str, amount, merchant, categorise(merchant), merchant])
            inserted += 1
        except Exception:
            skipped += 1
    con.close()
    print(f"PDF: {len(rows)} found, {inserted} inserted, {skipped} skipped")
    return {"inserted": inserted, "skipped": skipped, "format": "pdf"}


# ════════════════════════════════════════════════════════════
# INPUT TYPE 3 — WhatsApp natural language
# OpenClaw calls log_whatsapp_expense() as a tool.
#
# Handles messages like:
#   "spent 10 euros on shopping"
#   "coffee 3.50"
#   "rewe groceries 47€"
#   "transport ticket 2.90"
#   "bought medicine, 8 euros"
# ════════════════════════════════════════════════════════════

_AMOUNT_PATTERNS = [
    r'(\d+(?:[.,]\d{1,2})?)\s*(?:euros?|eur|€)',
    r'€\s*(\d+(?:[.,]\d{1,2})?)',
    r'(\d+(?:[.,]\d{1,2})?)\s*$',
]

def _parse_whatsapp_message(message: str):
    msg = message.strip()
    for pat in _AMOUNT_PATTERNS:
        m = re.search(pat, msg, re.IGNORECASE)
        if m:
            amount = float(m.group(1).replace(',', '.'))
            description = re.sub(pat, '', msg, flags=re.IGNORECASE)
            description = re.sub(r'\b(spent|on|for|bought|paid|at|got)\b', '',
                                  description, flags=re.IGNORECASE)
            description = re.sub(r'\s+', ' ', description).strip(' ,.-') or "Misc expense"
            return amount, description
    raise ValueError(f"No amount found in: '{message}'")


def log_whatsapp_expense(message: str) -> dict:
    """
    OpenClaw tool — called when user sends a spending message on WhatsApp.
    Returns dict with 'reply' key that OpenClaw sends back to the user.
    """
    init_db()

    try:
        amount, description = _parse_whatsapp_expense(message)
    except ValueError as e:
        return {
            "success": False,
            "reply": "Couldn't find an amount. Try: 'spent 12 euros on food' or 'groceries 34€'"
        }

    today    = date.today().isoformat()
    category = categorise(description)
    row_id   = make_id(today, amount, message)

    con = get_conn()
    con.execute("""
        INSERT OR IGNORE INTO transactions
            (id, date, amount, merchant, category, raw_text, source)
        VALUES (?, ?, ?, ?, ?, ?, 'whatsapp')
    """, [row_id, today, amount, description, category, message])
    con.close()

    # Get updated weekly picture for this category
    from tools import get_weekly_summary
    summary  = get_weekly_summary()
    cat_data = next((c for c in summary["categories"] if c["category"] == category), None)

    lines = [f"Logged: {category} €{amount:.2f}"]
    if cat_data and cat_data.get("limit"):
        pct = cat_data.get("pct_used") or 0
        lines.append(f"{category} this week: €{cat_data['spent']:.2f} of €{cat_data['limit']:.2f} ({pct:.0f}%)")
        if pct >= 100:
            lines.append("Over budget on this one. Worth pausing here.")
        elif pct >= 85:
            lines.append("Almost at your weekly limit for this.")
    else:
        lines.append(f"Total this week: €{summary['total_spent']:.2f}")

    return {"success": True, "amount": amount, "category": category, "reply": "\n".join(lines)}


def _parse_whatsapp_expense(message: str):
    return _parse_whatsapp_message(message)


# ════════════════════════════════════════════════════════════
# CLI
# ════════════════════════════════════════════════════════════

def ingest_file(filepath: str) -> dict:
    ext = os.path.splitext(filepath)[1].lower()
    if ext == '.pdf':   return ingest_pdf(filepath)
    elif ext == '.csv': return ingest_csv(filepath)
    else: raise ValueError(f"Unsupported: {ext}. Use .csv or .pdf")


def watch_and_ingest(folder: str = "exports_drop"):
    import time, shutil
    done_dir = os.path.join(folder, "done")
    os.makedirs(done_dir, exist_ok=True)
    print(f"Watching {folder}/ for CSV/PDF files... (Ctrl+C to stop)")
    seen = set()
    while True:
        for fname in os.listdir(folder):
            if fname.endswith(('.csv', '.pdf')) and fname not in seen:
                fpath = os.path.join(folder, fname)
                print(f"\nNew file: {fname}")
                result = ingest_file(fpath)
                print(json.dumps(result, indent=2))
                shutil.move(fpath, os.path.join(done_dir, fname))
                seen.add(fname)
        time.sleep(5)


if __name__ == "__main__":
    import sys
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python ingest.py mybank.csv")
        print("  python ingest.py mybank.pdf")
        print("  python ingest.py watch")
        print('  python ingest.py msg "spent 12 euros on coffee"')
    elif sys.argv[1] == "watch":
        watch_and_ingest()
    elif sys.argv[1] == "msg":
        result = log_whatsapp_expense(" ".join(sys.argv[2:]))
        print(json.dumps(result, indent=2))
    else:
        print(json.dumps(ingest_file(sys.argv[1]), indent=2))

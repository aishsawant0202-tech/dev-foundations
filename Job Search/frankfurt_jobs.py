#!/usr/bin/env python3
"""
Frankfurt IT Job Finder
=========================================================
Finds Werkstudent / Intern / Part-time / Student Assistant roles in IT
near Frankfurt — no German required.

HOW IT WORKS
------------
Job sites (LinkedIn, XING, Stepstone, Indeed) block server-side scraping.
This script does two things:

  1. OPENS pre-filtered browser tabs — the fastest way to actually see jobs.
     Each URL has all your filters baked in (24h, role type, location, etc.)

  2. GENERATES a checklist HTML file so you can tick off jobs you've applied to.

Run: python3 frankfurt_jobs.py
Then: choose what to open, results saved as HTML checklist.
"""

import webbrowser
import urllib.parse
import json
import os
from datetime import datetime

# ── Colours ───────────────────────────────────────────────────────────────────
G  = "\033[92m"; Y = "\033[93m"; C = "\033[96m"; B = "\033[1m"; RE = "\033[0m"

# ── Pre-filtered search URLs ──────────────────────────────────────────────────

SEARCHES = {

    "LinkedIn": [
        {
            "label": "Werkstudent IT/Software — Frankfurt",
            "desc": "Internship | last 24h | remote+hybrid+onsite",
            "url": (
                "https://www.linkedin.com/jobs/search/?"
                + urllib.parse.urlencode({
                    "keywords": "Werkstudent IT Software",
                    "location": "Frankfurt Rhine-Main Metropolitan Area",
                    "f_TPR": "r86400",
                    "f_JT": "I",
                    "f_WT": "1,2,3",
                })
            ),
        },
        {
            "label": "Werkstudent Data/AI — Frankfurt",
            "desc": "Internship | last 24h",
            "url": (
                "https://www.linkedin.com/jobs/search/?"
                + urllib.parse.urlencode({
                    "keywords": "Werkstudent Data AI Machine Learning",
                    "location": "Frankfurt Rhine-Main Metropolitan Area",
                    "f_TPR": "r86400",
                    "f_JT": "I",
                    "f_WT": "1,2,3",
                })
            ),
        },
        {
            "label": "Software / IT Intern — Frankfurt (English)",
            "desc": "Internship | last 24h",
            "url": (
                "https://www.linkedin.com/jobs/search/?"
                + urllib.parse.urlencode({
                    "keywords": "Software Intern IT Intern",
                    "location": "Frankfurt Rhine-Main Metropolitan Area",
                    "f_TPR": "r86400",
                    "f_JT": "I",
                    "f_WT": "1,2,3",
                })
            ),
        },
        {
            "label": "Student Assistant IT — Frankfurt",
            "desc": "Part-time | last 24h",
            "url": (
                "https://www.linkedin.com/jobs/search/?"
                + urllib.parse.urlencode({
                    "keywords": "Student Assistant IT Technology",
                    "location": "Frankfurt Rhine-Main Metropolitan Area",
                    "f_TPR": "r86400",
                    "f_JT": "P",
                    "f_WT": "1,2,3",
                })
            ),
        },
        {
            "label": "Part-time Developer — Frankfurt (remote)",
            "desc": "Part-time | last 24h | remote",
            "url": (
                "https://www.linkedin.com/jobs/search/?"
                + urllib.parse.urlencode({
                    "keywords": "Part time Developer Software Engineer",
                    "location": "Frankfurt Rhine-Main Metropolitan Area",
                    "f_TPR": "r86400",
                    "f_JT": "P",
                    "f_WT": "2",   # remote only
                })
            ),
        },
        {
            "label": "Cloud / DevOps Werkstudent",
            "desc": "Internship | last 24h",
            "url": (
                "https://www.linkedin.com/jobs/search/?"
                + urllib.parse.urlencode({
                    "keywords": "Werkstudent Cloud DevOps Azure AWS",
                    "location": "Frankfurt Rhine-Main Metropolitan Area",
                    "f_TPR": "r86400",
                    "f_JT": "I",
                    "f_WT": "1,2,3",
                })
            ),
        },
    ],

    "XING": [
        {
            "label": "Werkstudent IT — Frankfurt",
            "desc": "Werkstudent | 30 km radius",
            "url": (
                "https://www.xing.com/jobs/search?"
                + urllib.parse.urlencode({
                    "keywords": "Werkstudent IT",
                    "location": "Frankfurt am Main",
                    "radius": "30",
                })
            ),
        },
        {
            "label": "Werkstudent Software/Developer — Frankfurt",
            "desc": "Werkstudent | 30 km radius",
            "url": (
                "https://www.xing.com/jobs/search?"
                + urllib.parse.urlencode({
                    "keywords": "Werkstudent Software Developer",
                    "location": "Frankfurt am Main",
                    "radius": "30",
                })
            ),
        },
        {
            "label": "IT Intern / Praktikum — Frankfurt",
            "desc": "Praktikum | 30 km radius",
            "url": (
                "https://www.xing.com/jobs/search?"
                + urllib.parse.urlencode({
                    "keywords": "IT Praktikum Intern English",
                    "location": "Frankfurt am Main",
                    "radius": "30",
                })
            ),
        },
    ],

    "Stepstone": [
        {
            "label": "Werkstudent IT — Frankfurt",
            "desc": "Last 24h | 30 km radius",
            "url": "https://www.stepstone.de/jobs/Werkstudent-IT/in-Frankfurt-am-Main?radius=30&datePosted=1",
        },
        {
            "label": "Werkstudent Software Engineer — Frankfurt",
            "desc": "Last 24h | 30 km radius",
            "url": "https://www.stepstone.de/jobs/Werkstudent-Software-Engineer/in-Frankfurt-am-Main?radius=30&datePosted=1",
        },
        {
            "label": "Intern Developer — Frankfurt",
            "desc": "Last 24h | 30 km radius",
            "url": "https://www.stepstone.de/jobs/Intern-Developer/in-Frankfurt-am-Main?radius=30&datePosted=1",
        },
        {
            "label": "Werkstudent Data Science — Frankfurt",
            "desc": "Last 24h | 30 km radius",
            "url": "https://www.stepstone.de/jobs/Werkstudent-Data-Science/in-Frankfurt-am-Main?radius=30&datePosted=1",
        },
        {
            "label": "Student Assistant IT — Frankfurt",
            "desc": "Last 24h | 30 km radius",
            "url": "https://www.stepstone.de/jobs/Student-Assistant-IT/in-Frankfurt-am-Main?radius=30&datePosted=1",
        },
    ],

    "Indeed DE": [
        {
            "label": "Werkstudent IT — Frankfurt (English)",
            "desc": "Last 24h | 30 km | English language filter",
            "url": (
                "https://de.indeed.com/jobs?"
                + urllib.parse.urlencode({
                    "q": "Werkstudent IT",
                    "l": "Frankfurt am Main",
                    "radius": "30",
                    "fromage": "1",
                    "lang": "en",
                })
            ),
        },
        {
            "label": "Software Intern — Frankfurt",
            "desc": "Last 24h | 30 km",
            "url": (
                "https://de.indeed.com/jobs?"
                + urllib.parse.urlencode({
                    "q": "Software Intern IT",
                    "l": "Frankfurt am Main",
                    "radius": "30",
                    "fromage": "1",
                })
            ),
        },
        {
            "label": "Part time Developer — Remote / Frankfurt",
            "desc": "Last 24h | remote + Frankfurt",
            "url": (
                "https://de.indeed.com/jobs?"
                + urllib.parse.urlencode({
                    "q": "part time developer software",
                    "l": "Frankfurt am Main",
                    "radius": "30",
                    "fromage": "1",
                    "sc": "0kf:attr(DSQF7);",  # Indeed remote filter
                })
            ),
        },
        {
            "label": "Werkstudent Cloud / DevOps",
            "desc": "Last 24h | 30 km",
            "url": (
                "https://de.indeed.com/jobs?"
                + urllib.parse.urlencode({
                    "q": "Werkstudent Cloud DevOps AWS Azure",
                    "l": "Frankfurt am Main",
                    "radius": "30",
                    "fromage": "1",
                })
            ),
        },
    ],

    "Bundesagentur (Official DE Gov)": [
        {
            "label": "Werkstudent IT — Arbeitsagentur",
            "desc": "Official German job board | free | Frankfurt",
            "url": (
                "https://www.arbeitsagentur.de/jobsuche/suche?"
                + urllib.parse.urlencode({
                    "was": "Werkstudent IT",
                    "wo": "Frankfurt am Main",
                    "umkreis": "30",
                    "veroeffentlichtseit": "1",
                    "arbeitszeit": "snl",
                })
            ),
        },
        {
            "label": "IT Praktikum — Arbeitsagentur",
            "desc": "Official German job board | Frankfurt",
            "url": (
                "https://www.arbeitsagentur.de/jobsuche/suche?"
                + urllib.parse.urlencode({
                    "was": "IT Praktikum Software",
                    "wo": "Frankfurt am Main",
                    "umkreis": "30",
                    "veroeffentlichtseit": "1",
                })
            ),
        },
    ],
}


# ── HTML checklist output ─────────────────────────────────────────────────────

def generate_html(searches: dict) -> str:
    ts = datetime.now().strftime("%d %b %Y, %H:%M")
    rows = ""
    for platform, links in searches.items():
        rows += f'<tr class="platform-row"><td colspan="3"><strong>{platform}</strong></td></tr>\n'
        for lnk in links:
            rows += (
                f'<tr>'
                f'<td><input type="checkbox" /></td>'
                f'<td><a href="{lnk["url"]}" target="_blank">{lnk["label"]}</a></td>'
                f'<td class="desc">{lnk["desc"]}</td>'
                f'</tr>\n'
            )

    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Frankfurt IT Jobs — {ts}</title>
<style>
  body {{ font-family: 'Segoe UI', sans-serif; max-width: 900px; margin: 2rem auto; color: #222; }}
  h1 {{ font-size: 1.4rem; color: #1a5276; }}
  p.meta {{ color: #555; font-size: 0.9rem; margin-bottom: 1.5rem; }}
  table {{ width: 100%; border-collapse: collapse; }}
  th {{ background: #1a5276; color: white; padding: 8px 12px; text-align: left; }}
  td {{ padding: 7px 12px; border-bottom: 1px solid #e8e8e8; vertical-align: middle; }}
  tr.platform-row td {{ background: #eaf2fb; font-size: 0.95rem; padding: 6px 12px; }}
  a {{ color: #1a5276; text-decoration: none; }}
  a:hover {{ text-decoration: underline; }}
  .desc {{ color: #777; font-size: 0.85rem; }}
  input[type=checkbox] {{ width: 16px; height: 16px; cursor: pointer; }}
  tr:hover:not(.platform-row) {{ background: #fafafa; }}
</style>
</head>
<body>
<h1>🔍 Frankfurt IT Job Search — {ts}</h1>
<p class="meta">
  Filters: <strong>last 24h</strong> · <strong>IT field</strong> ·
  <strong>Werkstudent / Intern / Part-time / Student Assistant</strong> ·
  <strong>Frankfurt ±30 km (remote / hybrid / onsite)</strong> ·
  No German required
</p>
<table>
  <thead>
    <tr>
      <th style="width:40px">Done</th>
      <th>Search</th>
      <th>Filters</th>
    </tr>
  </thead>
  <tbody>
{rows}
  </tbody>
</table>
<p style="margin-top:1.5rem; color:#777; font-size:0.8rem;">
  Generated by frankfurt_jobs.py · Tick a box once you've checked a search.
</p>
</body>
</html>"""


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    total = sum(len(v) for v in SEARCHES.items())

    print(f"\n{B}{'='*60}")
    print("  Frankfurt IT Job Finder — No German Required")
    print("  Werkstudent · Intern · Part-time · Student Assistant")
    print(f"{'='*60}{RE}\n")

    for platform, links in SEARCHES.items():
        print(f"{B}{platform}{RE} — {len(links)} pre-filtered searches:")
        for lnk in links:
            print(f"  {C}•{RE} {lnk['label']}")
            print(f"    {lnk['desc']}")
        print()

    total_links = sum(len(v) for v in SEARCHES.values())
    print(f"{G}Total: {total_links} searches across {len(SEARCHES)} platforms{RE}\n")

    # Save HTML checklist
    html = generate_html(SEARCHES)
    fname = f"frankfurt_jobs_{datetime.now().strftime('%Y%m%d_%H%M')}.html"
    with open(fname, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"{G}✅ Checklist saved: {fname}{RE}")

    # Ask which platforms to open
    print(f"\n{B}Which platforms to open in browser?{RE}")
    platforms = list(SEARCHES.keys())
    for i, p in enumerate(platforms, 1):
        print(f"  {i}. {p}")
    print(f"  a. All platforms")
    print(f"  h. Open HTML checklist only")
    print(f"  n. Skip (just use the HTML file)")

    choice = input(f"\n{C}Your choice: {RE}").strip().lower()

    import time
    to_open = []

    if choice == "n":
        pass
    elif choice == "h":
        webbrowser.open(os.path.abspath(fname))
    elif choice == "a":
        for links in SEARCHES.values():
            to_open.extend(l["url"] for l in links)
        webbrowser.open(os.path.abspath(fname))
    else:
        # Try to parse numbers
        chosen = [c.strip() for c in choice.split(",")]
        for c in chosen:
            try:
                idx = int(c) - 1
                if 0 <= idx < len(platforms):
                    to_open.extend(l["url"] for l in SEARCHES[platforms[idx]])
            except ValueError:
                pass
        webbrowser.open(os.path.abspath(fname))

    for url in to_open:
        webbrowser.open(url)
        time.sleep(0.4)

    if to_open:
        print(f"\n{G}✅ Opened {len(to_open)} browser tabs.{RE}")

    print(f"\n{G}Done! Open {fname} anytime to track your searches.{RE}\n")


if __name__ == "__main__":
    main()

# Job Alert Agent 🚨

An automated job alert system that scrapes company career pages and sends relevant job notifications via Telegram.

## What It Does

- Runs every 6 hours via a schedule trigger
- Fetches job listings from company career pages using their REST API endpoints
- Cleans and extracts job title, location, and direct link
- Uses Gemini AI to filter only relevant jobs (AI, Software Engineering, Test Automation, Werkstudent, Internship)
- Sends instant Telegram notifications with job details

## Tech Stack

- **n8n** — workflow automation (self-hosted via Docker)
- **Gemini API** — AI filtering (gemini-2.5-flash-lite, free tier)
- **Telegram Bot API** — notifications
- **JavaScript** — HTML parsing and data extraction

## Workflow

```
Schedule Trigger → Companies List → Loop Over Items → HTTP Request → JS Cleaner → Gemini AI → If (match?) → Telegram
```

## Companies Tracked

- BMW Group
- Mercedes-Benz *(coming soon)*
- Capgemini *(coming soon)*
- KPIT *(coming soon)*
- Accenture *(coming soon)*
- Amazon *(coming soon)*
- Deutsche Bank *(coming soon)*
- Audi *(coming soon)*

## Setup

### Prerequisites
- Docker installed
- Gemini API key (free at [aistudio.google.com](https://aistudio.google.com))
- Telegram Bot token (via [@BotFather](https://t.me/BotFather))
- Telegram Chat ID (via [@userinfobot](https://t.me/userinfobot))

### Run n8n

```bash
docker run -it --rm --name n8n -p 5678:5678 -v n8n_data:/home/node/.n8n docker.n8n.io/n8nio/n8n
```

Open `http://localhost:5678` and import the workflow.

### Configure Credentials

1. Add your **Gemini API key** in the Gemini node
2. Add your **Telegram Bot token** and **Chat ID** in the Telegram node

## Key Challenge

Most modern career pages load jobs dynamically via JavaScript, so standard HTML scraping returns empty results. The solution was to inspect the **Network tab** in browser DevTools to find the hidden REST API endpoints each company uses behind the scenes.

## Sample Telegram Notification

```
🚨 New Job Alerts from BMW!

Title: Werkstudent Funktionsentwicklung Autonomes Fahren (w/m/x)
Location: München
Link: https://www.bmwgroup.jobs/de/de/jobfinder/job-description.183802.html
```

## Author

Built by Aishwarya — M.Sc. Information Technology student at Frankfurt UAS

# 📧 Email Agent — AI Powered Inbox Analyzer

A personal AI agent that reads your Gmail, classifies emails, summarizes them, and drafts replies — automatically. Built with Python, FastAPI, Gmail API, and LLaMA 3.3.

---

## 🚀 What it does

Every time you hit the web UI, the agent:

1. **Fetches today's emails** from your Gmail
2. **Classifies each one** — Important or Noise
3. **Summarizes** each email in one sentence
4. **Drafts a reply** for emails that need a response
5. **Displays everything** in a clean web interface

---

## 🛠️ Tech Stack

| Technology            | Purpose             |
| --------------------- | ------------------- |
| Python                | Core language       |
| FastAPI               | REST API server     |
| Uvicorn               | Server engine       |
| Gmail API + OAuth 2.0 | Secure email access |
| Groq API (LLaMA 3.3)  | Free AI brain       |
| HTML + JavaScript     | Frontend UI         |

---

## 📁 Project Structure

```
email-agent/
├── main.py            # FastAPI server + API endpoints
├── read_emails.py     # Gmail API integration
├── ai_agent.py        # AI analysis logic
├── index.html         # Web UI
├── .env               # API keys (never pushed to GitHub)
├── credentials.json   # Google OAuth credentials (never pushed to GitHub)
├── token.json         # Google auth token (never pushed to GitHub)
└── .gitignore         # Protects sensitive files
```

---

## ⚙️ Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/yourusername/email-agent.git
cd email-agent
```

### 2. Install dependencies

```bash
pip install fastapi uvicorn groq python-dotenv google-auth google-auth-oauthlib google-auth-httplib2 google-api-python-client
```

### 3. Set up Gmail API

- Go to [Google Cloud Console](https://console.cloud.google.com)
- Create a new project
- Enable the **Gmail API**
- Create **OAuth 2.0 credentials** (Desktop app)
- Download the credentials file and rename it to `credentials.json`
- Place it in the project root folder

### 4. Get your Groq API key

- Go to [console.groq.com](https://console.groq.com)
- Sign up for free
- Create an API key

### 5. Create your `.env` file

```
GROQ_API_KEY=your_groq_api_key_here
```

### 6. Run the server

```bash
uvicorn main:app --reload
```

### 7. Open the UI

Go to:

```
<URL>/ui
```

Click **"Analyze My Inbox"** and let the AI do its thing! 🤖

---

## 🔌 API Endpoints

| Method | Endpoint         | Description                                    |
| ------ | ---------------- | ---------------------------------------------- |
| GET    | `/`              | Health check — confirms server is running      |
| GET    | `/analyze-inbox` | Fetches today's emails and returns AI analysis |
| GET    | `/ui`            | Serves the web interface                       |

---

## 🔐 Security

- Gmail access uses **OAuth 2.0** — your password is never shared with the app
- Access is **read-only** — the agent cannot send, delete, or modify emails
- All sensitive files (`credentials.json`, `token.json`, `.env`) are excluded from GitHub via `.gitignore`
- Runs **locally on your machine** — no data is sent to any external server except Gmail and Groq APIs

---

## 💡 How it works

```
You click "Analyze My Inbox"
        ↓
FastAPI receives the GET request
        ↓
Gmail API fetches today's emails (read-only)
        ↓
Each email is sent to LLaMA 3.3 via Groq
        ↓
AI classifies → summarizes → drafts reply
        ↓
Results displayed as clean cards in the UI
```

---

## 🌱 Future Ideas

- [ ] Schedule daily digest via cron job
- [ ] Send digest as a Telegram message
- [ ] Add email body reading for deeper analysis
- [ ] Filter by specific senders or labels
- [ ] One-click send for drafted replies

---

## 👩‍💻 Built by

Built as a weekend project to solve a real problem — spending too much time on emails that don't matter.

---

## 📄 License

MIT License — free to use and modify.

from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse
from fastapi import FastAPI
from read_emails import get_emails
from ai_agent import analyze_email

app = FastAPI()


@app.get("/")
def home():
    return {"message": "Email Agent is running!"}


@app.get("/analyze-inbox")
def analyze_inbox():
    emails = get_emails()

    analyzed = []
    for email in emails:
        analysis = analyze_email(email["subject"], email["sender"])
        analyzed.append({
            "subject": email["subject"],
            "sender": email["sender"],
            "category": analysis["category"],
            "summary": analysis["summary"],
            "draft_reply": analysis["draft_reply"]
        })

    return {"inbox": analyzed}


@app.get("/ui")
def serve_ui():
    return FileResponse("index.html")

import os
from groq import Groq
from dotenv import load_dotenv

load_dotenv()

client = Groq(api_key=os.getenv("GROQ_API_KEY"))


def analyze_email(subject: str, sender: str) -> dict:
    prompt = f"""
    You are an email assistant. Analyze this email and respond in this exact JSON format:
    {{
        "category": "important" or "noise",
        "summary": "one sentence summary",
        "draft_reply": "a short professional reply if needed, or null if noise"
    }}

    Email:
    Subject: {subject}
    From: {sender}

    Respond with JSON only. No extra text.
    """

    response = client.chat.completions.create(
        model="llama-3.3-70b-versatile",
        messages=[{"role": "user", "content": prompt}],
    )

    import json
    result = response.choices[0].message.content
    return json.loads(result)

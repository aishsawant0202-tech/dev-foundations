import os
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from datetime import date

SCOPES = ['https://www.googleapis.com/auth/gmail.readonly']


def get_emails():
    creds = None

    if os.path.exists('token.json'):
        creds = Credentials.from_authorized_user_file('token.json', SCOPES)

    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(
                'credentials.json', SCOPES)
            creds = flow.run_local_server(port=0)
        with open('token.json', 'w') as token:
            token.write(creds.to_json())

    service = build('gmail', 'v1', credentials=creds)

    today = date.today().strftime("%Y/%m/%d")
    results = service.users().messages().list(
        userId='me', maxResults=10, q=f"after:{today}").execute()
    messages = results.get('messages', [])

    emails = []
    for msg in messages:
        txt = service.users().messages().get(
            userId='me', id=msg['id']).execute()
        headers = txt['payload']['headers']
        subject = ""
        sender = ""
        for header in headers:
            if header['name'] == 'Subject':
                subject = header['value']
            if header['name'] == 'From':
                sender = header['value']
        emails.append({"subject": subject, "sender": sender})

    return emails


get_emails()

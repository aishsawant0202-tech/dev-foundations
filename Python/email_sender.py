import smtplib
from email.message import EmailMessage
from string import Template
from pathlib import Path
from tokenize import Name

html = Template(Path('index.html').read_text())

email = EmailMessage()
email['from'] = 'Sender\'s Name'
email['to'] = 'recipient@example.com'
email['subject'] = 'Test Email'

email.set_content(html.substitute(name='Recipient'), 'html')

with smtplib.SMTP(host='smtp.gmail.com', port=587) as smtp:
    smtp.ehlo()
    smtp.starttls()
    smtp.login('Sender\'s Email', 'Sender\'s App Password')
    smtp.send_message(email)
print('Email sent successfully!')

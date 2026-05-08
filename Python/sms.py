from twilio.rest import Client
account_sid = '<sid>'
auth_token = '<auth_token>'
client = Client(account_sid, auth_token)
message = client.messages.create(
    from_='<from_number>',
    body='Hellloooo',
    to='<to_number>'
)
print(message.sid)

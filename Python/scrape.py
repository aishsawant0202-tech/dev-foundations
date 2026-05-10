import requests
from bs4 import BeautifulSoup

response = requests.get('https://news.ycombinator.com/')
soup = BeautifulSoup(response.text, 'html.parser')
links = soup.select('.titleline')[1]
print(links)
votes = soup.select('.score')
print(votes[1].get)

import requests
from bs4 import BeautifulSoup
from sympy import li
import pprint

response = requests.get('https://news.ycombinator.com/')
soup = BeautifulSoup(response.text, 'html.parser')
links = soup.select('.titleline')
subtext = soup.select('.subtext')


def sort_stoeries_by_votes(hnlist):
    return sorted(hnlist, key=lambda k: k['votes'], reverse=True)


def create_custom_hn(links, subtext):
    hn = []
    for idx, item in enumerate(links):
        title = item.getText()
        href = item.find('a').get('href', None)
        vote = subtext[idx].select('.score')
        if len(vote):
            points = int(vote[0].getText().replace(' points', ''))
            hn.append({'title': title, 'link': href, 'votes': points})
    return sort_stoeries_by_votes(hn)


pprint.pprint(create_custom_hn(links, subtext))

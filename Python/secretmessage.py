import requests
from bs4 import BeautifulSoup


def decode_secret_message(url):
    response = requests.get(url)
    soup = BeautifulSoup(response.text, 'html.parser')

    rows = soup.find_all('tr')
    grid = {}

    for row in rows[1:]:
        cols = row.find_all('td')
        if len(cols) == 3:
            x = int(cols[0].text.strip())
            char = cols[1].text.strip()
            y = int(cols[2].text.strip())
            grid[(x, y)] = char

    if not grid:
        return

    max_x = max(x for x, y in grid)
    max_y = max(y for x, y in grid)

    for y in range(max_y + 1):
        row_str = ""
        for x in range(max_x + 1):
            row_str += grid.get((x, y), " ")
        print(row_str)


decode_secret_message(
    "https://docs.google.com/document/d/e/2PACX-1vSvM5gDlNvt7npYHhp_XfsJvuntUhq184By5xO_pA4b_gCWeXb6dM6ZxwN8rE6S4ghUsCj2VKR21oEP/pub")

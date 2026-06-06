from selenium import webdriver
import time


chrome_browser = webdriver.Chrome()

chrome_browser.get("https://www.saucedemo.com/")
print("Current title:", chrome_browser.title)
# assert "Swag Labs" in chrome_browser.title
input("Press Enter to continue...")
chrome_browser.quit()

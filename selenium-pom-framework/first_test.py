from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

driver = webdriver.Chrome(service=Service(ChromeDriverManager().install()))
driver.get("<url link>")

wait = WebDriverWait(driver, 10)

# username_field = driver.find_element(By.ID, "user-name")
wait.until(EC.visibility_of_element_located((
    By.ID, "user-name"))).send_keys("<username>")
wait.until(EC.visibility_of_element_located((
    By.ID, "password"))).send_keys("<password>")
wait.until(EC.element_to_be_clickable((By.ID, "login-button"))).click()

print(wait.until(EC.visibility_of_element_located((By.CLASS_NAME, "title"))).text)


# print(username_field.tag_name)
# print(username_field.get_attribute("placeholder"))


# print(driver.title)
# driver.find_element(By.ID, "user-name").send_keys("<username>")
# driver.find_element(By.ID, "password").send_keys("<password>")
# driver.find_element(By.ID, "login-button").click()
# print(driver.find_element(By.CLASS_NAME, "title").text)
# driver.find_element(By.NAME, "user-name")
# driver.find_element(By.CLASS_NAME, "login-box")
# driver.find_element(By.TAG_NAME, "input")
# driver.find_element(By.XPATH, "//input[@id='user-name']")
# driver.find_element(By.CSS_SELECTOR, "#user-name")


driver.quit()

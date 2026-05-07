from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC


def test_valid_login(driver):
    wait = WebDriverWait(driver, 10)

    wait.until(EC.visibility_of_element_located(
        (By.ID, "user-name"))).send_keys("standard_user")
    wait.until(EC.visibility_of_element_located(
        (By.ID, "password"))).send_keys("secret_sauce")
    wait.until(EC.element_to_be_clickable((By.ID, "login-button"))).click()

    title = wait.until(EC.visibility_of_element_located(
        (By.CLASS_NAME, "title"))).text
    assert title == "Products"
    driver.quit()


def test_invalid_login(driver):
    wait = WebDriverWait(driver, 10)

    wait.until(EC.visibility_of_element_located(
        (By.ID, "user-name"))).send_keys("invalid_user")
    wait.until(EC.visibility_of_element_located(
        (By.ID, "password"))).send_keys("invalid_password")
    wait.until(EC.element_to_be_clickable((By.ID, "login-button"))).click()

    error = wait.until(EC.visibility_of_element_located(
        (By.CSS_SELECTOR, "[data-test='error']")))
    assert "Username and password do not match any user in this service" in error.text

    driver.quit()

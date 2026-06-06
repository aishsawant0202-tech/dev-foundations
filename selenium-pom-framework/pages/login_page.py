from selenium.webdriver.common.by import By
from pages.base_page import BasePage

class LoginPage(BasePage):
    """Page object for the login page."""
    
    # Locators defined as class variables
    USERNAME = (By.ID, 'username')
    PASSWORD = (By.ID, 'password')
    LOGIN_BUTTON = (By.ID, 'login-button')
    ERROR_MSG = (By.CSS_SELECTOR, "[data-test='error']")

    # Actions
    def login(self, username, password):
        
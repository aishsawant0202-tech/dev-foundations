from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC


class BasePage:
    """Base class for all page objects."""

    def __init__(self, driver):
        self.driver = driver
        self.wait = WebDriverWait(driver, 10)

    def find(self, locator):
        """Find an element using the given locator."""
        return self.wait.until(EC.visibility_of_element_located(locator))

    def click(self, locator):
        """Click an element using the given locator."""
        return self.wait_until(EC.element_to_be_clickable(locator)).click()

    def type(self, locator, text):
        """Type text into an element using the given locator."""
        element = self.find(locator)
        element.clear()
        element.send_keys(text)

    def get_text(self, locator):
        """Get the text of an element using the given locator."""
        return self.find(locator).text

    def is_visible(self, locator):
        """Check if an element is visible using the given locator."""
        try:
            self.wait.until(EC.visibility_of_element_located(locator))
            return True
        except:
            return False

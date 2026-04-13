using OpenQA.Selenium;

namespace MotorcyclePartShop.AutomationTests.Pages
{
    public class LoginPage
    {
        private IWebDriver _driver;

        // Định nghĩa các phần tử (Locators)
        private By emailInput = By.Id("Email");
        private By passwordInput = By.Id("loginPass");
        private By loginButton = By.Id("btnLogin");

        // Constructor
        public LoginPage(IWebDriver driver)
        {
            _driver = driver;
        }

        // Các hành động trên trang
        public void EnterEmail(string email) => _driver.FindElement(emailInput).SendKeys(email);
        public void EnterPassword(string password) => _driver.FindElement(passwordInput).SendKeys(password);
        public void ClickLogin() => _driver.FindElement(loginButton).Click();

        // Hành động gộp (Facade) để gọi cho nhanh
        public void LoginToSystem(string email, string password)
        {
            EnterEmail(email);
            EnterPassword(password);
            ClickLogin();
        }
    }
}
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;

namespace MotorcyclePartShop.AutomationTests.Pages
{
    public class HomePage
    {
        private IWebDriver _driver;
        private WebDriverWait _wait;

        // 1. Định vị phần tử chuẩn xác 100% theo ID trong View của bạn
        private By userAvatar = By.Id("userAvatar");
        private By orderHistoryLink = By.Id("orderHistory");

        // Constructor
        public HomePage(IWebDriver driver)
        {
            _driver = driver;
            // Thiết lập thời gian chờ tối đa 10 giây
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        }

        // 2. Hàm click vào Avatar
        public void ClickUserAvatar()
        {
            // Chờ đến khi nút Avatar xuất hiện và có thể click được
            _wait.Until(ExpectedConditions.ElementToBeClickable(userAvatar)).Click();
        }

        // 3. Hàm click vào Order History
        public void GoToOrderHistory()
        {
            // Khi Avatar được click, Bootstrap sẽ có hiệu ứng xổ menu (dropdown) mất khoảng 0.2s
            // Hàm ElementToBeClickable sẽ tự động kiên nhẫn chờ cho menu xổ ra xong thì mới click
            _wait.Until(ExpectedConditions.ElementToBeClickable(orderHistoryLink)).Click();
        }
    }
}
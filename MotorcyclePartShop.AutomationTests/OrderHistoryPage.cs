using OpenQA.Selenium;
using System.Collections.Generic;

namespace MotorcyclePartShop.AutomationTests.Pages
{
    public class OrderHistoryPage
    {
        private IWebDriver _driver;

        // Định vị các phần tử dựa trên cấu trúc View thực tế
        private By pageTitle = By.TagName("h4"); // Tiêu đề đang dùng thẻ <h4>
        private By orderCards = By.CssSelector(".order-card"); // Mỗi đơn hàng là 1 div class order-card

        // Nút Hủy đơn (Nằm trong order-card, dành cho đơn Pending)
        private By cancelOrderButton = By.CssSelector("button[data-bs-target='#cancelModal']");

        public OrderHistoryPage(IWebDriver driver)
        {
            _driver = driver;
        }

        // Lấy tiêu đề trang để xác nhận đã chuyển trang thành công
        public string GetPageTitle()
        {
            return _driver.FindElement(pageTitle).Text;
        }

        // Đếm tổng số lượng đơn hàng đang hiển thị
        public int GetTotalOrders()
        {
            return _driver.FindElements(orderCards).Count;
        }

        // (Tùy chọn) Lấy Mã vận đơn của đơn hàng đầu tiên trong danh sách
        public string GetFirstOrderTrackingCode()
        {
            var firstCard = _driver.FindElements(orderCards)[0];
            // Mã vận đơn nằm trong thẻ span có class badge
            return firstCard.FindElement(By.CssSelector(".order-header .badge")).Text;
        }
    }
}
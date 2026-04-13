using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using MotorcyclePartShop.AutomationTests.Pages; // Gọi thư mục Pages vào

namespace MotorcyclePartShop.AutomationTests
{
    [TestFixture]
    public class OrderAutomationTest
    {
        private IWebDriver driver;

        [SetUp]
        public void Setup()
        {
            driver = new ChromeDriver();
            driver.Manage().Window.Maximize();
        }

        [Test]
        public void TC41_ViewPersonalOrderHistory_WithPOM()
        {
            // 1. Khởi tạo các trang (Pages)
            LoginPage loginPage = new LoginPage(driver);
            HomePage homePage = new HomePage(driver);
            OrderHistoryPage historyPage = new OrderHistoryPage(driver);

            // 2. Bước Đăng nhập
            driver.Navigate().GoToUrl("https://motorcyclepartshop.onrender.com/Auth/Login");
            loginPage.LoginToSystem("trivd22@uef.edu.vn", "0367586852Tri@");

            // 3. Bước Điều hướng từ Trang chủ
            homePage.ClickUserAvatar();
            homePage.GoToOrderHistory();

            // 4. Kiểm chứng kết quả (Asserts) tại trang Lịch sử
            Assert.That(driver.Url.Contains("/Order/History"), Is.True, "Lỗi: Không ở đúng URL lịch sử.");

            // Tiêu đề trang (chứa thẻ <i> và chữ "Purchase History")
            Assert.That(historyPage.GetPageTitle(), Does.Contain("Purchase History").Or.Contain("History"));

            // Kiểm tra xem có ít nhất 1 đơn hàng (order-card) được hiển thị không
            int totalOrders = historyPage.GetTotalOrders();
            Assert.That(totalOrders > 0, Is.True, "Lỗi: User này chưa có đơn hàng nào hoặc danh sách không hiển thị.");

            // In mã vận đơn của đơn đầu tiên ra màn hình Console để kiểm tra (Đã fix warning NUnit1033)
            if (totalOrders > 0)
            {
                string trackingCode = historyPage.GetFirstOrderTrackingCode();
                TestContext.Out.WriteLine($"Test Pass! Da tim thay don hang co ma: {trackingCode}");
            }

        }

        [TearDown]
        public void TearDown()
        {
            if (driver != null)
            {
                driver.Quit();
                driver.Dispose();
            }
        }
    }
}
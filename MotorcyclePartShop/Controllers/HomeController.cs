using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using System.Diagnostics;

namespace MotorcyclePartShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public HomeController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel();

            // --- 1. Get Product Data ---
            viewModel.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();

            viewModel.FeaturedProducts = await _context.Products
                .Where(p => p.IsFeatured == true && p.IsActive == true)
                .OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync();

            viewModel.NewProducts = await _context.Products
                .Where(p => p.IsActive == true)
                .OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync();

            // [FIXED] Dùng UtcNow thay cho Now để so sánh khớp với PostgreSQL
            var today = DateTime.UtcNow;
            viewModel.DiscountedProducts = await _context.Products
                .Include(p => p.PromotionProducts).ThenInclude(pp => pp.Promotion)
                .Where(p => p.IsActive == true &&
                            p.PromotionProducts.Any(pp => pp.Promotion.IsActive && pp.Promotion.StartDate <= today && pp.Promotion.EndDate >= today))
                .Take(8).ToListAsync();


            // --- 2. HANDLE TOASTR NOTIFICATIONS BASED ON SESSION ---
            var userIdStr = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("Role");
            var userName = HttpContext.Session.GetString("UserName");

            if (!string.IsNullOrEmpty(userIdStr))
            {
                // A. WELCOME TOASTR
                if (role == "Customer")
                {
                    var welcomeShown = HttpContext.Session.GetString("WelcomeShown");

                    if (string.IsNullOrEmpty(welcomeShown))
                    {
                        ViewBag.ShowWelcomeUser = true;
                        ViewBag.UserName = userName;

                        HttpContext.Session.SetString("WelcomeShown", "true");
                    }
                }

                // B. TIMEOUT ORDER TOASTR
                if (int.TryParse(userIdStr, out int userId))
                {
                    var timeoutOrder = await _context.Orders
                        .Where(o => o.UserId == userId && o.PaymentStatus == "Timeout")
                        .OrderByDescending(o => o.OrderDate)
                        .FirstOrDefaultAsync();

                    if (timeoutOrder != null)
                    {
                        ViewBag.TimeoutOrderId = timeoutOrder.OrderId;
                        ViewBag.TimeoutTrackingCode = timeoutOrder.TrackingCode;
                    }
                }
            }

            return View(viewModel);
        }

        public IActionResult Careers() { return View(); }
        public IActionResult About() { return View(); }
        public IActionResult Contact() { return View(); }
        public IActionResult ReturnPolicy() { return View(); }
        public IActionResult WarrantyPolicy() { return View(); }
        public IActionResult ShoppingGuide() { return View(); }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
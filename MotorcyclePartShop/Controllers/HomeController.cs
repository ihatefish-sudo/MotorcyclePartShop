using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using System.Diagnostics;
// using System.Security.Claims; // Not needed since we are using Session

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

            // --- 1. Get Product Data (Keep your existing logic) ---
            viewModel.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();

            viewModel.FeaturedProducts = await _context.Products
                .Where(p => p.IsFeatured == true && p.IsActive == true)
                .OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync();

            viewModel.NewProducts = await _context.Products
                .Where(p => p.IsActive == true)
                .OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync();

            var today = DateTime.Now;
            viewModel.DiscountedProducts = await _context.Products
                .Include(p => p.PromotionProducts).ThenInclude(pp => pp.Promotion)
                .Where(p => p.IsActive == true &&
                            p.PromotionProducts.Any(pp => pp.Promotion.IsActive && pp.Promotion.StartDate <= today && pp.Promotion.EndDate >= today))
                .Take(8).ToListAsync();


            // --- [NEW LOGIC] HANDLE TOASTR NOTIFICATIONS BASED ON SESSION ---

            // Retrieve User info from Session (set in AuthController)
            var userIdStr = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("Role");
            var userName = HttpContext.Session.GetString("UserName");

            // Only execute if the user is logged in
            if (!string.IsNullOrEmpty(userIdStr))
            {
                // A. WELCOME TOASTR (Only for Customers)
                if (role == "Customer")
                {
                    // Check if the notification has already been shown in this session
                    // "WelcomeShown" is a temporary key we define
                    var welcomeShown = HttpContext.Session.GetString("WelcomeShown");

                    if (string.IsNullOrEmpty(welcomeShown))
                    {
                        // Set flag for the View to display the toastr
                        ViewBag.ShowWelcomeUser = true;
                        ViewBag.UserName = userName;

                        // Mark in Session that it has been shown -> F5 will not show it again
                        HttpContext.Session.SetString("WelcomeShown", "true");
                    }
                }

                // B. TIMEOUT ORDER TOASTR (Old logic converted to use Session)
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
            // ---------------------------------------------------------

            return View(viewModel);
        }

        // ... Other Actions (Privacy, About...) keep as is
        public IActionResult Privacy() { return View(); }
        public IActionResult About() { return View(); }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
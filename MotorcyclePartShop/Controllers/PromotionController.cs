using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Để lấy Session UserId

namespace MotorcyclePartShop.Controllers
{
    public class PromotionController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public PromotionController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Now;

            // Lấy ID người dùng đang đăng nhập (nếu có)
            int? userId = null;
            if (int.TryParse(HttpContext.Session.GetString("UserId"), out int uid))
            {
                userId = uid;
            }

            // Lấy danh sách khuyến mãi đang chạy
            var promotions = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .ThenInclude(pp => pp.Product)
                .Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today)
                .OrderByDescending(p => p.EndDate)
                .ToListAsync();

            // Tạo ViewModel hoặc ViewBag để lưu trạng thái sử dụng của user với từng mã
            // Ở đây dùng ViewBag dictionary cho đơn giản: Key=PromoId, Value=Số lần đã dùng
            var userUsageMap = new Dictionary<int, int>();

            if (userId.HasValue)
            {
                // Đếm số đơn hàng của user này có gắn PromotionId tương ứng
                var usageStats = await _context.Orders
                    .Where(o => o.UserId == userId && o.PromotionId != null)
                    .GroupBy(o => o.PromotionId)
                    .Select(g => new { PromoId = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var stat in usageStats)
                {
                    if (stat.PromoId.HasValue)
                        userUsageMap[stat.PromoId.Value] = stat.Count;
                }
            }

            ViewBag.UserUsageMap = userUsageMap;
            ViewBag.IsLoggedIn = userId.HasValue;

            return View(promotions);
        }
    }
}
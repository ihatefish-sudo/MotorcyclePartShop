using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;

namespace MotorcyclePartShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public ProductController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        // GET: /Product?categoryId=1&search=lop-xe
        public async Task<IActionResult> Index(int? categoryId, int? brandId, string search)
        {
            var products = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .AsQueryable();

            // Lọc
            if (categoryId.HasValue) products = products.Where(p => p.CategoryId == categoryId.Value);
            if (brandId.HasValue) products = products.Where(p => p.BrandId == brandId.Value);

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                string kw = search.ToLower().Trim();
                products = products.Where(p =>
                    p.ProductName.Contains(kw) ||
                    p.Description.Contains(kw) ||
                    p.Brand.BrandName.Contains(kw)
                );
                ViewBag.SearchKeyword = search;
            }

            products = products.Where(p => p.IsActive == true);

            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive == true).ToListAsync();
            ViewBag.Brands = await _context.Brands.Where(b => b.BrandName != null).ToListAsync();

            return View(await products.OrderByDescending(p => p.CreatedAt).ToListAsync());
        }

        // API Gợi ý tìm kiếm (Live Search Header)
        [HttpGet]
        public async Task<IActionResult> SearchSuggestions(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
                return Json(new List<object>());

            string kw = query.ToLower().Trim();

            var suggestions = await _context.Products
                .Where(p => p.IsActive && p.ProductName.Contains(kw))
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new {
                    id = p.ProductId,
                    name = p.ProductName,
                    price = p.Price.ToString("#,##0") + "₫",
                    image = p.MainImage
                })
                .ToListAsync();

            return Json(suggestions);
        }

        // GET: /Product/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Specifications) // Load thông số
                .Include(p => p.PromotionProducts).ThenInclude(pp => pp.Promotion)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return NotFound();

            var relatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.ProductId != id && p.IsActive)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }

        // --- [CẬP NHẬT QUAN TRỌNG] ACTION COMPARE ---
        public async Task<IActionResult> Compare(string ids)
        {
            // 1. Kiểm tra đầu vào
            if (string.IsNullOrEmpty(ids))
            {
                return View(new List<Product>());
            }

            var idList = ids.Split(',')
                            .Select(id => int.TryParse(id, out int n) ? n : 0)
                            .Where(n => n > 0)
                            .ToList();

            // 2. Lấy danh sách sản phẩm + kèm bảng Thông số (Specifications)
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Specifications) // Bắt buộc phải có dòng này
                .Where(p => idList.Contains(p.ProductId))
                .ToListAsync();

            // 3. [LOGIC MỚI] Tự động lấy danh sách tên thông số (Dynamic List)
            // - SelectMany: Gom tất cả thông số của các xe lại thành 1 danh sách khổng lồ
            // - Select: Chỉ lấy cái Tên (SpecName)
            // - Distinct: Loại bỏ các tên trùng nhau (Ví dụ 2 xe đều có "Engine Type" thì chỉ giữ 1 cái)
            var allSpecNames = products
                .SelectMany(p => p.Specifications)
                .Select(s => s.SpecName)
                .Distinct()
                .OrderBy(s => s) // (Tùy chọn) Sắp xếp tên A-Z cho dễ nhìn, hoặc bỏ đi nếu muốn theo thứ tự nhập
                .ToList();

            // Gửi danh sách tự động này sang View
            ViewBag.SpecNames = allSpecNames;

            return View(products);
        }

        // API Tìm kiếm xe máy cho trang so sánh
        [HttpGet]
        public async Task<IActionResult> SearchMotorcycleJson(string query)
        {
            if (string.IsNullOrEmpty(query)) return Json(new List<object>());

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive
                            && p.ProductName.Contains(query)
                            && p.Category != null
                            && (p.Category.CategoryName.Contains("Motorcycle") ||
                                p.Category.CategoryName.Contains("Xe máy") ||
                                p.Category.CategoryName.Contains("Scooter")))
                .Select(p => new {
                    id = p.ProductId,
                    name = p.ProductName,
                    image = p.MainImage,
                    price = p.Price
                })
                .Take(5)
                .ToListAsync();

            return Json(products);
        }
    }
}
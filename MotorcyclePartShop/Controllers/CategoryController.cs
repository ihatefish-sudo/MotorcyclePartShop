using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;

namespace MotorcyclePartShop.Controllers
{
    public class CategoryController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public CategoryController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Categories.ToListAsync());
        }

        public async Task<IActionResult> Products(int id)
        {
            var products = await _context.Products
                .Where(p => p.CategoryId == id)
                .ToListAsync();

            return View("~/Views/Product/Index.cshtml", products);
        }
    }
}

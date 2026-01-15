using Microsoft.AspNetCore.Mvc;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using MotorcyclePartShop.Extensions;

namespace MotorcyclePartShop.Controllers
{
    public class CartController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public CartController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        // Helper to get cart from Session
        private List<CartItem> GetCart()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart");
            return cart ?? new List<CartItem>();
        }

        // Helper to save cart to Session
        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetObjectAsJson("Cart", cart);
        }

        // DISPLAY CART
        public IActionResult Index()
        {
            var cart = GetCart();
            // Calculate total price
            ViewBag.CartTotal = cart.Sum(item => item.Total);
            return View(cart);
        }

        // ==========================================
        // [UPDATE] ADD TO CART
        // Handle both cases: AJAX (Stay on page) and Normal Link (Buy Now -> Redirect)
        // ==========================================
        public IActionResult AddToCart(int id, int quantity = 1)
        {
            var product = _context.Products.FirstOrDefault(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == id);

            if (item == null)
            {
                // If not exists, add new
                cart.Add(new CartItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    Quantity = quantity,
                    MainImage = product.MainImage
                });
            }
            else
            {
                // If exists, increment quantity
                item.Quantity += quantity;
            }

            SaveCart(cart);

            // --- NEW LOGIC STARTS HERE ---

            // 1. Check if this request is AJAX (from "Add to Cart" button)
            // jQuery automatically sends header "X-Requested-With": "XMLHttpRequest"
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // Calculate total quantity to update Header
                int totalCount = cart.Sum(c => c.Quantity);

                return Json(new
                {
                    success = true,
                    count = totalCount,
                    message = "Product added to cart successfully!"
                });
            }

            // 2. If not AJAX (from "Buy Now" button), redirect to Cart page
            return RedirectToAction("Index");
        }

        // UPDATE QUANTITY (When clicking +/- or entering number)
        [HttpPost]
        public IActionResult UpdateCart(int id, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == id);

            if (item != null)
            {
                if (quantity > 0)
                {
                    item.Quantity = quantity;
                }
                else
                {
                    // If quantity <= 0, remove item
                    cart.Remove(item);
                }
                SaveCart(cart);
            }

            return RedirectToAction("Index");
        }

        // REMOVE PRODUCT FROM CART
        public IActionResult Remove(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == id);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            return RedirectToAction("Index");
        }

        // CLEAR CART
        public IActionResult Clear()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }
    }
}
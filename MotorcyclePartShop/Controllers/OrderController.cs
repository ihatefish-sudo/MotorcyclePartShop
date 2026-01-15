using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using MotorcyclePartShop.Extensions;
using MotorcyclePartShop.Services;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MotorcyclePartShop.Controllers
{
    public class OrderController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public OrderController(MotorcyclePartShopDbContext context, IEmailSender emailSender, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _emailSender = emailSender;
            _webHostEnvironment = webHostEnvironment;
        }

        // 1. CHECKOUT PAGE (GET)
        public async Task<IActionResult> Checkout()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index", "Cart");

            int userId = int.Parse(userIdString);
            var user = await _context.Users.FindAsync(userId);

            var model = new CheckoutViewModel
            {
                CartItems = cart,
                FullName = user?.FullName,
                Phone = user?.Phone,
                Address = user?.Address,
                TotalAmount = cart.Sum(x => x.Total)
            };

            return View(model);
        }

        // 2. PROCESS ORDER (POST)
        [HttpPost]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index", "Cart");

            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");
            int userId = int.Parse(userIdString);

            // --- Calculate Shipping Fee ---
            decimal shippingFee = 0;
            decimal cartTotal = cart.Sum(x => x.Total);

            if (cartTotal >= 200000) shippingFee = 0;
            else
            {
                if (model.Province == "Hồ Chí Minh") shippingFee = 10000;
                else if (new[] { "Bình Dương", "Đồng Nai", "Long An" }.Contains(model.Province)) shippingFee = 15000;
                else shippingFee = 20000;
            }

            var userEmail = HttpContext.Session.GetString("Email");
            if (string.IsNullOrEmpty(userEmail))
            {
                var user = await _context.Users.FindAsync(userId);
                userEmail = user?.Email;
            }

            ModelState.Remove("CartItems");
            if (ModelState.IsValid)
            {
                decimal finalTotal = cartTotal;
                decimal discountAmount = 0;
                int? appliedPromotionId = null;

                // --- Coupon Processing Logic ---
                if (!string.IsNullOrEmpty(model.CouponCode))
                {
                    var today = DateTime.Now;
                    var promo = await _context.Promotions
                        .Include(p => p.PromotionProducts)
                        .FirstOrDefaultAsync(p => p.PromoCode == model.CouponCode && p.IsActive && p.StartDate <= today && p.EndDate >= today);

                    if (promo != null)
                    {
                        int usedCount = await _context.Orders.CountAsync(o => o.UserId == userId && o.PromotionId == promo.PromotionId);

                        if (usedCount < promo.UsageLimitPerUser)
                        {
                            var applicableProductIds = promo.PromotionProducts.Select(pp => pp.ProductId).ToList();
                            bool isGlobalCoupon = !applicableProductIds.Any();

                            foreach (var item in cart)
                            {
                                if (isGlobalCoupon || applicableProductIds.Contains(item.ProductId))
                                {
                                    discountAmount += item.Total * (decimal)(promo.DiscountPercent / 100.0);
                                }
                            }
                            finalTotal -= discountAmount;
                            appliedPromotionId = promo.PromotionId;
                        }
                    }
                }

                finalTotal += shippingFee;

                // --- Create Order ---
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    TotalAmount = finalTotal,
                    PaymentStatus = "Pending",
                    DeliveryStatus = "Pending",
                    ShippingAddress = $"{model.Address}, {model.Province} (Receiver: {model.FullName}, Phone: {model.Phone}, Note: {model.Note}) {(string.IsNullOrEmpty(model.CouponCode) ? "" : $"[Coupon: {model.CouponCode} - Disc: {discountAmount:N0}đ]")}",
                    PaymentMethod = model.PaymentMethod,
                    TrackingCode = "ORD" + DateTime.Now.Ticks.ToString().Substring(10),
                    ShippingFee = shippingFee,
                    PromotionId = appliedPromotionId
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // --- Save Order Items ---
                foreach (var item in cart)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    };
                    _context.OrderItems.Add(orderItem);

                    // Update Stock
                    var p = await _context.Products.FindAsync(item.ProductId);
                    if (p != null) p.Stock = Math.Max(0, p.Stock - item.Quantity);
                }

                // --- Order Tracking Log ---
                _context.OrderTracking.Add(new OrderTracking
                {
                    OrderId = order.OrderId,
                    Status = "Order placed successfully.",
                    UpdatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                // VNPAY INTEGRATION
                if (model.PaymentMethod == "VNPAY")
                {
                    HttpContext.Session.Remove("Cart");
                    return RedirectToAction("CreatePaymentUrl", "Payment", new { orderId = order.OrderId });
                }

                // --- Email Notification ---
                try
                {
                    string emailBody = GetEmailBody(order, cart, model);
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        await _emailSender.SendEmailAsync(userEmail, $"Order Confirmation #{order.TrackingCode}", emailBody);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Email send error: " + ex.Message);
                }

                HttpContext.Session.Remove("Cart");
                return RedirectToAction("OrderSuccess", new { id = order.TrackingCode });
            }

            model.CartItems = cart;
            return View(model);
        }

        // [POST] CHECK COUPON (AJAX)
        [HttpPost]
        public async Task<IActionResult> CheckCoupon(string code)
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return Json(new { success = false, message = "Cart is empty" });

            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Please login to use coupons." });
            }
            int userId = int.Parse(userIdString);

            var today = DateTime.Now;
            var promo = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .FirstOrDefaultAsync(p => p.PromoCode == code && p.IsActive);

            if (promo == null)
            {
                return Json(new { success = false, message = "Invalid coupon code." });
            }

            if (DateTime.Now < promo.StartDate || DateTime.Now > promo.EndDate)
            {
                return Json(new { success = false, message = "Coupon is expired or not started yet." });
            }

            // Check Usage Limit Per User
            int usedCount = await _context.Orders.CountAsync(o => o.UserId == userId && o.PromotionId == promo.PromotionId);
            if (usedCount >= promo.UsageLimitPerUser)
            {
                return Json(new { success = false, message = $"You have reached the usage limit for this coupon ({usedCount}/{promo.UsageLimitPerUser})." });
            }

            decimal totalCart = cart.Sum(x => x.Total);
            decimal discountAmt = 0;

            var applicableProductIds = promo.PromotionProducts.Select(pp => pp.ProductId).ToList();
            bool isGlobalCoupon = !applicableProductIds.Any();

            foreach (var item in cart)
            {
                if (isGlobalCoupon || applicableProductIds.Contains(item.ProductId))
                {
                    discountAmt += item.Total * (decimal)(promo.DiscountPercent / 100.0);
                }
            }

            bool isFreeShip = promo.PromoCode.ToUpper().Contains("SHIP");

            return Json(new
            {
                success = true,
                discount = discountAmt,
                percent = promo.DiscountPercent,
                isFreeShip = isFreeShip,
                message = "Coupon applied successfully!"
            });
        }

        public IActionResult OrderSuccess(string id)
        {
            ViewBag.TrackingCode = id;
            return View();
        }

        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");
            int userId = int.Parse(userIdString);

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order != null && order.DeliveryStatus == "Shipping")
            {
                order.DeliveryStatus = "Completed";
                if (order.PaymentMethod == "COD") order.PaymentStatus = "Paid";

                _context.OrderTracking.Add(new OrderTracking
                {
                    OrderId = id,
                    Status = "Customer confirmed receipt of goods.",
                    UpdatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Thank you for your purchase!";
            }

            return RedirectToAction("History");
        }

        [HttpGet]
        public async Task<IActionResult> TrackOrder(string code)
        {
            if (string.IsNullOrEmpty(code)) return View(null);

            var order = await _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.Tracking)
                .FirstOrDefaultAsync(o => o.TrackingCode == code);

            if (order == null)
            {
                ViewBag.Error = "Order not found! Please check the tracking code.";
                ViewBag.SearchedCode = code;
                return View(null);
            }

            return View(order);
        }

        [HttpPost]
        public IActionResult GetShippingFee(string province, decimal totalCartValue)
        {
            decimal shippingFee = 0;
            if (totalCartValue >= 200000) return Json(new { fee = 0, message = "Free shipping (Order > 200k)" });

            if (string.IsNullOrEmpty(province)) shippingFee = 0;
            else if (province == "Hồ Chí Minh") shippingFee = 10000;
            else if (new[] { "Bình Dương", "Đồng Nai", "Long An", "Bà Rịa - Vũng Tàu" }.Contains(province)) shippingFee = 15000;
            else shippingFee = 20000;

            return Json(new { fee = shippingFee });
        }

        public async Task<IActionResult> History()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");
            int userId = int.Parse(userIdString);

            var orders = await _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");
            int userId = int.Parse(userIdString);

            var order = await _context.Orders
                .Include(o => o.Items).ThenInclude(oi => oi.Product)
                .Include(o => o.Tracking)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            if (order.UserId != userId) return RedirectToAction("History");

            return View(order);
        }

        // Return features
        [HttpGet]
        public IActionResult Return(int id)
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");
            int userId = int.Parse(userIdString);

            var order = _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .FirstOrDefault(o => o.OrderId == id && o.UserId == userId);

            if (order == null || order.DeliveryStatus != "Completed")
            {
                TempData["Error"] = "The order is invalid or incomplete and therefore cannot be returned.";
                return RedirectToAction("Details", new { id = id });
            }

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitReturn(int OrderId, string Reason, string Note, IFormFile EvidenceImage)
        {
            try
            {
                if (OrderId <= 0) throw new Exception("Invalid order code.");
                if (string.IsNullOrEmpty(Reason)) throw new Exception("Please select the reason for the return.");

                string uniqueFileName = null;
                if (EvidenceImage != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "returns");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    uniqueFileName = Guid.NewGuid().ToString() + "_" + EvidenceImage.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await EvidenceImage.CopyToAsync(fileStream);
                    }
                }
                else
                {
                    throw new Exception("Please upload photographic proof.");
                }

                var pOrderId = new SqlParameter("@OrderId", OrderId);
                var pReason = new SqlParameter("@Reason", Reason);
                var pNote = new SqlParameter("@Note", Note ?? "");
                var pImage = new SqlParameter("@ImageEvidence", uniqueFileName ?? (object)DBNull.Value);

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_CreateReturnRequest @OrderId, @Reason, @Note, @ImageEvidence",
                    pOrderId, pReason, pNote, pImage
                );

                TempData["Success"] = "Request submitted successfully!";
                return RedirectToAction("History");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "System error: " + ex.Message;
                return RedirectToAction("Return", new { id = OrderId });
            }
        }

        // =========================================================
        // [NEW FEATURE] CANCEL ORDER
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            // 1. Auth Check
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Auth");
            int userId = int.Parse(userIdString);

            // 2. Find Order & Include Items to restore stock
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("History");
            }

            // 3. Status Check: Only Pending can be cancelled
            if (order.DeliveryStatus?.ToUpper() == "PENDING")
            {
                // a. Change Delivery Status
                order.DeliveryStatus = "CANCELLED";

                // b. Change Payment Status based on method
                if (order.PaymentMethod == "VNPAY")
                {
                    order.PaymentStatus = "Refunded";
                }
                else
                {
                    order.PaymentStatus = "Cancelled";
                }

                // c. Restore Stock (Quan trọng)
                if (order.Items != null && order.Items.Any())
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.Stock += item.Quantity; // Hoàn lại kho
                        }
                    }
                }

                // d. Add Tracking Log
                _context.OrderTracking.Add(new OrderTracking
                {
                    OrderId = order.OrderId,
                    Status = $"Order cancelled by customer. Payment: {order.PaymentStatus}",
                    UpdatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Order has been cancelled successfully. Stock has been restored.";
            }
            else
            {
                TempData["Error"] = "Cannot cancel order. It might have been processed or shipped already.";
            }

            return RedirectToAction("Details", new { id = id });
        }


        // --- Helper for Email ---
        private string GetEmailBody(Order order, List<CartItem> cart, CheckoutViewModel model)
        {
            string itemsHtml = "";
            foreach (var item in cart)
            {
                itemsHtml += $@"
                    <tr>
                        <td style='padding: 8px; border-bottom: 1px solid #ddd;'>{item.ProductName}</td>
                        <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: center;'>{item.Quantity}</td>
                        <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{item.Price.ToString("#,##0")}đ</td>
                        <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{item.Total.ToString("#,##0")}đ</td>
                    </tr>";
            }

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #ddd;'>
                    <div style='background-color: #d70018; padding: 20px; text-align: center; color: white;'>
                        <h2 style='margin: 0;'>ORDER CONFIRMATION</h2>
                    </div>
                    <div style='padding: 20px;'>
                        <p>Hello <strong>{model.FullName}</strong>,</p>
                        <p>Thank you for shopping at <strong>Moto Shop</strong>. Your order is currently being processed.</p>
                        
                        <h3 style='border-bottom: 2px solid #d70018; padding-bottom: 10px;'>Order Info #{order.TrackingCode}</h3>
                        <p><strong>Date:</strong> {order.OrderDate:dd/MM/yyyy HH:mm}</p>
                        <p><strong>Shipping Address:</strong> {model.Address}</p>
                        <p><strong>Phone:</strong> {model.Phone}</p>
                        <p><strong>Payment Method:</strong> {model.PaymentMethod}</p>

                        <h3 style='margin-top: 20px;'>Product Details</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <thead>
                                <tr style='background-color: #f8f9fa;'>
                                    <th style='padding: 8px; text-align: left;'>Product</th>
                                    <th style='padding: 8px; text-align: center;'>Qty</th>
                                    <th style='padding: 8px; text-align: right;'>Price</th>
                                    <th style='padding: 8px; text-align: right;'>Total</th>
                                </tr>
                            </thead>
                            <tbody>
                                {itemsHtml}
                            </tbody>
                            <tfoot>
                                <tr>
                                    <td colspan='3' style='padding: 15px; text-align: right; font-weight: bold;'>Grand Total:</td>
                                    <td style='padding: 15px; text-align: right; font-weight: bold; color: #d70018; font-size: 18px;'>{order.TotalAmount.ToString("#,##0")}đ</td>
                                </tr>
                            </tfoot>
                        </table>

                        <p style='margin-top: 30px; font-size: 13px; color: #666;'>
                            If you have any questions, please contact hotline 1900 xxxx.<br>
                            Best regards,<br>
                            <strong>Moto Shop Team</strong>
                        </p>
                    </div>
                </div>";
        }
    }
}
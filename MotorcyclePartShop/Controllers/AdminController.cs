using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using Microsoft.AspNetCore.Http; 
using Microsoft.Data.SqlClient;
using MotorcyclePartShop.Utilities;
using System.IO; 

namespace MotorcyclePartShop.Controllers
{
    public class AdminController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(MotorcyclePartShopDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Helper: Check Admin permission
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("Role") == "Admin";
        }

        // ==========================================
        // 1. DASHBOARD & REPORTS (THỐNG KÊ)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            // [FIXED] Security Check
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // 1. Thống kê tổng quan
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();

            ViewBag.TotalRevenue = await _context.Orders
                .Where(o => o.PaymentStatus == "Paid" || o.DeliveryStatus == "Completed")
                .SumAsync(o => o.TotalAmount);

            // 2. Thống kê Doanh thu chi tiết
            var today = DateTime.Now.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = today.AddDays(-1 * diff).Date;

            ViewBag.RevenueToday = await _context.Orders
                .Where(o => o.OrderDate >= today && (o.PaymentStatus == "Paid" || o.DeliveryStatus == "Completed"))
                .SumAsync(o => o.TotalAmount);

            ViewBag.RevenueWeek = await _context.Orders
                .Where(o => o.OrderDate >= startOfWeek && (o.PaymentStatus == "Paid" || o.DeliveryStatus == "Completed"))
                .SumAsync(o => o.TotalAmount);

            ViewBag.RevenueMonth = await _context.Orders
                .Where(o => o.OrderDate >= startOfMonth && (o.PaymentStatus == "Paid" || o.DeliveryStatus == "Completed"))
                .SumAsync(o => o.TotalAmount);

            // 3. Biểu đồ Doanh thu 12 tháng
            var currentYear = DateTime.Now.Year;
            var revenueData = await _context.Orders
                .Where(o => o.OrderDate.Year == currentYear && (o.PaymentStatus == "Paid" || o.DeliveryStatus == "Completed"))
                .GroupBy(o => o.OrderDate.Month)
                .Select(g => new { Month = g.Key, Revenue = g.Sum(o => o.TotalAmount) })
                .ToListAsync();

            decimal[] monthlyRevenue = new decimal[12];
            foreach (var item in revenueData)
            {
                // [FIXED] Safety check to prevent crash if Month is invalid
                if (item.Month >= 1 && item.Month <= 12)
                {
                    monthlyRevenue[item.Month - 1] = item.Revenue;
                }
            }
            ViewBag.RevenueChartData = monthlyRevenue;

            // 4. Biểu đồ Trạng thái đơn hàng
            var statusCounts = await _context.Orders
                .GroupBy(o => o.DeliveryStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.StatusLabels = statusCounts.Select(x => x.Status).ToList();
            ViewBag.StatusData = statusCounts.Select(x => x.Count).ToList();

            // 5. Top 5 Sản phẩm bán chạy
            var topProducts = await _context.OrderItems
                .Include(oi => oi.Product)
                .GroupBy(oi => new { oi.ProductId, oi.Product.ProductName, oi.Product.MainImage, oi.Product.Price })
                .Select(g => new
                {
                    Name = g.Key.ProductName,
                    Image = g.Key.MainImage,
                    Price = g.Key.Price,
                    TotalSold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.Price)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(5)
                .ToListAsync();

            ViewBag.TopProducts = topProducts;

            var bottomProducts = await _context.OrderItems
        .Include(oi => oi.Product)
        .GroupBy(oi => new { oi.ProductId, oi.Product.ProductName, oi.Product.MainImage, oi.Product.Price })
        .Select(g => new
        {
            Name = g.Key.ProductName,
            Image = g.Key.MainImage,
            Price = g.Key.Price,
            TotalSold = g.Sum(oi => oi.Quantity),
            TotalRevenue = g.Sum(oi => oi.Quantity * oi.Price)
        })
        .OrderBy(x => x.TotalSold) // Sắp xếp tăng dần (Ít nhất lên đầu)
        .Take(5)
        .ToListAsync();

            ViewBag.BottomProducts = bottomProducts;

            return View();
         
        }

        // ==========================================
        // 2. PRODUCT MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Products(string search, string sortOrder, string statusFilter)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.ProductName.Contains(search) || p.ProductId.ToString() == search);
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "active") query = query.Where(p => p.IsActive == true);
                else if (statusFilter == "inactive") query = query.Where(p => p.IsActive == false);
            }

            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentFilter = search;
            ViewBag.StatusFilter = statusFilter;

            switch (sortOrder)
            {
                case "price_asc": query = query.OrderBy(p => p.Price); break;
                case "price_desc": query = query.OrderByDescending(p => p.Price); break;
                case "stock_asc": query = query.OrderBy(p => p.Stock); break;
                case "stock_desc": query = query.OrderByDescending(p => p.Stock); break;
                case "date_asc": query = query.OrderBy(p => p.CreatedAt); break;
                default: query = query.OrderByDescending(p => p.CreatedAt); break;
            }

            return View(await query.ToListAsync());
        }

        public IActionResult CreateProduct()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.Brands = _context.Brands.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product model, IFormFile imageFile)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // --- BỔ SUNG CÁC DÒNG NÀY ---
            // Bỏ qua kiểm tra object liên kết (Navigation properties)
            ModelState.Remove("MainImage"); // Đã có từ trước
            ModelState.Remove("Category");  // <--- Thêm dòng này
            ModelState.Remove("Brand");     // <--- Thêm dòng này
            // ----------------------------

            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images/products");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    string filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    model.MainImage = fileName;
                }
                else
                {
                    model.MainImage = "default.png";
                }

                model.CreatedAt = DateTime.Now;
                _context.Products.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm sản phẩm thành công!"; // Thêm thông báo
                return RedirectToAction("Products");
            }

            // Nếu vẫn lỗi thì load lại danh sách để hiện form
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.Brands = _context.Brands.ToList();
            return View(model);
        }

        public async Task<IActionResult> EditProduct(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var product = await _context.Products
                .Include(p => p.Specifications)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return NotFound();

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.Brands = await _context.Brands.ToListAsync();

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(int id, Product model, IFormFile? imageFile, List<ProductSpecification> specs)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            if (id != model.ProductId) return NotFound();

            ModelState.Remove("Brand");
            ModelState.Remove("Category");
            ModelState.Remove("Images");
            ModelState.Remove("PromotionProducts");
            ModelState.Remove("MainImage");

            foreach (var key in ModelState.Keys)
            {
                if (key.StartsWith("specs") || key.StartsWith("Specifications"))
                    ModelState.Remove(key);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingProduct = await _context.Products
                        .Include(p => p.Specifications)
                        .FirstOrDefaultAsync(p => p.ProductId == id);

                    if (existingProduct == null) return NotFound();

                    existingProduct.ProductName = model.ProductName;
                    existingProduct.Price = model.Price;
                    existingProduct.Stock = model.Stock;
                    existingProduct.Description = model.Description;
                    existingProduct.CategoryId = model.CategoryId;
                    existingProduct.BrandId = model.BrandId;
                    existingProduct.IsActive = model.IsActive;
                    existingProduct.IsFeatured = model.IsFeatured;

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        string folder = Path.Combine(_webHostEnvironment.WebRootPath, "images/products");
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                        // [FIXED] Delete old image
                        if (!string.IsNullOrEmpty(existingProduct.MainImage))
                        {
                            string oldPath = Path.Combine(folder, existingProduct.MainImage);
                            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                        }

                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                        string filePath = Path.Combine(folder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(stream);
                        }
                        existingProduct.MainImage = fileName;
                    }

                    _context.ProductSpecifications.RemoveRange(existingProduct.Specifications);
                    if (specs != null && specs.Any())
                    {
                        foreach (var s in specs)
                        {
                            if (!string.IsNullOrEmpty(s.SpecName) && !string.IsNullOrEmpty(s.SpecValue))
                            {
                                _context.ProductSpecifications.Add(new ProductSpecification
                                {
                                    ProductId = id,
                                    SpecName = s.SpecName,
                                    SpecValue = s.SpecValue
                                });
                            }
                        }
                    }

                    _context.Update(existingProduct);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Product updated successfully!";
                    return RedirectToAction(nameof(Products));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "System error: " + ex.Message);
                }
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.Brands = await _context.Brands.ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Soft delete
                product.IsActive = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Products");
        }

        // ==========================================
        // 3. ORDER MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Orders(string search, string status, string payment, string dateRange)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var query = _context.Orders
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.TrackingCode.Contains(search) || (o.User != null && o.User.FullName.Contains(search)));
            }
            if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.DeliveryStatus == status);
            if (!string.IsNullOrEmpty(payment)) query = query.Where(o => o.PaymentStatus == payment);

            if (!string.IsNullOrEmpty(dateRange))
            {
                var today = DateTime.Now.Date;
                switch (dateRange)
                {
                    case "today": query = query.Where(o => o.OrderDate.Date == today); break;
                    case "week": query = query.Where(o => o.OrderDate >= today.AddDays(-7)); break;
                    case "month": query = query.Where(o => o.OrderDate >= today.AddDays(-30)); break;
                }
            }

            query = query.OrderByDescending(o => o.OrderDate);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPayment = payment;
            ViewBag.CurrentDateRange = dateRange;

            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var order = await _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int OrderId, string DeliveryStatus, string PaymentStatus)
        {
            var order = await _context.Orders.FindAsync(OrderId);
            if (order == null)
            {
                TempData["Error"] = "Order not found!";
                return RedirectToAction("Orders");
            }

            // Server-side Validation: Chặn nếu cố tình hack HTML để sửa
            if (order.DeliveryStatus == "Completed" && DeliveryStatus != "Completed")
            {
                TempData["Error"] = "Cannot change delivery status once Completed!";
                return RedirectToAction("Orders");
            }

            if (order.PaymentStatus == "Paid" && PaymentStatus != "Paid")
            {
                TempData["Error"] = "Cannot change payment status once Paid!";
                return RedirectToAction("Orders");
            }

            // Cập nhật
            bool isChanged = false;
            if (order.DeliveryStatus != DeliveryStatus)
            {
                order.DeliveryStatus = DeliveryStatus;
                // Logic thêm OrderTracking nếu cần
                isChanged = true;
            }

            if (order.PaymentStatus != PaymentStatus)
            {
                order.PaymentStatus = PaymentStatus;
                isChanged = true;
            }

            if (isChanged)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Order status updated successfully!";
            }
            else
            {
                TempData["Success"] = "No changes made.";
            }

            return RedirectToAction("Orders");
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string paymentStatus)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.PaymentStatus = paymentStatus;
                _context.OrderTracking.Add(new OrderTracking
                {
                    OrderId = orderId,
                    Status = $"Admin updated payment status: {paymentStatus}",
                    UpdatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        // ==========================================
        // 4. PROMOTIONS
        // ==========================================
        public async Task<IActionResult> Promotions(string search, string status)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var query = _context.Promotions.AsQueryable();

            if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.PromoCode.Contains(search));
            if (!string.IsNullOrEmpty(status))
            {
                var now = DateTime.Now;
                if (status == "active") query = query.Where(p => p.IsActive && p.EndDate >= now);
                else if (status == "expired") query = query.Where(p => !p.IsActive || p.EndDate < now);
            }

            query = query.OrderByDescending(p => p.StartDate);
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;

            return View(await query.ToListAsync());
        }


        // [GET] Create Promotion
        public async Task<IActionResult> CreatePromotion() // Changed to async to await list
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // Load products for selection in the view
            ViewBag.AllProducts = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new { p.ProductId, p.ProductName })
                .ToListAsync();

            return View();
        }

        // [POST] Create Promotion
        [HttpPost]
        public async Task<IActionResult> CreatePromotion(Promotion model, List<int> selectedProducts)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // 1. Remove validation for the navigation property
            ModelState.Remove("PromotionProducts");

            // 2. Custom Validation Logic
            if (await _context.Promotions.AnyAsync(p => p.PromoCode == model.PromoCode))
                ModelState.AddModelError("PromoCode", "This Promo Code already exists!");

            if (model.StartDate > model.EndDate)
                ModelState.AddModelError("EndDate", "End Date must be greater than Start Date.");

            if (ModelState.IsValid)
            {
                // 3. Save Promotion
                model.PromoCode = model.PromoCode.ToUpper();
                _context.Promotions.Add(model);
                await _context.SaveChangesAsync(); // Save first to generate PromotionId

                // 4. Save Selected Products (if any)
                if (selectedProducts != null && selectedProducts.Any())
                {
                    foreach (var productId in selectedProducts)
                    {
                        _context.PromotionProducts.Add(new PromotionProduct
                        {
                            PromotionId = model.PromotionId,
                            ProductId = productId
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Promotion created successfully!";
                return RedirectToAction("Promotions");
            }

            // 5. Repopulate ViewBag if validation fails
            ViewBag.AllProducts = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new { p.ProductId, p.ProductName })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> EditPromotion(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var promo = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .FirstOrDefaultAsync(p => p.PromotionId == id);

            if (promo == null) return NotFound();

            ViewBag.AllProducts = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new { p.ProductId, p.ProductName })
                .ToListAsync();

            ViewBag.SelectedProductIds = promo.PromotionProducts.Select(pp => pp.ProductId).ToList();
            return View(promo);
        }

        [HttpPost]
        public async Task<IActionResult> EditPromotion(Promotion model, List<int> selectedProducts)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            ModelState.Remove("PromotionProducts");

            if (ModelState.IsValid)
            {
                var existingPromo = await _context.Promotions
                    .Include(p => p.PromotionProducts)
                    .FirstOrDefaultAsync(p => p.PromotionId == model.PromotionId);

                if (existingPromo == null) return NotFound();

                existingPromo.PromoCode = model.PromoCode;
                existingPromo.DiscountPercent = model.DiscountPercent;
                existingPromo.StartDate = model.StartDate;
                existingPromo.EndDate = model.EndDate;
                existingPromo.IsActive = model.IsActive;

                _context.PromotionProducts.RemoveRange(existingPromo.PromotionProducts);

                if (selectedProducts != null && selectedProducts.Any())
                {
                    foreach (var productId in selectedProducts)
                    {
                        _context.PromotionProducts.Add(new PromotionProduct
                        {
                            PromotionId = model.PromotionId,
                            ProductId = productId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Promotion updated successfully!";
                return RedirectToAction("Promotions");
            }
            TempData["Error"] = "Please correct the errors below.";
            ViewBag.AllProducts = await _context.Products.Where(p => p.IsActive).ToListAsync();
            ViewBag.SelectedProductIds = selectedProducts;
            return View(model);
        }

        public async Task<IActionResult> DeletePromotion(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var promo = await _context.Promotions.FindAsync(id);
            if (promo != null)
            {
                _context.Promotions.Remove(promo);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Deleted promotion successfully!";
            }
            return RedirectToAction("Promotions");
        }

        // ==========================================
        // 5. USER MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Users(string search, string role, string status)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var query = _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search) || u.Phone.Contains(search));

            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == role));

            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(u => u.IsActive == isActive);
            }

            query = query.OrderByDescending(u => u.CreatedAt);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentRole = role;
            ViewBag.CurrentStatus = status;
            ViewBag.Roles = await _context.Roles.ToListAsync();

            return View(await query.ToListAsync());
        }

        public IActionResult CreateUser()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.Roles = _context.Roles.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(User model, int RoleId, string Password)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                ModelState.AddModelError("Email", "This email already exists!");

            if (ModelState.IsValid)
            {
                model.PasswordHash = SecurityHelper.HashPassword(Password);
                model.CreatedAt = DateTime.Now;
                model.IsActive = true;

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                if (RoleId > 0)
                {
                    _context.UserRoles.Add(new UserRole { UserId = model.UserId, RoleId = RoleId });
                    await _context.SaveChangesAsync();
                }
                TempData["Success"] = "User added successfully!";
                return RedirectToAction("Users");
            }

            ViewBag.Roles = _context.Roles.ToList();
            return View(model);
        }

        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
            }
            TempData["Success"] = user.IsActive ? "User account unlocked!" : "User account locked!";
            return RedirectToAction("Users");
        }

        public async Task<IActionResult> EditUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            ViewBag.Roles = await _context.Roles.ToListAsync();
            var currentRole = user.UserRoles.FirstOrDefault();
            ViewBag.CurrentRoleId = currentRole?.RoleId ?? 0;

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(User model, int RoleId, string? NewPassword)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;
            user.IsActive = model.IsActive;

            if (!string.IsNullOrEmpty(NewPassword))
            {
                user.PasswordHash = SecurityHelper.HashPassword(NewPassword);
            }

            var oldRoles = _context.UserRoles.Where(ur => ur.UserId == model.UserId);
            _context.UserRoles.RemoveRange(oldRoles);

            if (RoleId > 0)
            {
                _context.UserRoles.Add(new UserRole { UserId = model.UserId, RoleId = RoleId });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "User updated successfully!";
            return RedirectToAction("Users");
        }

        // ==========================================
        // 6. CATEGORIES
        // ==========================================
        public async Task<IActionResult> Categories(string search, string status)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var query = _context.Categories.AsQueryable();
            if (!string.IsNullOrEmpty(search)) query = query.Where(c => c.CategoryName.Contains(search));
            if (!string.IsNullOrEmpty(status))
            {
                bool isActive = status == "active";
                query = query.Where(c => c.IsActive == isActive);
            }
            query = query.OrderByDescending(c => c.CategoryId);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            return View(await query.ToListAsync());
        }

        public IActionResult CreateCategory()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory(Category model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            if (await _context.Categories.AnyAsync(c => c.CategoryName == model.CategoryName))
                ModelState.AddModelError("CategoryName", "Category name already exists!");

            if (ModelState.IsValid)
            {
                _context.Categories.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Category added successfully!";
                return RedirectToAction(nameof(Categories));
            }
            return View(model);
        }

        public async Task<IActionResult> EditCategory(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> EditCategory(Category model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            if (await _context.Categories.AnyAsync(c => c.CategoryName == model.CategoryName && c.CategoryId != model.CategoryId))
                ModelState.AddModelError("CategoryName", "Category name already exists!");

            if (ModelState.IsValid)
            {
                // Call Stored Procedure
                var pId = new SqlParameter("@CategoryId", model.CategoryId);
                var pName = new SqlParameter("@CategoryName", model.CategoryName);
                var pActive = new SqlParameter("@IsActive", model.IsActive);

                await _context.Database.ExecuteSqlRawAsync("EXEC sp_UpdateCategory @CategoryId, @CategoryName, @IsActive", pId, pName, pActive);

                TempData["Success"] = "Category updated successfully!";
                return RedirectToAction(nameof(Categories));
            }
            return View(model);
        }
        // ==========================================
        // 6. QUẢN LÝ YÊU CẦU TRẢ HÀNG (RETURN REQUESTS)
        // ==========================================

        // [GET] Danh sách yêu cầu
        public async Task<IActionResult> ReturnRequests(string status)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var query = _context.ReturnRequests
                .Include(r => r.Order)
                .ThenInclude(o => o.User) // Để hiển thị tên khách hàng
                .AsQueryable();

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }
            else
            {
                // Mặc định ưu tiên hiện 'Pending' lên đầu
                query = query.OrderByDescending(r => r.Status == "Pending").ThenByDescending(r => r.RequestedAt);
            }

            ViewBag.CurrentStatus = status;
            return View(await query.ToListAsync());
        }

        // [POST] Xử lý Duyệt / Từ chối
        [HttpPost]
        public async Task<IActionResult> ProcessReturn(int ReturnId, string Status, string AdminNote)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            try
            {
                var pReturnId = new SqlParameter("@ReturnId", ReturnId);
                var pStatus = new SqlParameter("@Status", Status);
                var pNote = new SqlParameter("@AdminNote", AdminNote ?? "");

                // Gọi Stored Procedure
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_ProcessReturnRequest @ReturnId, @Status, @AdminNote",
                    pReturnId, pStatus, pNote
                );

                // --- SỬA THÀNH TIẾNG ANH Ở ĐÂY ---
                string statusText = Status == "Approved" ? "approved" : "rejected";
                TempData["Success"] = $"Return request has been {statusText} successfully!";
            }
            catch (Exception ex)
            {
                // --- SỬA THÀNH TIẾNG ANH Ở ĐÂY ---
                TempData["Error"] = "Error processing request: " + ex.Message;
            }

            return RedirectToAction("ReturnRequests");
        }
        // [GET] Xem chi tiết yêu cầu trả hàng
        public async Task<IActionResult> ReturnRequestDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var request = await _context.ReturnRequests
                .Include(r => r.Order).ThenInclude(o => o.Items).ThenInclude(i => i.Product) // Load sản phẩm trong đơn
                .Include(r => r.Order).ThenInclude(o => o.User) // Load thông tin khách hàng
                .FirstOrDefaultAsync(r => r.ReturnId == id);

            if (request == null) return NotFound();

            return View(request);
        }
        // ==========================================
// 7. BRAND MANAGEMENT (QUẢN LÝ THƯƠNG HIỆU)
// ==========================================

// [GET] Danh sách Brands
public async Task<IActionResult> Brands(string search, string status)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Auth");

    var query = _context.Brands.AsQueryable();

    if (!string.IsNullOrEmpty(search))
    {
        query = query.Where(b => b.BrandName.Contains(search));
    }

    if (!string.IsNullOrEmpty(status))
    {
        bool isActive = status == "active";
        query = query.Where(b => b.IsActive == isActive);
    }

    ViewBag.CurrentSearch = search;
    ViewBag.CurrentStatus = status;

    // Sắp xếp mới nhất lên đầu
    return View(await query.OrderByDescending(b => b.BrandId).ToListAsync());
}

// [GET] Tạo Brand
public IActionResult CreateBrand()
{
    if (!IsAdmin()) return RedirectToAction("Login", "Auth");
    return View();
}

// [POST] Lưu Brand mới
[HttpPost]
public async Task<IActionResult> CreateBrand(Brand model)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Auth");

    // Loại bỏ check validation cho Products (List)
    ModelState.Remove("Products");

    if (await _context.Brands.AnyAsync(b => b.BrandName == model.BrandName))
    {
        ModelState.AddModelError("BrandName", "Brand name already exists!");
    }

    if (ModelState.IsValid)
    {
        model.CreatedAt = DateTime.Now;
        _context.Brands.Add(model);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Brand created successfully!";
        return RedirectToAction(nameof(Brands));
    }
    return View(model);
}

// [GET] Sửa Brand
public async Task<IActionResult> EditBrand(int id)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Auth");
    var brand = await _context.Brands.FindAsync(id);
    if (brand == null) return NotFound();
    return View(brand);
}

// [POST] Lưu sửa Brand
[HttpPost]
public async Task<IActionResult> EditBrand(Brand model)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Auth");

    ModelState.Remove("Products");

    if (await _context.Brands.AnyAsync(b => b.BrandName == model.BrandName && b.BrandId != model.BrandId))
    {
        ModelState.AddModelError("BrandName", "Brand name already exists!");
    }

    if (ModelState.IsValid)
    {
        var existingBrand = await _context.Brands.FindAsync(model.BrandId);
        if (existingBrand != null)
        {
            existingBrand.BrandName = model.BrandName;
            existingBrand.Description = model.Description;
            existingBrand.IsActive = model.IsActive;
            // Không cập nhật CreatedAt
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Brand updated successfully!";
            return RedirectToAction(nameof(Brands));
        }
    }
    return View(model);
}

// [POST/GET] Xóa Brand
public async Task<IActionResult> DeleteBrand(int id)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Auth");

    var brand = await _context.Brands.FindAsync(id);
    if (brand != null)
    {
        // Kiểm tra ràng buộc: Nếu có sản phẩm thì chỉ ẩn (Soft Delete), không xóa cứng
        bool hasProducts = await _context.Products.AnyAsync(p => p.BrandId == id);
        
        if (hasProducts)
        {
            brand.IsActive = false; // Chuyển sang Inactive
            TempData["Success"] = "Brand contains products. It has been deactivated instead of deleted.";
        }
        else
        {
            _context.Brands.Remove(brand);
            TempData["Success"] = "Brand deleted successfully!";
        }
        await _context.SaveChangesAsync();
    }
    return RedirectToAction(nameof(Brands));
}
    }
}
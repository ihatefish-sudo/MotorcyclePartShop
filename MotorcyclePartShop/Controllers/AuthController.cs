using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using MotorcyclePartShop.Utilities;
using MotorcyclePartShop.Services;

namespace MotorcyclePartShop.Controllers
{
    public class AuthController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;
        private readonly IEmailSender _emailSender;

        public AuthController(MotorcyclePartShopDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // ============================
        // LOGIN (UNCHANGED LOGIC)
        // ============================
        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                var role = HttpContext.Session.GetString("Role");
                if (role == "Admin") return RedirectToAction("Index", "Admin");
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter both Email and Password!";
                return View();
            }

            string hashedPassword = SecurityHelper.HashPassword(password);

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || user.PasswordHash != hashedPassword)
            {
                ViewBag.Error = "Incorrect Email or Password!";
                return View();
            }

            if (user.IsActive == false)
            {
                ViewBag.Error = "Your account has been locked. Please contact Admin (Email: vodinhtri2204@gmail.com).";
                return View();
            }

            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("Email", user.Email);

            bool isAdmin = user.UserRoles.Any(ur => ur.Role.RoleName == "Admin");

            if (isAdmin)
            {
                HttpContext.Session.SetString("Role", "Admin");
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                HttpContext.Session.SetString("Role", "Customer");
                return RedirectToAction("Index", "Home");
            }
        }

        // ============================
        // REGISTER (UNCHANGED LOGIC)
        // ============================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model, string ConfirmPassword)
        {
            ModelState.Remove("UserId");
            ModelState.Remove("UserRoles");

            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.PasswordHash))
            {
                ViewBag.Error = "Please fill in all information!";
                return View(model);
            }

            string passwordPattern = @"^(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*]).{8,}$";
            if (!Regex.IsMatch(model.PasswordHash, passwordPattern))
            {
                ViewBag.Error = "Password is not strong enough! Requirements: at least 8 characters, 1 uppercase letter, 1 number, and 1 special character (!@#$%^&*).";
                return View(model);
            }

            if (model.PasswordHash != ConfirmPassword)
            {
                ViewBag.Error = "Confirmation password does not match!";
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ViewBag.Error = "This email is already in use!";
                return View(model);
            }

            try
            {
                model.PasswordHash = SecurityHelper.HashPassword(model.PasswordHash);
                model.CreatedAt = DateTime.Now;
                model.IsActive = true;

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
                if (customerRole != null)
                {
                    _context.UserRoles.Add(new UserRole { UserId = model.UserId, RoleId = customerRole.RoleId });
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred: " + ex.Message;
                return View(model);
            }
        }

        // ============================
        // LOGOUT (UNCHANGED LOGIC)
        // ============================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // [NEW] FORGOT PASSWORD FEATURE
        // ==========================================

        // 1. Enter Email Page
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                ViewBag.Error = "This email is not registered in the system!";
                return View();
            }

            // Check resend cooldown (1m 30s)
            // Get the latest token for this user
            var lastToken = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastToken != null)
            {
                var secondsPassed = (DateTime.Now - lastToken.CreatedAt).TotalSeconds;
                if (secondsPassed < 90) // Less than 90 seconds
                {
                    ViewBag.Error = $"Please wait {Math.Ceiling(90 - secondsPassed)} more seconds before resending the code.";
                    return View();
                }
            }

            // Send OTP
            await SendOtpToUser(user);

            // Redirect to Verify page, passing email via QueryString or TempData
            return RedirectToAction("VerifyOtp", new { email = email });
        }

        // 2. Verify OTP Page
        [HttpGet]
        public async Task<IActionResult> VerifyOtp(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");

            ViewBag.Email = email;

            // Calculate countdown for Resend button
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                var lastToken = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.UserId)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (lastToken != null)
                {
                    var secondsPassed = (DateTime.Now - lastToken.CreatedAt).TotalSeconds;
                    var remaining = 90 - secondsPassed;
                    ViewBag.Countdown = remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string email, string otp)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return RedirectToAction("ForgotPassword");

            // Find valid token: Match User, Match OTP, Not Used
            // Get the latest one to check
            var token = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.OtpCode == otp && !t.IsUsed)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (token == null)
            {
                ViewBag.Error = "Incorrect or used OTP code.";
            }
            else if (DateTime.Now > token.ExpiryTime)
            {
                ViewBag.Error = "OTP code has expired (valid for 1 minute). Please send a new code.";
            }
            else
            {
                // Success -> Allow Password Reset
                // Use TempData to safely store verified status
                TempData["ResetEmail"] = email;
                TempData["ResetOtp"] = otp; // Store OTP to mark as used after password reset
                return RedirectToAction("ResetPassword");
            }

            // If error, reload view with countdown
            ViewBag.Email = email;

            // Recalculate countdown
            var lastToken = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.UserId)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();
            if (lastToken != null)
            {
                var secondsPassed = (DateTime.Now - lastToken.CreatedAt).TotalSeconds;
                var remaining = 90 - secondsPassed;
                ViewBag.Countdown = remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
            }

            return View();
        }

        // AJAX API: Resend OTP
        [HttpPost]
        public async Task<IActionResult> ResendOtp(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return Json(new { success = false, message = "Email does not exist." });

            // Check cooldown
            var lastToken = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastToken != null)
            {
                var secondsPassed = (DateTime.Now - lastToken.CreatedAt).TotalSeconds;
                if (secondsPassed < 90)
                {
                    return Json(new { success = false, message = "Please wait for the countdown to finish." });
                }
            }

            await SendOtpToUser(user);
            return Json(new { success = true, message = "A new OTP code has been sent to your email!" });
        }

        // 3. Reset Password Page
        [HttpGet]
        public IActionResult ResetPassword()
        {
            // Check if verify step passed
            if (TempData.Peek("ResetEmail") == null) return RedirectToAction("ForgotPassword");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
        {
            var email = TempData.Peek("ResetEmail")?.ToString();
            var otp = TempData.Peek("ResetOtp")?.ToString();

            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Confirmation password does not match!";
                return View();
            }

            // Check password strength (same as Register)
            string passwordPattern = @"^(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*]).{8,}$";
            if (!Regex.IsMatch(newPassword, passwordPattern))
            {
                ViewBag.Error = "Password is not strong enough! Requirements: at least 8 characters, 1 uppercase letter, 1 number, and 1 special character (!@#$%^&*).";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                // Update password
                user.PasswordHash = SecurityHelper.HashPassword(newPassword);

                // Mark Token as used
                var token = await _context.PasswordResetTokens
                    .Where(t => t.UserId == user.UserId && t.OtpCode == otp)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (token != null)
                {
                    token.IsUsed = true;
                }

                await _context.SaveChangesAsync();

                // Clear TempData
                TempData.Clear();
                TempData["Success"] = "Password reset successfully! Please login with your new password.";
                return RedirectToAction("Login");
            }

            return View();
        }

        // Helper: Generate & Save OTP to DB + Send Email
        private async Task SendOtpToUser(User user)
        {
            string otp = new Random().Next(100000, 999999).ToString();

            // Save to DB
            var token = new PasswordResetToken
            {
                UserId = user.UserId,
                OtpCode = otp,
                ExpiryTime = DateTime.Now.AddMinutes(1), // Expires in 1 minute
                CreatedAt = DateTime.Now,
                IsUsed = false
            };

            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            // Send Email
            string subject = "Password Reset Verification Code - MotoShop";
            string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #d70018;'>Password Reset Request</h2>
                    <p>Hello <strong>{user.FullName}</strong>,</p>
                    <p>You have requested to reset your password for your MotoShop account.</p>
                    <p>Your verification code (OTP) is:</p>
                    <h1 style='color: #d70018; letter-spacing: 5px;'>{otp}</h1>
                    <p>This code is valid for <strong>1 minute</strong>.</p>
                    <p>If you did not request this, please ignore this email.</p>
                </div>";

            await _emailSender.SendEmailAsync(user.Email, subject, body);
        }
    }
}
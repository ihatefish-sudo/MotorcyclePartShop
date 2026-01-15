using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using MotorcyclePartShop.Utilities;
using System.Text.RegularExpressions;

namespace MotorcyclePartShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public AccountController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        // Helper to get current User
        private async Task<User?> GetCurrentUser()
        {
            var userIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdString)) return null;

            int userId = int.Parse(userIdString);
            return await _context.Users.FindAsync(userId);
        }

        // ==========================================
        // 1. PROFILE MANAGEMENT
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Auth");

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(User model)
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Auth");

            // Only update allowed fields
            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update UserName in Session if changed
            HttpContext.Session.SetString("UserName", user.FullName);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        // ==========================================
        // 2. CHANGE PASSWORD
        // ==========================================
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login", "Auth");

            // 1. Check old password
            string currentHash = SecurityHelper.HashPassword(currentPassword);
            if (user.PasswordHash != currentHash)
            {
                ViewBag.Error = "Incorrect current password!";
                return View();
            }

            // 2. Check new password strength (Server-side validation)
            // (Number requirement removed based on your previous request)
            string passwordPattern = @"^(?=.*[A-Z])(?=.*[!@#$%^&*]).{8,}$";
            if (!Regex.IsMatch(newPassword, passwordPattern))
            {
                ViewBag.Error = "New password is not strong enough (Requires 8 chars, 1 uppercase, 1 special char)!";
                return View();
            }

            // 3. Check confirm password
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Confirmation password does not match!";
                return View();
            }

            // 4. Save new password
            user.PasswordHash = SecurityHelper.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully!";
            return RedirectToAction("Profile");
        }
     
    }
}
using System.Security.Claims;

using System.Text;
using CRM.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CRM.Controllers
{
    public class AuthController : Controller
    {
        private readonly CallCenterContext _context;

        public AuthController(CallCenterContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
     
        public async Task<IActionResult> Login(loginVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == model.Username && u.PasswordHash == model.Password);

            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError("", "Invalid credentials or inactive user.");
                return View(model);
            }

            // Optional: store user ID in session
            HttpContext.Session.SetInt32("UserId", user.UserId);

            // Required: create claims
            var claims = new List<Claim>
    {
                 new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.RoleId.ToString())
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Log login activity
            user.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home");
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}

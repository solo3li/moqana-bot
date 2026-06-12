using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelegramAdminPanel.Data;
using TelegramAdminPanel.Models;

namespace TelegramAdminPanel.Controllers;

public class AuthController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PasswordHasher<AdminUser> _passwordHasher;

    public AuthController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _passwordHasher = new PasswordHasher<AdminUser>();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }
        
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var user = await _dbContext.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);

        if (user != null)
        {
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
        }

        ViewData["ErrorMessage"] = "Invalid username or password.";
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        // Only allow registration if no users exist
        bool hasUsers = await _dbContext.AdminUsers.AnyAsync();
        if (hasUsers)
        {
            ViewData["RegistrationClosed"] = true;
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string username, string password, string confirmPassword)
    {
        // Double check if registration is closed
        bool hasUsers = await _dbContext.AdminUsers.AnyAsync();
        if (hasUsers)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewData["ErrorMessage"] = "Username and password are required.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewData["ErrorMessage"] = "Passwords do not match.";
            return View();
        }

        var newUser = new AdminUser
        {
            Username = username
        };

        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, password);

        _dbContext.AdminUsers.Add(newUser);
        await _dbContext.SaveChangesAsync();

        // Automatically log them in after registration
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, newUser.Username),
            new Claim(ClaimTypes.NameIdentifier, newUser.Id.ToString())
        };
        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using VideoActive.Models;
using System.Text.Json;
using BCrypt.Net;

public class AdminController: Controller
{

    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Dashboard", "Home"); // ✅ Prevents infinite redirection loop
        }
        
        return View();
    }

    [HttpPost]
public async Task<IActionResult> Login(LoginViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    var admin = _context.Admins.SingleOrDefault(a => a.Username == model.Username);
    if (admin == null || !BCrypt.Net.BCrypt.Verify(model.Password, admin.PasswordHash))
    {
        ModelState.AddModelError("", "Invalid username or password");
        return View(model);
    }

    // 🔹 Check if the password is still default
    bool needsPasswordUpdate = admin.IsDefaultPassword; // A new column in DB

    // 🔹 Store username for the password update modal
    ViewData["NeedsPasswordUpdate"] = needsPasswordUpdate ? "true" : "false";
    ViewData["Username"] = model.Username;

    if (needsPasswordUpdate)
    {
        return View(model); // Show modal
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, admin.Username),
        new Claim(ClaimTypes.Role, "Admin")
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var authProperties = new AuthenticationProperties { IsPersistent = false };

    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                                  new ClaimsPrincipal(claimsIdentity),
                                  authProperties);

    return RedirectToAction("Dashboard", "Home");
}


    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(".AspNetCore.Cookies");
        return RedirectToAction("Login");
    }

    private bool VerifyPassword(string enteredPassword, string storedHash)
    {
        // print hash of entered password
        Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(enteredPassword));
        return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHash);
    }

    [HttpPost]
    public IActionResult UpdatePassword(string Username, string NewPassword, string ConfirmPassword)
    {
        if (NewPassword != ConfirmPassword)
        {
            TempData["Error"] = "Passwords do not match.";
            return RedirectToAction("Login");
        }

        var admin = _context.Admins.SingleOrDefault(a => a.Username == Username);
        if (admin == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Login");
        }

        // 🔹 Hash and update the new password
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
        admin.IsDefaultPassword = false; // 🔹 Mark as updated
        _context.SaveChanges();

        TempData["Success"] = "Password updated successfully! Please log in again.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        string username = User.Identity.Name;
        var admin = _context.Admins.SingleOrDefault(a => a.Username == username);

        if (admin == null)
        {
            TempData["Error"] = "Admin not found.";
            return RedirectToAction("ChangePassword");
        }

        // 🔹 Verify the current password
        if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, admin.PasswordHash))
        {
            TempData["Error"] = "Current password is incorrect.";
            return RedirectToAction("ChangePassword");
        }

        // 🔹 Hash and update the new password
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        admin.IsDefaultPassword = false;
        _context.SaveChanges();

        TempData["Success"] = "Password updated successfully!";
        // return RedirectToAction("Logout");
        // Stay at the same page
        return View();
    }

}
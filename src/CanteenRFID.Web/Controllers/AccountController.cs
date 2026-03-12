using System.Security.Claims;
using CanteenRFID.Web.Models;
using CanteenRFID.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenRFID.Web.Controllers;

public class AccountController : Controller
{
    private readonly AdminCredentialStore _credentialStore;

    public AccountController(AdminCredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        if (await _credentialStore.ValidateAsync(username, password))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),
                new(ClaimTypes.Role, AdminCredentialStore.AdminRole)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Redirect(returnUrl ?? "/");
        }

        ViewBag.Error = "Ungültige Anmeldedaten";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        return RedirectToAction("Login");
    }

    [Authorize(Roles = AdminCredentialStore.AdminRole)]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize(Roles = AdminCredentialStore.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var isValid = await _credentialStore.ValidateAsync(User.Identity?.Name ?? string.Empty, model.CurrentPassword);
        if (!isValid)
        {
            ModelState.AddModelError(string.Empty, "Aktuelles Passwort ist ungültig.");
            return View(model);
        }

        await _credentialStore.ChangePasswordAsync(model.NewPassword);
        TempData["Success"] = "Passwort wurde geändert.";
        return RedirectToAction("Index", "Home");
    }
}

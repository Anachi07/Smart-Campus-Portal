using Microsoft.AspNetCore.Mvc;
using SmartQueueSystemGUI.Models;
using SmartQueueSystemGUI.Services;

namespace SmartQueueSystemGUI.Controllers;

public class AuthController : Controller
{
    private readonly QueueStore _store;
    public AuthController(QueueStore store) => _store = store;

    private IActionResult RedirectByRole(string? role)
    {
        if (role == "Admin" || role == "Staff") return RedirectToAction("Dashboard", "Admin");
        if (role == "Customer") return RedirectToAction("GetTicket", "Queue");
        return RedirectToAction("Login", "Auth");
    }

    public IActionResult Setup()
    {
        TempData["SuccessMessage"] = "Admin account is already included in the system. Please login using the owner-provided credentials.";
        return RedirectToAction("Login");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Setup(RegisterViewModel model)
    {
        TempData["SuccessMessage"] = "Admin account is already included in the system. Please login using the owner-provided credentials.";
        return RedirectToAction("Login");
    }

    public IActionResult Login()
    {
        var role = HttpContext.Session.GetString("Role");
        if (!string.IsNullOrWhiteSpace(role)) return RedirectByRole(role);

        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        ViewBag.Settings = _store.Snapshot().Settings;
        ViewBag.HasAdministrator = _store.HasAdministrator();
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        ViewBag.Settings = _store.Snapshot().Settings;
        ViewBag.HasAdministrator = _store.HasAdministrator();

        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.ErrorMessage = "Please enter your username and password.";
            return View(model);
        }

        var user = _store.Login(model.Username, model.Password);
        if (user == null)
        {
            model.ErrorMessage = "Invalid username or password.";
            return View(model);
        }

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Role", user.Role);
        HttpContext.Session.SetString("PhoneNumber", user.PhoneNumber);

        return RedirectByRole(user.Role);
    }

    public IActionResult Register()
    {
        var role = HttpContext.Session.GetString("Role");
        if (!string.IsNullOrWhiteSpace(role)) return RedirectByRole(role);

        ViewBag.Settings = _store.Snapshot().Settings;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        ViewBag.Settings = _store.Snapshot().Settings;

        var result = _store.RegisterCustomer(model.FullName, model.PhoneNumber, model.Username, model.Password, model.ConfirmPassword);
        if (!result.ok)
        {
            model.ErrorMessage = result.message;
            return View(model);
        }

        var user = result.user!;
        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Role", user.Role);
        HttpContext.Session.SetString("PhoneNumber", user.PhoneNumber);

        TempData["SuccessMessage"] = "Account created successfully. You can now get a queue ticket.";
        return RedirectToAction("GetTicket", "Queue");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "You have been logged out.";
        return RedirectToAction("Login", "Auth");
    }
}

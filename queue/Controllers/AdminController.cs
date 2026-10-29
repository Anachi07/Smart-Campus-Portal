using Microsoft.AspNetCore.Mvc;
using SmartQueueSystemGUI.Models;
using SmartQueueSystemGUI.Services;

namespace SmartQueueSystemGUI.Controllers;

public class AdminController : Controller
{
    private readonly QueueStore _store;
    public AdminController(QueueStore store) => _store = store;

    private bool IsAllowed()
    {
        var role = HttpContext.Session.GetString("Role");
        return role == "Admin" || role == "Staff";
    }

    private bool IsAdmin()
    {
        return HttpContext.Session.GetString("Role") == "Admin";
    }

    private IActionResult RequireLogin()
    {
        if (!IsAllowed()) return Redirect("/sims/login.php");
        ViewBag.FullName = HttpContext.Session.GetString("FullName");
        ViewBag.Role = HttpContext.Session.GetString("Role");
        ViewBag.Settings = _store.Snapshot().Settings;
        return new EmptyResult();
    }

    private IActionResult RequireAdmin()
    {
        var gate = RequireLogin();
        if (gate is not EmptyResult) return gate;
        if (!IsAdmin()) return RedirectToAction("Dashboard");
        return new EmptyResult();
    }

    public IActionResult Dashboard()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        return View(_store.GetDashboard());
    }

    public IActionResult QueueControl()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        return View(_store.GetQueueControl());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CallNext(string counterName)
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        var ticket = _store.CallNext(counterName);
        TempData["SuccessMessage"] = ticket == null ? "No ticket was called. Check that the selected counter is available, has no active ticket, and that visitors are waiting." : $"{ticket.TicketNumber} is now serving at {ticket.CounterName}.";
        return RedirectToAction("QueueControl");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Complete(string ticketNumber)
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        TempData["SuccessMessage"] = _store.CompleteTicket(ticketNumber) ? "Ticket marked served." : "This ticket cannot be updated from its current status. Refresh the queue and try again.";
        return RedirectToAction("QueueControl");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Skip(string ticketNumber)
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        TempData["SuccessMessage"] = _store.SkipTicket(ticketNumber) ? "Ticket skipped." : "This ticket cannot be updated from its current status. Refresh the queue and try again.";
        return RedirectToAction("QueueControl");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Return(string ticketNumber)
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        TempData["SuccessMessage"] = _store.ReturnTicket(ticketNumber) ? "Ticket returned to the waiting queue." : "This ticket cannot be updated from its current status. Refresh the queue and try again.";
        return RedirectToAction("QueueControl");
    }

    public IActionResult Reports()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        return View(_store.GetReports());
    }

    [HttpGet]
    public IActionResult NotificationSummary()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return Unauthorized();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return Unauthorized();

        var latest = _store.GetLatestAccountNotification(userId.Value);
        return Json(new
        {
            unreadCount = _store.GetUnreadNotificationCount(userId.Value),
            latestId = latest?.Id ?? 0,
            latestTitle = latest?.Title ?? string.Empty,
            latestMessage = latest?.Message ?? string.Empty,
            latestCreatedAt = latest?.CreatedAt.ToString("MMM dd, yyyy hh:mm tt") ?? string.Empty
        });
    }

    public IActionResult Notifications()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        var data = _store.Snapshot();
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId.HasValue) _store.MarkAccountNotificationsRead(currentUserId.Value);
        return View(new NotificationCenterViewModel
        {
            PhoneNotifications = data.SmsNotifications.OrderByDescending(x => x.CreatedAt).ToList(),
            AccountNotifications = data.AccountNotifications.OrderByDescending(x => x.CreatedAt).ToList()
        });
    }

    public IActionResult Counters()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        return View(_store.Snapshot().Counters.OrderBy(x => x.Name).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddCounter(string name, string staffName)
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        TempData["SuccessMessage"] = _store.AddCounter(name, staffName) ? "Counter saved successfully." : "Use a unique counter name of up to 60 characters and a staff name of up to 80 characters.";
        return RedirectToAction("Counters");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateCounter(int id, string status)
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        TempData["SuccessMessage"] = _store.UpdateCounter(id, status) ? "Counter status updated." : "Cannot change this counter. Complete or skip its active ticket first; serving status is set when calling a ticket.";
        return RedirectToAction("Counters");
    }

    public IActionResult Users()
    {
        var gate = RequireAdmin(); if (gate is not EmptyResult) return gate;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        var users = _store.GetUsers();
        return View(new UserManagementViewModel
        {
            Users = users,
            AdminStaffUsers = users.Where(u => u.Role == "Admin" || u.Role == "Staff").ToList(),
            CustomerUsers = users.Where(u => u.Role == "Customer").ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddUser(UserManagementViewModel model)
    {
        var gate = RequireAdmin(); if (gate is not EmptyResult) return gate;
        if (model.Role != "Admin" && model.Role != "Staff")
        {
            model.ErrorMessage = "Public registration is for customers only. Admin can create only Admin or Staff accounts here.";
            var users = _store.GetUsers();
            model.Users = users;
            model.AdminStaffUsers = users.Where(u => u.Role == "Admin" || u.Role == "Staff").ToList();
            model.CustomerUsers = users.Where(u => u.Role == "Customer").ToList();
            ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
            return View("Users", model);
        }

        var result = _store.CreateUser(model.FullName, model.PhoneNumber, model.Username, model.Password, model.ConfirmPassword, model.Role);
        if (!result.ok)
        {
            model.ErrorMessage = result.message;
            var users = _store.GetUsers();
            model.Users = users;
            model.AdminStaffUsers = users.Where(u => u.Role == "Admin" || u.Role == "Staff").ToList();
            model.CustomerUsers = users.Where(u => u.Role == "Customer").ToList();
            ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
            return View("Users", model);
        }
        TempData["SuccessMessage"] = "Admin/Staff account created successfully.";
        return RedirectToAction("Users");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteUser(int id)
    {
        var gate = RequireAdmin(); if (gate is not EmptyResult) return gate;
        var currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;
        var result = _store.DeleteUser(id, currentUserId);
        TempData["SuccessMessage"] = result.message;
        return RedirectToAction("Users");
    }

    public IActionResult ActivityLogs()
    {
        var gate = RequireLogin(); if (gate is not EmptyResult) return gate;
        return View(_store.Snapshot().ActivityLogs.OrderByDescending(x => x.CreatedAt).Take(100).ToList());
    }

    public IActionResult Settings()
    {
        var gate = RequireAdmin(); if (gate is not EmptyResult) return gate;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;
        return View(_store.Snapshot().Settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Settings(AppSettings settings)
    {
        var gate = RequireAdmin(); if (gate is not EmptyResult) return gate;
        _store.SaveSettings(settings);
        TempData["SuccessMessage"] = "Settings saved successfully.";
        return RedirectToAction("Settings");
    }
}

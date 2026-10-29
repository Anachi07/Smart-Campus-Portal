using Microsoft.AspNetCore.Mvc;
using QRCoder;
using SmartQueueSystemGUI.Models;
using SmartQueueSystemGUI.Services;

namespace SmartQueueSystemGUI.Controllers;

public class QueueController : Controller
{
    private readonly QueueStore _store;
    public QueueController(QueueStore store) => _store = store;

    private IActionResult? RequireCustomer()
    {
        var role = HttpContext.Session.GetString("Role");
        if (role == "Customer") return null;
        if (role == "Admin" || role == "Staff") return RedirectToAction("Dashboard", "Admin");
        return RedirectToAction("Login", "Auth");
    }

    public IActionResult GetTicket()
    {
        var gate = RequireCustomer(); if (gate != null) return gate;
        ViewBag.Settings = _store.Snapshot().Settings;
        ViewBag.FullName = HttpContext.Session.GetString("FullName");
        ViewBag.Role = HttpContext.Session.GetString("Role");
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string;

        return View(new TicketRequestViewModel
        {
            CustomerName = HttpContext.Session.GetString("FullName") ?? string.Empty,
            PhoneNumber = HttpContext.Session.GetString("PhoneNumber") ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GetTicket(TicketRequestViewModel model)
    {
        var gate = RequireCustomer(); if (gate != null) return gate;
        ViewBag.Settings = _store.Snapshot().Settings;
        ViewBag.FullName = HttpContext.Session.GetString("FullName");
        ViewBag.Role = HttpContext.Session.GetString("Role");

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var customerName = HttpContext.Session.GetString("FullName") ?? model.CustomerName;
        var phoneNumber = model.PhoneNumber;
        var result = _store.CreateTicket(customerName, phoneNumber, model.ServiceType, model.PriorityType, currentUserId);
        if (!result.ok)
        {
            model.ErrorMessage = result.message;
            return View(model);
        }
        return RedirectToAction("Ticket", new { id = result.ticket!.TicketNumber });
    }

    public IActionResult Ticket(string id)
    {
        ViewBag.Settings = _store.Snapshot().Settings;
        var ticket = _store.FindTicket(id);
        if (ticket == null) return RedirectToAction("GetTicket");
        return View(ticket);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult TicketQr(string id)
    {
        var ticket = _store.FindTicket(id);
        if (ticket == null) return NotFound();

        var ticketUrl = Url.Action("MyTicket", "Queue", new { searchText = ticket.TicketNumber }, Request.Scheme) ?? ticket.TicketNumber;
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(ticketUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(qrData);
        var svg = qrCode.GetGraphic(7, "#0f172a", "#ffffff", true);
        return Content(svg, "image/svg+xml");
    }

    public IActionResult MyTicket(string? searchText)
    {
        ViewBag.Settings = _store.Snapshot().Settings;
        var model = new TicketSearchViewModel { SearchText = searchText ?? string.Empty, BestVisitTime = _store.GetBestVisitTimeRecommendation() };
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            model.Results = _store.SearchTickets(searchText);
            if (!model.Results.Any()) model.Message = "No ticket found. Check your ticket number or phone number.";
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult NotificationSummary()
    {
        var role = HttpContext.Session.GetString("Role");
        if (role != "Customer") return Unauthorized();

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
        ViewBag.Settings = _store.Snapshot().Settings;
        ViewBag.FullName = HttpContext.Session.GetString("FullName");
        ViewBag.Role = HttpContext.Session.GetString("Role");

        var gate = RequireCustomer(); if (gate != null) return gate;

        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return RedirectToAction("Login", "Auth");

        var notifications = _store.GetAccountNotifications(userId.Value);
        _store.MarkAccountNotificationsRead(userId.Value);
        return View(notifications);
    }

}

using Microsoft.AspNetCore.Mvc;
using SmartQueueSystemGUI.Services;

namespace SmartQueueSystemGUI.Controllers;

public class HomeController : Controller
{
    private readonly QueueStore _store;
    public HomeController(QueueStore store) => _store = store;

    public IActionResult Index()
    {
        return RedirectToAction("Login", "Auth");
    }

    public IActionResult Display()
    {
        ViewBag.Settings = _store.Snapshot().Settings;
        return View(_store.GetQueueControl());
    }

    // Public projection: never expose names, phone numbers, or request notes.
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult LiveQueue()
    {
        var queue = _store.GetQueueControl();
        object PublicTicket(SmartQueueSystemGUI.Models.Ticket t) => new {
            t.TicketNumber, t.Office, t.CounterName, t.PredictedCounterName,
            t.ServiceType, t.RequestType
        };
        return Json(new {
            serving = queue.NowServingTickets.OrderBy(t => t.CounterName).Select(PublicTicket),
            waiting = queue.WaitingTickets.Take(10).Select(PublicTicket)
        });
    }

    public IActionResult Error()
    {
        return View();
    }
}

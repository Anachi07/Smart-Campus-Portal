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

    public IActionResult Error()
    {
        return View();
    }
}

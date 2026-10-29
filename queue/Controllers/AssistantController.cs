using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using SmartQueueSystemGUI.Models;
using SmartQueueSystemGUI.Services;

namespace SmartQueueSystemGUI.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class AssistantController : Controller
{
    private readonly QueueStore _store;private readonly SchoolAssistantService _assistant;private readonly AssistantThrottle _throttle;private readonly IAntiforgery _csrf;
    public AssistantController(QueueStore store,SchoolAssistantService assistant,AssistantThrottle throttle,IAntiforgery csrf){_store=store;_assistant=assistant;_throttle=throttle;_csrf=csrf;}
    private int? UserId=>HttpContext.Session.GetInt32("UserId");
    private List<CampusClass> Classes {get{try{return (JsonSerializer.Deserialize<List<CampusClass>>(HttpContext.Session.GetString("CampusClasses")??"[]")??new()).Where(c=>Enum.TryParse<DayOfWeek>(c.Day,true,out _) && TimeSpan.TryParse(c.Start,out var from) && TimeSpan.TryParse(c.End,out var to) && from>=TimeSpan.Zero && to<=TimeSpan.FromDays(1) && from<to).ToList();}catch(JsonException){return new();}}}
    private bool Limited(string action,int limit) {
        var guest=HttpContext.Session.GetString("AssistantGuest");if(guest==null){guest=Guid.NewGuid().ToString("N");HttpContext.Session.SetString("AssistantGuest",guest);}
        return !_throttle.Allow(action+":"+(UserId?.ToString()??guest),limit,TimeSpan.FromMinutes(10));
    }
    private object BookingView(Ticket t)=>new {t.TicketNumber,t.ServiceType,t.RequestType,t.StudentNumber,t.Status,t.Office,t.ScheduledAt,t.CheckedInAt,t.EstimatedWaitText,t.CounterName,t.RequestNotes,canCheckIn=t.Status=="Scheduled"&&t.ScheduledAt.HasValue&&QueueStore.CampusNow>=t.ScheduledAt.Value.AddMinutes(-15)&&QueueStore.CampusNow<=t.ScheduledAt.Value.AddMinutes(30),canCancel=t.Status is "Scheduled" or "Waiting",ticketUrl=Url.Action("Ticket","Queue",new{id=t.TicketNumber})};
    [HttpGet]
    public IActionResult Context() {
        var settings=_store.Snapshot().Settings;
        return Json(new {token=_csrf.GetAndStoreTokens(HttpContext).RequestToken,authenticated=UserId.HasValue,aiConfigured=_assistant.AiConfigured,studentNumber=HttpContext.Session.GetString("StudentNumber")??"",phoneNumber=HttpContext.Session.GetString("PhoneNumber")??"",hasTimetable=Classes.Count>0,services=CampusServices.RequestTypes,timezone="Asia/Manila",now=QueueStore.CampusNow,settings=new {settings.EnableAppointments,settings.AppointmentSlotMinutes,settings.MaxBookingsPerSlot,settings.AdvanceBookingDays,settings.BookingDays,settings.BusinessStartHour,settings.BusinessEndHour}});
    }
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat([FromBody] ChatRequest? chat,CancellationToken token) {
        if(chat==null || string.IsNullOrWhiteSpace(chat.Message)||chat.Message.Length>1200 || chat.History==null || chat.History.Count>6 || chat.History.Any(h=>h==null||h.Content==null||h.Content.Length>1200||h.Role is not ("user" or "assistant")))return BadRequest(new{message="Ask a question of up to 1200 characters."});
        if(Limited("chat",20))return StatusCode(429,new{message="Please wait a few minutes before sending more questions."});
        try { var facts=await _assistant.Knowledge(token);return Json(await _assistant.Answer(chat,facts,token)); }
        catch(Exception e) when(e is HttpRequestException or JsonException or TaskCanceledException){return StatusCode(503,new{message="School information is temporarily unavailable. You can still review queue visits from My transactions."});}
    }
    [HttpGet]
    public IActionResult Slots(string serviceType,string? date) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to view and reserve visit times."});
        if(Limited("slots",100))return StatusCode(429,new{message="Please wait before refreshing the schedule."});
        if(!CampusServices.RequestTypes.ContainsKey(serviceType??""))return BadRequest(new{message="Choose a campus service."});
        DateOnly? selected=null;
        if(!string.IsNullOrEmpty(date)){if(!DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))return BadRequest(new{message="Choose a valid date."});selected=parsed;}
        var slots=_store.GetBookingSlots(serviceType!,selected,UserId.Value,Classes);
        return Json(new{slots,recommended=slots.Where(s=>s.Recommended).ToList(),timezone="Asia/Manila",notice="Times follow the portal's booking settings and current reservations. Confirm school office hours and your availability; service may not start exactly at the reserved time."});
    }
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Book([FromBody] AppointmentRequest? request) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in before confirming a visit."});
        if(request==null||!ModelState.IsValid)return BadRequest(new{message="Complete the booking details."});
        if(Limited("book",15))return StatusCode(429,new{message="Please wait before submitting another booking."});
        var result=_store.BookVisit(request,UserId.Value,HttpContext.Session.GetString("FullName")??"Campus user",Classes);
        return result.ok?Json(new{message=result.message,booking=BookingView(result.ticket!)}):Conflict(new{message=result.message});
    }
    [HttpGet]
    public IActionResult Bookings(){if(!UserId.HasValue)return Unauthorized(new{message="Sign in to view your requests."});return Json(new{bookings=_store.Snapshot().Tickets.Where(t=>t.CustomerUserId==UserId.Value&&t.Status is "Scheduled" or "Waiting" or "Now Serving").OrderBy(t=>t.ScheduledAt??new DateTimeOffset(t.CreatedAt)).Take(20).Select(BookingView)});}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult CheckIn([FromBody] TicketAction? action)=>Act(action,true);
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Cancel([FromBody] TicketAction? action)=>Act(action,false);
    private IActionResult Act(TicketAction? action,bool checkIn){if(Limited("action",30))return StatusCode(429,new{message="Please wait before updating more requests."});if(!UserId.HasValue)return Unauthorized(new{message="Sign in to update a request."});if(action==null||string.IsNullOrWhiteSpace(action.TicketNumber)||action.TicketNumber.Length>80)return BadRequest(new{message="Choose a valid request."});var result=checkIn?_store.CheckInVisit(action.TicketNumber,UserId.Value):_store.CancelVisit(action.TicketNumber,UserId.Value);return result.ok?Json(new{message=result.message}):Conflict(new{message=result.message});}
}

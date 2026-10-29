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
    private readonly QueueStore _store;private readonly SchoolAssistantService _assistant;private readonly AssistantThrottle _throttle;private readonly IAntiforgery _csrf;private readonly VisitEligibilityService _eligibility;
    public AssistantController(QueueStore store,SchoolAssistantService assistant,AssistantThrottle throttle,IAntiforgery csrf,VisitEligibilityService eligibility){_eligibility=eligibility;_store=store;_assistant=assistant;_throttle=throttle;_csrf=csrf;}
    private int? UserId=>HttpContext.Session.GetInt32("UserId");
    private List<CampusClass> Classes {get{try{return (JsonSerializer.Deserialize<List<CampusClass>>(HttpContext.Session.GetString("CampusClasses")??"[]")??new()).Where(c=>Enum.TryParse<DayOfWeek>(c.Day,true,out _) && TimeSpan.TryParse(c.Start,out var from) && TimeSpan.TryParse(c.End,out var to) && from>=TimeSpan.Zero && to<=TimeSpan.FromDays(1) && from<to).ToList();}catch(JsonException){return new();}}}
    private bool Limited(string action,int limit) {
        var guest=HttpContext.Session.GetString("AssistantGuest");if(guest==null){guest=Guid.NewGuid().ToString("N");HttpContext.Session.SetString("AssistantGuest",guest);}
        return !_throttle.Allow(action+":"+(UserId?.ToString()??guest),limit,TimeSpan.FromMinutes(10));
    }
    private object BookingView(Ticket t)=>new {t.TicketNumber,t.ServiceType,t.RequestType,t.StudentNumber,t.Status,t.Office,t.ScheduledAt,t.CheckedInAt,t.EstimatedWaitText,t.CounterName,t.RequestNotes,t.DocumentStatus,canCheckIn=t.Status=="Scheduled"&&t.ScheduledAt.HasValue&&QueueStore.CampusNow>=t.ScheduledAt.Value.AddMinutes(-15)&&QueueStore.CampusNow<=t.ScheduledAt.Value.AddMinutes(30),canCancel=t.Status is "Scheduled" or "Waiting",canChange=_store.CanChangeVisit(t),ticketUrl=Url.Action("Ticket","Queue",new{id=t.TicketNumber})};
    [HttpGet]
    public IActionResult Context() {
        var settings=_store.Snapshot().Settings;
        return Json(new {token=_csrf.GetAndStoreTokens(HttpContext).RequestToken,authenticated=UserId.HasValue,aiConfigured=_assistant.AiConfigured,studentNumber=HttpContext.Session.GetString("StudentNumber")??"",phoneNumber=HttpContext.Session.GetString("PhoneNumber")??"",hasTimetable=Classes.Count>0,pendingExchanges=UserId.HasValue?_store.GetExchanges(UserId.Value).Count(e=>e.CanRespond):0,services=CampusServices.RequestTypes,timezone="Asia/Manila",now=QueueStore.CampusNow,settings=new {settings.EnableAppointments,settings.AppointmentSlotMinutes,settings.MaxBookingsPerSlot,settings.AdvanceBookingDays,settings.BookingDays,settings.BusinessStartHour,settings.BusinessEndHour}});
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
    [HttpGet]
    public IActionResult ChangeSlots(string ticketNumber) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to change your visit."});
        if(Limited("slots",100))return StatusCode(429,new{message="Please wait before refreshing times."});
        return Json(new{slots=_store.GetChangeSlots(ticketNumber??"",UserId.Value,Classes),notice="Choose a new time, review it, then confirm. Your original reservation stays in place until confirmation."});
    }
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Reschedule([FromBody] ChangeVisitRequest? request) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to change a visit."});
        if(!ValidChange(request))return BadRequest(new{message="Choose a valid reservation and time."});
        if(Limited("change",15))return StatusCode(429,new{message="Please wait before making more changes."});
        var result=_store.RescheduleVisit(request!,UserId.Value,Classes);
        return result.ok?Json(new{message=result.message,booking=BookingView(result.ticket!)}):Conflict(new{message=result.message});
    }
    [HttpGet]
    public IActionResult ExchangeTimes(string ticketNumber) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to review exchange times."});
        if(Limited("slots",100))return StatusCode(429,new{message="Please wait before refreshing times."});
        return Json(new{times=_store.GetExchangeTimes(ticketNumber??"",UserId.Value,Classes),notice="These times have reservations that may be eligible for a voluntary exchange. No names or contact details are shared. Availability is checked again before invitations and acceptance."});
    }
    [HttpGet]
    public IActionResult Exchanges() {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to view your exchange invitations."});
        return Json(new{exchanges=_store.GetExchanges(UserId.Value)});
    }
    private static bool ValidChange(ChangeVisitRequest? request)=>request!=null&&!string.IsNullOrWhiteSpace(request.TicketNumber)&&request.TicketNumber.Length<=80&&request.StartsAt?.Length<=60&&request.ExpectedStartsAt?.Length<=60;
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestExchange([FromBody] ChangeVisitRequest? request,CancellationToken token) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to request an exchange."});
        if(!ValidChange(request))return BadRequest(new{message="Choose a valid reservation and time."});
        if(!request!.Confirmed)return Conflict(new{message="Review and confirm the exchange offer first."});
        if(Limited("exchange",10))return StatusCode(429,new{message="Please wait before sending more invitations."});
        var ids=_store.ExchangeParticipantIds(request,UserId.Value);
        if(ids.Count<2)return Conflict(new{message="No eligible reservation was found. Refresh your visit times."});
        try {
            var eligibility=await _eligibility.Fetch(ids,token);
            var result=_store.RequestExchange(request,UserId.Value,eligibility);
            return result.ok?Json(new{message=result.message}):Conflict(new{message=result.message});
        } catch(Exception e) when(e is HttpRequestException or JsonException or TaskCanceledException) {return StatusCode(503,new{message="Unable to verify class schedules and active accounts. No invitations were sent. Try again later."});}
    }
    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondExchange([FromBody] SwapAction? action,CancellationToken token) {
        if(!UserId.HasValue)return Unauthorized(new{message="Sign in to respond to an exchange."});
        if(action==null||!Guid.TryParseExact(action.Id,"N",out _)||action.Decision is not ("accept" or "decline" or "withdraw"))return BadRequest(new{message="Choose a valid exchange decision."});
        if(!action.Confirmed)return Conflict(new{message="Confirm your decision first."});
        if(Limited("exchange-action",30))return StatusCode(429,new{message="Please wait before updating more invitations."});
        var eligibility=new Dictionary<int,VisitEligibility>();
        if(action.Decision=="accept") {
            var ids=_store.ExchangeResponseParticipants(action.Id,UserId.Value);
            if(ids.Count<2)return Conflict(new{message="This invitation is no longer available for your account."});
            try {eligibility=await _eligibility.Fetch(ids,token);}
            catch(Exception e) when(e is HttpRequestException or JsonException or TaskCanceledException) {return StatusCode(503,new{message="Unable to verify both class schedules and active accounts. No visits were changed."});}
        }
        var result=_store.RespondExchange(action,UserId.Value,eligibility);
        return result.ok?Json(new{message=result.message}):Conflict(new{message=result.message});
    }
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult CheckIn([FromBody] TicketAction? action)=>Act(action,true);
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Cancel([FromBody] TicketAction? action)=>Act(action,false);
    private IActionResult Act(TicketAction? action,bool checkIn){if(Limited("action",30))return StatusCode(429,new{message="Please wait before updating more requests."});if(!UserId.HasValue)return Unauthorized(new{message="Sign in to update a request."});if(action==null||string.IsNullOrWhiteSpace(action.TicketNumber)||action.TicketNumber.Length>80)return BadRequest(new{message="Choose a valid request."});var result=checkIn?_store.CheckInVisit(action.TicketNumber,UserId.Value):_store.CancelVisit(action.TicketNumber,UserId.Value);return result.ok?Json(new{message=result.message}):Conflict(new{message=result.message});}
}

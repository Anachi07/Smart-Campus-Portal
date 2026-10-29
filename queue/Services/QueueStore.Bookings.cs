using System.Globalization;
using SmartQueueSystemGUI.Models;

namespace SmartQueueSystemGUI.Services;

public partial class QueueStore
{
    public static readonly TimeZoneInfo CampusZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
    public static DateTimeOffset CampusNow => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CampusZone);

    private bool ConflictsWithClass(DateTimeOffset at, string service, IEnumerable<CampusClass> classes)
    {
        var start=at.TimeOfDay-TimeSpan.FromMinutes(15);
        var end=at.TimeOfDay+TimeSpan.FromMinutes(EstimateServiceMinutes(service,"Regular")+15);
        return classes.Any(c => Enum.TryParse<DayOfWeek>(c.Day,true,out var day) && day==at.DayOfWeek &&
            TimeSpan.TryParse(c.Start,CultureInfo.InvariantCulture,out var from) && TimeSpan.TryParse(c.End,CultureInfo.InvariantCulture,out var to) && start<to && end>from);
    }

    private List<BookingSlot> SlotsLocked(string service, DateOnly? date, int userId, List<CampusClass> classes)
    {
        if(!_data.Settings.EnableAppointments || !CampusServices.RequestTypes.ContainsKey(service)) return new();
        if(!_data.Counters.Any(c => c.Office==CampusServices.OfficeFor(service) || c.Office=="All Services")) return new();
        var now=CampusNow;var today=DateOnly.FromDateTime(now.DateTime);var last=today.AddDays(Math.Clamp(_data.Settings.AdvanceBookingDays,1,30));
        if(date.HasValue && (date.Value<today || date.Value>last)) return new();
        var step=new[]{15,30,60}.Contains(_data.Settings.AppointmentSlotMinutes)?_data.Settings.AppointmentSlotMinutes:30;
        var capacity=Math.Clamp(_data.Settings.MaxBookingsPerSlot,1,10);
        var result=new List<BookingSlot>();
        for(var day=date??today;day<=(date??last);day=day.AddDays(1)) {
            if(!(_data.Settings.BookingDays??new()).Contains((int)day.DayOfWeek)) continue;
            for(var minute=_data.Settings.BusinessStartHour*60;minute+step<=_data.Settings.BusinessEndHour*60;minute+=step) {
                var local=day.ToDateTime(TimeOnly.MinValue).AddMinutes(minute);
                var at=new DateTimeOffset(local,CampusZone.GetUtcOffset(local));
                if(at<now.AddHours(1) || ConflictsWithClass(at,service,classes)) continue;
                var active=_data.Tickets.Where(t => t.ScheduledAt.HasValue && (t.Status is "Scheduled" or "Waiting" or "Now Serving")).ToList();
                if(active.Any(t => t.CustomerUserId==userId && Math.Abs((t.ScheduledAt!.Value-at).TotalMinutes)<step)) continue;
                var occupied=active.Count(t => t.Office==CampusServices.OfficeFor(service) && t.ScheduledAt==at);
                if(occupied>=capacity) continue;
                result.Add(new BookingSlot(at,CampusServices.OfficeFor(service),classes.Count>0?"Fits your saved class schedule and current booking availability.":"Based on current booking availability. Check your personal commitments.",capacity-occupied,false));
            }
        }
        var recommended=result.OrderByDescending(s=>s.RemainingCapacity).ThenBy(s=>s.StartsAt).Take(3).Select(s=>s.StartsAt).ToHashSet();
        return result.Select(s=>s with {Recommended=recommended.Contains(s.StartsAt)}).OrderBy(s=>s.StartsAt).ToList();
    }

    public List<BookingSlot> GetBookingSlots(string service,DateOnly? date,int userId,List<CampusClass> classes)
    {
        lock(_lock) {if(ExpireOldTickets(_data))Save();return SlotsLocked(service,date,userId,classes);}
    }

    public (bool ok,string message,Ticket? ticket) BookVisit(AppointmentRequest request,int userId,string fullName,List<CampusClass> classes)
    {
        lock(_lock) {
            request.StudentNumber ??= ""; request.ServiceType ??= ""; request.RequestType ??= "";request.IdempotencyKey ??= "";
            if(!request.Confirmed) return(false,"Review the date, time, and transaction, then confirm your booking.",null);
            if(!Guid.TryParse(request.IdempotencyKey,out _)) return(false,"Start a new booking confirmation.",null);
            if(!DateTimeOffset.TryParse(request.StartsAt,CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)) return(false,"Choose an available date and time.",null);
            var at=TimeZoneInfo.ConvertTime(parsed,CampusZone);
            var same=_data.Tickets.FirstOrDefault(t=>t.CustomerUserId==userId && t.BookingKey==request.IdempotencyKey);
            if(same!=null) return same.ScheduledAt==at && same.ServiceType==request.ServiceType && same.RequestType==request.RequestType && same.StudentNumber==request.StudentNumber.Trim() ? (true,"This booking was already confirmed.",same) : (false,"This confirmation was already used. Start a new booking.",null);
            if(ExpireOldTickets(_data))Save();
            if(_data.Tickets.Count(t=>t.CustomerUserId==userId && t.Status=="Scheduled")>=3) return(false,"You can have up to three upcoming visits. Cancel an existing visit before adding another.",null);
            var duplicate=_data.Tickets.FirstOrDefault(t=>t.CustomerUserId==userId && t.ServiceType==request.ServiceType && t.RequestType==request.RequestType && t.StudentNumber==request.StudentNumber.Trim() && t.Status is "Scheduled" or "Waiting" or "Now Serving");
            if(duplicate!=null) return(false,"You already have an active request for this transaction. Open My transactions to review or cancel it.",null);
            if(!SlotsLocked(request.ServiceType,DateOnly.FromDateTime(at.DateTime),userId,classes).Any(s=>s.StartsAt==at)) return(false,"That time is no longer available or conflicts with your saved schedule. Please choose another slot.",null);
            return CreateTicket(fullName,request.PhoneNumber,request.ServiceType,"Regular",userId,request.RequestType,request.StudentNumber,request.RequestNotes,at,request.IdempotencyKey);
        }
    }

    public (bool ok,string message) CheckInVisit(string number,int userId)
    {
        lock(_lock) {
            if(ExpireOldTickets(_data))Save();
            var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number && t.CustomerUserId==userId);
            if(ticket==null) return(false,"Booking not found.");
            if(ticket.Status=="Waiting" && ticket.CheckedInAt.HasValue) return(true,"You are already checked in.");
            var now=CampusNow;
            if(ticket.Status!="Scheduled" || !ticket.ScheduledAt.HasValue || now<ticket.ScheduledAt.Value.AddMinutes(-15) || now>ticket.ScheduledAt.Value.AddMinutes(30)) return(false,"Check in from 15 minutes before until 30 minutes after your reserved time.");
            ticket.Status="Waiting";ticket.CheckedInAt=now.DateTime;RefreshQueuePredictionsLocked();
            AddLog("Appointment check-in",$"{ticket.TicketNumber} joined the {ticket.Office} queue.");Save();return(true,"You are checked in. Watch your ticket for the counter call.");
        }
    }

    public (bool ok,string message) CancelVisit(string number,int userId)
    {
        lock(_lock) {
            var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number && t.CustomerUserId==userId);
            if(ticket==null)return(false,"Booking not found.");
            if(ticket.Status=="Cancelled")return(true,"This request is already cancelled.");
            if(ticket.Status is not ("Scheduled" or "Waiting"))return(false,"This request cannot be cancelled now. Please contact the office.");
            ticket.Status="Cancelled";ticket.EstimatedWaitText="Cancelled";RefreshQueuePredictionsLocked();
            AddLog("Request cancelled",$"{ticket.TicketNumber} was cancelled by its owner.");Save();return(true,"Your request was cancelled and its reservation was released.");
        }
    }
}

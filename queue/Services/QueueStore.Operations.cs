using System.Globalization;
using System.Security.Cryptography;
using SmartQueueSystemGUI.Models;
namespace SmartQueueSystemGUI.Services;

public partial class QueueStore
{
    public static readonly string[] DocumentStages={"For review","Processing","Ready for pickup","Released"};
    public static bool ChecklistClosed(Ticket ticket)=>ticket.DocumentStatus!=null?ticket.DocumentStatus=="Released"||ticket.DocumentStatus=="For review"&&(ticket.Status is "Cancelled" or "Expired"):ticket.Status is "Cancelled" or "Expired" or "Served";
    public static readonly string[] Offices={"Registrar","Cashier","All Services"};
    private static bool ValidTransaction(string service,string type)=>CampusServices.RequestTypes.TryGetValue(service??"",out var types)&&types.Contains(type);
    private static bool Clock(string value,out TimeSpan time)=>TimeSpan.TryParseExact(value,@"hh\:mm",CultureInfo.InvariantCulture,out time)&&time>=TimeSpan.Zero&&time<TimeSpan.FromDays(1);
    private void InitializeRequest(Ticket ticket) {
        if(ticket.ServiceType=="Document Request"&&ticket.DocumentStatus==null) {
            ticket.DocumentStatus="For review";
            ticket.DocumentHistory.Add(new(){Status="For review",Note="Request received. The Registrar will review requirements and eligibility.",At=CampusNow});
        }
        ticket.Checklist=_data.ServiceRequirements.Where(r=>r.Active&&r.ServiceType==ticket.ServiceType&&r.RequestType==ticket.RequestType).Select(r=>new RequestChecklistItem{RequirementId=r.Id,Title=r.Title,Instructions=r.Instructions??"",Required=r.Required}).ToList();
    }
    private static bool InitializeLegacyOperations(SmartQueueData data) {
        var changed=false;
        foreach(var ticket in data.Tickets) {
            ticket.Checklist??=new();ticket.DocumentHistory??=new();
            if(ticket.ServiceType=="Document Request"&&ticket.DocumentStatus==null) {
                ticket.DocumentStatus="For review";ticket.DocumentHistory.Add(new(){Status="For review",At=CampusNow,Note="Existing request imported for Registrar review. Queue completion does not confirm document release."});changed=true;
            }
        }
        return changed;
    }
    private static void ValidateOperationsBackup(SmartQueueData data) {
        if(data.ServiceRequirements==null||data.OfficeClosures==null||data.Feedback==null)throw new InvalidDataException("Invalid campus services data.");
        if(data.ServiceRequirements.Any(r=>r==null||!ValidTransaction(r.ServiceType,r.RequestType)||string.IsNullOrWhiteSpace(r.Title)||r.Title.Length>120||(r.Instructions?.Length??0)>600)||data.ServiceRequirements.Select(r=>r.Id).Distinct().Count()!=data.ServiceRequirements.Count)throw new InvalidDataException("Invalid requirement definitions.");
        if(data.OfficeClosures.Any(c=>c==null||!Offices.Contains(c.Office)||string.IsNullOrWhiteSpace(c.Title)||c.Title.Length>120||c.Weekday.HasValue&&(c.Weekday<0||c.Weekday>6)||!c.AllDay&&(!Clock(c.StartTime,out var start)||!Clock(c.EndTime,out var end)||start>=end)))throw new InvalidDataException("Invalid office calendar.");
        if(data.Feedback.Any(f=>f==null||f.Rating<1||f.Rating>5||(f.Comment?.Length??0)>500||!data.Tickets.Any(t=>t.TicketNumber==f.TicketNumber&&t.CustomerUserId==f.UserId))||data.Feedback.Select(f=>f.TicketNumber).Distinct().Count()!=data.Feedback.Count)throw new InvalidDataException("Invalid transaction feedback.");
        if(data.Tickets.Any(t=>t.Checklist==null||t.DocumentHistory==null||t.DocumentStatus!=null&&!DocumentStages.Contains(t.DocumentStatus)||t.Checklist.Any(c=>c==null)||t.DocumentStatus is "Ready for pickup" or "Released"&&string.IsNullOrWhiteSpace(t.ClaimCode)))throw new InvalidDataException("Invalid document request data.");
    }
    public List<ServiceRequirement> Requirements(string service,string type) {lock(_lock)return _data.ServiceRequirements.Where(r=>r.Active&&r.ServiceType==service&&r.RequestType==type).Select(r=>new ServiceRequirement{Id=r.Id,ServiceType=r.ServiceType,RequestType=r.RequestType,Title=r.Title,Instructions=r.Instructions??"",Required=r.Required,Active=r.Active}).ToList();}
    public (bool ok,string message) SaveRequirement(ServiceRequirement input,int staffId) {
        lock(_lock) {
            if(!ValidTransaction(input.ServiceType,input.RequestType)||string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>120||(input.Instructions?.Length??0)>600)return(false,"Choose a valid transaction and a requirement title of up to 120 characters; instructions may have up to 600.");
            var existing=_data.ServiceRequirements.FirstOrDefault(r=>r.Id==input.Id);
            if(input.Id!=0&&existing==null)return(false,"Requirement not found.");
            if(existing!=null&&(existing.ServiceType!=input.ServiceType||existing.RequestType!=input.RequestType))return(false,"Archive the old requirement and create a new one to change its transaction.");
            if(existing==null) {existing=new(){Id=NextId(_data.ServiceRequirements.Select(r=>r.Id)),ServiceType=input.ServiceType,RequestType=input.RequestType};_data.ServiceRequirements.Add(existing);}
            existing.Title=input.Title.Trim();existing.Instructions=(input.Instructions??"").Trim();existing.Required=input.Required;existing.Active=input.Active;
            AddLog("Service requirement updated",$"Staff #{staffId} updated requirement #{existing.Id}. Existing request checklists retain their original snapshot.");Save();return(true,"Requirement saved. It applies to new requests; existing request checklists are preserved.");
        }
    }
    public (bool ok,string message) SetPrepared(string number,int userId,List<int> prepared) {
        lock(_lock) {
            var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number&&t.CustomerUserId==userId);
            if(ticket==null)return(false,"Request not found.");
            if(ChecklistClosed(ticket))return(false,"This checklist is closed.");
            if(prepared.Any(id=>!ticket.Checklist.Any(c=>c.RequirementId==id)))return(false,"Refresh the checklist and select valid requirements.");
            foreach(var item in ticket.Checklist)item.Prepared=prepared.Contains(item.RequirementId);
            Save();return(true,"Your preparation checklist was saved. The office still verifies each requirement.");
        }
    }
    public (bool ok,string message) VerifyChecklist(string number,List<int> verified,int staffId) {
        lock(_lock) {
            var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number);
            if(ticket==null)return(false,"Request not found.");
            if(ChecklistClosed(ticket)||ticket.DocumentStatus=="Ready for pickup")return(false,"Verification is closed for this request.");
            if(verified.Any(id=>!ticket.Checklist.Any(c=>c.RequirementId==id)))return(false,"Select valid checklist items.");
            foreach(var item in ticket.Checklist)item.Verified=verified.Contains(item.RequirementId);
            AddLog("Requirements verified",$"Staff #{staffId} reviewed checklist for {ticket.TicketNumber}.");Save();return(true,"Office verification saved.");
        }
    }
    public (bool ok,string message) AdvanceDocument(string number,string stage,string? note,int staffId) {
        lock(_lock) {
            var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number&&t.ServiceType=="Document Request");
            if(ticket==null)return(false,"Document request not found.");
            if(ticket.Status=="Scheduled"||ticket.DocumentStatus=="For review"&&(ticket.Status is "Cancelled" or "Expired" or "Skipped"))return(false,"Review an active, checked-in request. Resolve the queue state first.");
            if((note?.Length??0)>600)return(false,"Keep the public progress note within 600 characters.");
            var current=Array.IndexOf(DocumentStages,ticket.DocumentStatus??"For review");
            if(current<0||current==DocumentStages.Length-1||DocumentStages[current+1]!=stage)return(false,"Follow the document stages in order. Released documents cannot be reopened here.");
            if(stage=="Ready for pickup"&&ticket.Checklist.Any(c=>c.Required&&!c.Verified))return(false,"Verify every required checklist item before marking the document ready.");
            if(stage=="Released"&&string.IsNullOrWhiteSpace(note))return(false,"Enter a release confirmation note after checking the recipient's identity in person.");
            if(stage=="Ready for pickup"&&string.IsNullOrEmpty(ticket.ClaimCode))ticket.ClaimCode="ICAS-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            if(stage=="Released"&&ticket.Status is "Waiting" or "Now Serving") {
                if(ticket.Status=="Now Serving") {var counter=_data.Counters.FirstOrDefault(c=>c.Name==ticket.CounterName);if(counter!=null)counter.Status="Available";}
                ticket.Status="Served";ticket.CompletedAt=CampusNow.DateTime;ticket.EstimatedWaitText="Served";
            }
            ticket.DocumentStatus=stage;ticket.DocumentHistory.Add(new(){Status=stage,Note=(note??"").Trim(),At=CampusNow,StaffId=staffId});
            if(ticket.CustomerUserId.HasValue)AddAccountNotification(ticket.CustomerUserId.Value,ticket.TicketNumber,"Document: "+stage,stage=="Ready for pickup"?"Your document is ready for pickup. Open your request for the claim stub and the Registrar's instructions.":$"Your document request is now {stage.ToLowerInvariant()}. Open the request to review its progress.");
            AddLog("Document progress",$"Staff #{staffId}: {ticket.TicketNumber} → {stage}.");Save();return(true,"Document progress updated. The requester has an in-app notification.");
        }
    }
    public bool CanSubmitFeedback(Ticket ticket)=>ticket.ServiceType=="Document Request"?ticket.DocumentStatus=="Released":ticket.Status=="Served";
    public (bool ok,string message) SubmitFeedback(string number,int userId,int rating,string? comment) {
        lock(_lock) {
            var ticket=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==number&&t.CustomerUserId==userId);
            if(ticket==null)return(false,"Request not found.");
            if(!CanSubmitFeedback(ticket))return(false,"Feedback opens after the transaction is completed; documents must be released first.");
            if(rating<1||rating>5||(comment?.Length??0)>500)return(false,"Choose a rating from 1 to 5 and keep your comment within 500 characters.");
            if(_data.Feedback.Any(f=>f.TicketNumber==number))return(false,"Feedback was already submitted for this transaction.");
            _data.Feedback.Add(new(){TicketNumber=number,UserId=userId,Rating=rating,Comment=(comment??"").Trim(),SubmittedAt=CampusNow});Save();return(true,"Thank you. Your feedback was sent privately to the school office.");
        }
    }
    private bool CalendarBlocks(string service,DateTimeOffset startsAt,int minutes)=>_data.OfficeClosures.Any(c=>ClosureBlocks(c,service,startsAt,minutes));
    private static bool ClosureBlocks(OfficeClosure c,string service,DateTimeOffset startsAt,int minutes) {
        var local=TimeZoneInfo.ConvertTime(startsAt,CampusZone);var date=DateOnly.FromDateTime(local.DateTime);var start=local.TimeOfDay;var end=start+TimeSpan.FromMinutes(minutes);
        return (c.Office=="All Services"||c.Office==CampusServices.OfficeFor(service))&&(c.Date.HasValue?c.Date==date:!c.Weekday.HasValue||c.Weekday==(int)local.DayOfWeek)&&(c.AllDay||Clock(c.StartTime,out var from)&&Clock(c.EndTime,out var to)&&start<to&&end>from);
    }
    public bool CanChangeVisit(Ticket ticket){lock(_lock)return ticket.Status=="Scheduled"&&ticket.ScheduledAt.HasValue&&(ticket.ScheduledAt>=CampusNow.AddHours(1)||CalendarBlocks(ticket.ServiceType,ticket.ScheduledAt.Value,_data.Settings.AppointmentSlotMinutes));}
    public (bool ok,string message) SaveClosure(ClosureInput input,int staffId) {
        lock(_lock) {
            if(!Offices.Contains(input.Office)||string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>120||input.Mode is not ("Date" or "Weekly")||input.Mode=="Date"&&!input.Date.HasValue||input.Mode=="Weekly"&&input.Weekday.HasValue&&(input.Weekday<0||input.Weekday>6)||!input.AllDay&&(!Clock(input.StartTime,out var from)||!Clock(input.EndTime,out var to)||from>=to))return(false,"Choose an office, a date or weekly day, a title, and a valid time range within one day.");
            if(input.Mode=="Weekly")input.Date=null;else input.Weekday=null;
            if(_data.OfficeClosures.Count>=500)return(false,"Remove old office closures before adding more.");
            var closure=new OfficeClosure{Id=NextId(_data.OfficeClosures.Select(c=>c.Id)),Office=input.Office,Date=input.Date,Weekday=input.Weekday,AllDay=input.AllDay,StartTime=input.StartTime,EndTime=input.EndTime,Title=input.Title.Trim()};_data.OfficeClosures.Add(closure);
            var affected=_data.Tickets.Where(t=>t.Status=="Scheduled"&&t.ScheduledAt.HasValue&&ClosureBlocks(closure,t.ServiceType,t.ScheduledAt.Value,Math.Max(_data.Settings.AppointmentSlotMinutes,EstimateServiceMinutes(t.ServiceType,"Regular")))).ToList();
            foreach(var ticket in affected)if(ticket.CustomerUserId.HasValue)AddAccountNotification(ticket.CustomerUserId.Value,ticket.TicketNumber,"Office availability changed","Your reserved visit overlaps an office closure. Your reservation was not automatically cancelled. Open My requests to choose a new time or contact the office.");
            AddLog("Office calendar",$"Staff #{staffId} added closure #{closure.Id}; {affected.Count} reservations currently overlap closures.");Save();return(true,$"Office closure saved. {affected.Count} affected reservation(s) received an in-app notice; their times remain unchanged.");
        }
    }
    public bool RemoveClosure(int id,int staffId) {lock(_lock){if(_data.OfficeClosures.RemoveAll(c=>c.Id==id)==0)return false;AddLog("Office calendar",$"Staff #{staffId} removed closure #{id}.");Save();return true;}}
    public OperationsReport GetOperationsReport(DateOnly from,DateOnly to,string office) {
        lock(_lock) {
            var tickets=_data.Tickets.Where(t=>DateOnly.FromDateTime(t.CreatedAt)>=from&&DateOnly.FromDateTime(t.CreatedAt)<=to&&(office=="All Services"||t.Office==office)).ToList();
            var waits=tickets.Where(t=>t.CalledAt.HasValue&&(!t.ScheduledAt.HasValue||t.CheckedInAt.HasValue)).Select(t=>(t.CalledAt!.Value-(t.CheckedInAt??t.CreatedAt)).TotalMinutes).Where(v=>v>=0).ToList();
            var numbers=tickets.Select(t=>t.TicketNumber).ToHashSet();var feedback=_data.Feedback.Where(f=>numbers.Contains(f.TicketNumber)).ToList();
            return new(){From=from,To=to,Office=office,Requests=tickets.Count,CompletedVisits=tickets.Count(t=>t.Status=="Served"),AverageWaitMinutes=waits.Count==0?null:Math.Round(waits.Average(),1),WaitSamples=waits.Count,ReleasedDocuments=tickets.Count(t=>t.DocumentStatus=="Released"),FeedbackCount=feedback.Count,AverageRating=feedback.Count==0?null:Math.Round(feedback.Average(f=>f.Rating),2),Transactions=tickets.GroupBy(t=>t.RequestType).Select(g=>new MetricRow(g.Key,g.Count())).OrderByDescending(r=>r.Count).ToList(),QueueStatuses=tickets.GroupBy(t=>t.Status).Select(g=>new MetricRow(g.Key,g.Count())).ToList(),DocumentStatuses=tickets.Where(t=>t.ServiceType=="Document Request").GroupBy(t=>t.DocumentStatus??"For review").Select(g=>new MetricRow(g.Key,g.Count())).ToList(),Feedback=feedback.OrderByDescending(f=>f.SubmittedAt).Take(100).ToList()};
        }
    }
    private DateTimeOffset _reminderPauseUntil;
    public void PauseRemindersForRestore(){lock(_lock)_reminderPauseUntil=DateTimeOffset.UtcNow.AddMinutes(1);}
    public int ProcessReminders(DateTimeOffset? time=null) {lock(_lock){if(DateTimeOffset.UtcNow<_reminderPauseUntil)return 0;var expired=ExpireOldTickets(_data);var count=ProcessRemindersLocked(time??CampusNow);if(count>0||expired)Save();return count;}}
    private int ProcessRemindersLocked(DateTimeOffset now) {
        if(!_data.Settings.EnableAccountNotifications)return 0;var count=0;
        foreach(var ticket in _data.Tickets.Where(t=>t.Status=="Scheduled"&&t.ScheduledAt.HasValue&&t.CustomerUserId.HasValue&&t.ScheduledAt>now&&t.ScheduledAt<=now.AddMinutes(30)&&t.VisitReminderFor!=t.ScheduledAt&&!CalendarBlocks(t.ServiceType,t.ScheduledAt.Value,_data.Settings.AppointmentSlotMinutes))) {
            AddAccountNotification(ticket.CustomerUserId!.Value,ticket.TicketNumber,"Visit reminder",$"Your {ticket.Office} visit is at {ticket.ScheduledAt:MMM d, h:mm tt} Manila time. Check office notices and check in when you arrive; check-in opens 15 minutes early.");ticket.VisitReminderFor=ticket.ScheduledAt;count++;
        }
        if(_data.Settings.NotifyOnAlmostNext)foreach(var group in WaitingOrdered().GroupBy(t=>t.Office)) {
            if(!_data.Counters.Any(c=>(c.Office==group.Key||c.Office=="All Services")&&c.Status is "Available" or "Serving"))continue;
            foreach(var ticket in group.Take(Math.Clamp(_data.Settings.AlmostNextPosition,1,10)).Where(t=>t.CustomerUserId.HasValue&&!t.AlmostNextReminded&&!CalendarBlocks(t.ServiceType,now,1))) {
                AddAccountNotification(ticket.CustomerUserId!.Value,ticket.TicketNumber,"Queue reminder",$"Your ticket is among the next {Math.Clamp(_data.Settings.AlmostNextPosition,1,10)} waiting visitors for {ticket.Office}. Stay nearby and watch for the counter call; this is not an exact service time.");ticket.AlmostNextReminded=true;count++;
            }
        }
        return count;
    }
}

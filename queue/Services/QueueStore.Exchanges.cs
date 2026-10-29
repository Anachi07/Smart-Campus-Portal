using System.Globalization;
using SmartQueueSystemGUI.Models;
namespace SmartQueueSystemGUI.Services;

public partial class QueueStore
{
    private Ticket? OwnedReservation(string number,int userId) => _data.Tickets.FirstOrDefault(t=>t.TicketNumber==number && t.CustomerUserId==userId && t.Status=="Scheduled" && t.ScheduledAt.HasValue);
    private int? CampusId(int? queueUserId) {
        var username=_data.Users.FirstOrDefault(u=>u.Id==queueUserId)?.Username??"";
        return username.StartsWith("campus:",StringComparison.Ordinal) && int.TryParse(username[7..],out var id) ? id : null;
    }
    private static bool ParseVisitTime(string? value,out DateTimeOffset at) {
        if(DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)) {at=TimeZoneInfo.ConvertTime(parsed,CampusZone);return true;}
        at=default;return false;
    }
    private static bool Expected(Ticket ticket,string value)=>ParseVisitTime(value,out var at)&&ticket.ScheduledAt==at;
    private List<Ticket> ExchangeCandidates(Ticket offer,DateTimeOffset desired) => _data.Tickets.Where(t=>t.Status=="Scheduled" && t.ScheduledAt==desired && t.CustomerUserId.HasValue && t.CustomerUserId!=offer.CustomerUserId && t.Office==offer.Office && CampusId(t.CustomerUserId).HasValue).ToList();
    private bool AvailableAfterRemoving(Ticket ticket,DateTimeOffset at,List<CampusClass> classes,HashSet<string> exclude) => ticket.CustomerUserId.HasValue && SlotsLocked(ticket.ServiceType,DateOnly.FromDateTime(at.DateTime),ticket.CustomerUserId.Value,classes,exclude).Any(s=>s.StartsAt==at);
    private bool PairEligible(Ticket offer,Ticket target,Dictionary<int,VisitEligibility> eligibility) {
        var from=CampusId(offer.CustomerUserId);var to=CampusId(target.CustomerUserId);
        if(!from.HasValue||!to.HasValue||!eligibility.TryGetValue(from.Value,out var first)||!eligibility.TryGetValue(to.Value,out var second)||!first.Active||!second.Active)return false;
        if(offer.Status!="Scheduled"||target.Status!="Scheduled"||!offer.ScheduledAt.HasValue||!target.ScheduledAt.HasValue||offer.Office!=target.Office||offer.CustomerUserId==target.CustomerUserId)return false;
        var exclude=new HashSet<string>{offer.TicketNumber,target.TicketNumber};
        return AvailableAfterRemoving(offer,target.ScheduledAt.Value,first.Classes,exclude)&&AvailableAfterRemoving(target,offer.ScheduledAt.Value,second.Classes,exclude);
    }
    private void ExchangeNotice(Ticket ticket,string title,string body) {
        if(ticket.CustomerUserId.HasValue)AddAccountNotification(ticket.CustomerUserId.Value,ticket.TicketNumber,title,body);
    }
    private void CloseExchange(VisitExchange exchange,string status,string notice) {
        exchange.Status=status;
        foreach(var ticket in _data.Tickets.Where(t=>t.TicketNumber==exchange.OfferTicketNumber||exchange.CandidateTicketNumbers.Contains(t.TicketNumber))) ExchangeNotice(ticket,"Visit exchange update",notice);
    }
    private void RefreshExchangesLocked() {
        var changed=false;
        if(ExpireOldTickets(_data))changed=true;
        foreach(var exchange in _data.VisitExchanges.Where(e=>e.Status=="Pending")) {
            var offer=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==exchange.OfferTicketNumber);
            if(exchange.ExpiresAt<=CampusNow||offer?.Status!="Scheduled"||offer.ScheduledAt!=exchange.OfferStartsAt) {CloseExchange(exchange,"Expired","A visit exchange expired or its original reservation changed. Your current reservation is unchanged.");changed=true;continue;}
            var eligible=exchange.CandidateTicketNumbers.Except(exchange.DeclinedTicketNumbers).Any(n=>_data.Tickets.Any(t=>t.TicketNumber==n&&t.Status=="Scheduled"&&t.ScheduledAt==exchange.DesiredStartsAt));
            if(!eligible){CloseExchange(exchange,"Unavailable","The requested visit exchange is no longer available. Your current reservation is unchanged.");changed=true;}
        }
        if(changed)Save();
    }
    private void InvalidateExchangesLocked(string number) {
        foreach(var exchange in _data.VisitExchanges.Where(e=>e.Status=="Pending"&&(e.OfferTicketNumber==number||e.CandidateTicketNumbers.Contains(number)))) {
            // A changed candidate does not cancel invitations to other candidates.
            if(exchange.OfferTicketNumber==number)CloseExchange(exchange,"Cancelled","A visit exchange was withdrawn because its reservation changed. No times were exchanged.");
            else {exchange.DeclinedTicketNumbers.Add(number);if(!exchange.CandidateTicketNumbers.Except(exchange.DeclinedTicketNumbers).Any())CloseExchange(exchange,"Unavailable","A visit exchange is no longer available. Your reservation is unchanged.");}
        }
    }
    public (bool ok,string message,Ticket? ticket) RescheduleVisit(ChangeVisitRequest request,int userId,List<CampusClass> classes) {
        lock(_lock) {
            RefreshExchangesLocked();var ticket=OwnedReservation(request.TicketNumber,userId);
            if(!request.Confirmed)return(false,"Review the proposed time and confirm the change.",null);
            if(ticket==null)return(false,"Only your upcoming, unchecked-in reservation can be changed.",null);
            if(!ParseVisitTime(request.StartsAt,out var at))return(false,"Choose an available time.",null);
            if(ticket.ScheduledAt==at)return(true,"Your reservation already has this time.",ticket);
            if(!Expected(ticket,request.ExpectedStartsAt))return(false,"Your reservation changed. Refresh My requests before continuing.",null);
            if(ticket.ScheduledAt<CampusNow.AddHours(1))return(false,"Changes close one hour before your current visit. Contact the office.",null);
            if(!AvailableAfterRemoving(ticket,at,classes,new(){ticket.TicketNumber}))return(false,"That time is unavailable or conflicts with your saved schedule. Choose another time, or request a voluntary exchange.",null);
            InvalidateExchangesLocked(ticket.TicketNumber);ticket.ScheduledAt=at;ticket.BookingKey="";
            ExchangeNotice(ticket,"Visit rescheduled",$"Your visit is now {at:MMM d, yyyy h:mm tt} Manila time. Check-in rules still apply.");
            AddLog("Visit rescheduled",$"{ticket.TicketNumber} moved to {at:O} after owner confirmation.");RefreshQueuePredictionsLocked();Save();return(true,"Your visit was rescheduled. The old time has been released.",ticket);
        }
    }
    public List<BookingSlot> GetChangeSlots(string number,int userId,List<CampusClass> classes) {
        lock(_lock) {RefreshExchangesLocked();var ticket=OwnedReservation(number,userId);return ticket==null||ticket.ScheduledAt<CampusNow.AddHours(1)?new():SlotsLocked(ticket.ServiceType,null,userId,classes,new(){ticket.TicketNumber}).Where(s=>s.StartsAt!=ticket.ScheduledAt).ToList();}
    }
    public List<ExchangeTime> GetExchangeTimes(string number,int userId,List<CampusClass> classes) {
        lock(_lock) {
            RefreshExchangesLocked();var offer=OwnedReservation(number,userId);if(offer==null||offer.ScheduledAt<CampusNow.AddHours(1))return new();
            return _data.Tickets.Where(t=>t.Status=="Scheduled"&&t.CustomerUserId!=userId&&t.Office==offer.Office&&t.ScheduledAt.HasValue&&t.ScheduledAt!=offer.ScheduledAt&&CampusId(t.CustomerUserId).HasValue)
                .Where(t=>AvailableAfterRemoving(offer,t.ScheduledAt!.Value,classes,new(){offer.TicketNumber,t.TicketNumber}))
                .Select(t=>new ExchangeTime(t.ScheduledAt!.Value,t.Office)).Distinct().OrderBy(t=>t.StartsAt).Take(100).ToList();
        }
    }
    public List<int> ExchangeParticipantIds(ChangeVisitRequest request,int userId) {
        lock(_lock) {var offer=OwnedReservation(request.TicketNumber,userId);if(offer==null||!Expected(offer,request.ExpectedStartsAt)||!ParseVisitTime(request.StartsAt,out var at))return new();return ExchangeCandidates(offer,at).Append(offer).Select(t=>CampusId(t.CustomerUserId)).Where(id=>id.HasValue).Select(id=>id!.Value).Distinct().ToList();}
    }
    public (bool ok,string message) RequestExchange(ChangeVisitRequest request,int userId,Dictionary<int,VisitEligibility> eligibility) {
        lock(_lock) {
            RefreshExchangesLocked();var offer=OwnedReservation(request.TicketNumber,userId);
            if(!request.Confirmed)return(false,"Confirm that you want to offer your current time for an exchange.");
            if(offer==null||!Expected(offer,request.ExpectedStartsAt)||!ParseVisitTime(request.StartsAt,out var desired)||desired==offer.ScheduledAt)return(false,"The reservation changed. Refresh My requests.");
            if(_data.VisitExchanges.Any(e=>e.Status=="Pending"&&e.OfferTicketNumber==offer.TicketNumber))return(false,"This reservation already has a pending exchange. Withdraw it before sending another.");
            var candidates=ExchangeCandidates(offer,desired).Where(t=>PairEligible(offer,t,eligibility)).ToList();
            if(candidates.Count==0)return(false,"No eligible reservation can exchange with that time. No invitations were sent.");
            var expiry=new[]{CampusNow.AddHours(24),offer.ScheduledAt!.Value.AddHours(-1),desired.AddHours(-1)}.Min();
            if(expiry<=CampusNow)return(false,"Changes close one hour before either visit.");
            var exchange=new VisitExchange {OfferTicketNumber=offer.TicketNumber,OfferStartsAt=offer.ScheduledAt.Value,DesiredStartsAt=desired,CandidateTicketNumbers=candidates.Select(t=>t.TicketNumber).ToList(),ExpiresAt=expiry};
            _data.VisitExchanges.Add(exchange);
            foreach(var target in candidates)ExchangeNotice(target,"Visit exchange invitation",$"Another user offers {exchange.OfferStartsAt:MMM d, h:mm tt} Manila time in exchange for your {desired:MMM d, h:mm tt} visit. Open Ask ICAS → Exchange requests to accept or decline. Your time stays unchanged unless you accept.");
            ExchangeNotice(offer,"Exchange requested","The eligible reservation holder(s) received an in-app invitation. Your current time stays reserved until someone accepts.");
            AddLog("Visit exchange requested",$"{offer.TicketNumber} requested a voluntary exchange.");Save();return(true,"Exchange invitation sent in the portal. Your current time stays reserved until someone accepts; no response means no change.");
        }
    }
    public List<ExchangeView> GetExchanges(int userId) {
        lock(_lock) {
            RefreshExchangesLocked();var own=_data.Tickets.Where(t=>t.CustomerUserId==userId).Select(t=>t.TicketNumber).ToHashSet();
            return _data.VisitExchanges.AsEnumerable().Reverse().Where(e=>own.Contains(e.OfferTicketNumber)||e.CandidateTicketNumbers.Any(own.Contains)).OrderByDescending(e=>e.Status=="Pending").ThenByDescending(e=>e.ExpiresAt).Take(30).Select(e=>{
                var incoming=!own.Contains(e.OfferTicketNumber);var number=incoming?e.CandidateTicketNumbers.First(own.Contains):e.OfferTicketNumber;
                var declined=e.DeclinedTicketNumbers.Contains(number);
                return new ExchangeView(e.Id,number,incoming?e.DesiredStartsAt:e.OfferStartsAt,incoming?e.OfferStartsAt:e.DesiredStartsAt,declined?"Declined":incoming&&e.Status=="Accepted"&&e.AcceptedTicketNumber!=number?"Closed":e.Status,e.ExpiresAt,incoming,incoming&&!declined&&e.Status=="Pending",!incoming&&e.Status=="Pending");
            }).ToList();
        }
    }
    public List<int> ExchangeResponseParticipants(string id,int userId) {
        lock(_lock) {var exchange=_data.VisitExchanges.FirstOrDefault(e=>e.Id==id&&e.Status=="Pending");if(exchange==null)return new();var target=_data.Tickets.FirstOrDefault(t=>exchange.CandidateTicketNumbers.Contains(t.TicketNumber)&&t.CustomerUserId==userId&&!exchange.DeclinedTicketNumbers.Contains(t.TicketNumber));var offer=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==exchange.OfferTicketNumber);return target==null||offer==null?new():new[]{CampusId(target.CustomerUserId),CampusId(offer.CustomerUserId)}.Where(id=>id.HasValue).Select(id=>id!.Value).Distinct().ToList();}
    }
    public (bool ok,string message) RespondExchange(SwapAction action,int userId,Dictionary<int,VisitEligibility> eligibility) {
        lock(_lock) {
            RefreshExchangesLocked();var exchange=_data.VisitExchanges.FirstOrDefault(e=>e.Id==action.Id);
            if(!action.Confirmed)return(false,"Review and confirm your exchange decision.");
            if(exchange==null||exchange.Status!="Pending")return(false,"This exchange is no longer pending. Refresh your requests.");
            var offer=_data.Tickets.FirstOrDefault(t=>t.TicketNumber==exchange.OfferTicketNumber);
            if(action.Decision=="withdraw") {
                if(offer?.CustomerUserId!=userId)return(false,"Exchange not found.");
                CloseExchange(exchange,"Cancelled","The requester withdrew the exchange. Existing reservations are unchanged.");Save();return(true,"Exchange withdrawn. Your current visit is unchanged.");
            }
            var target=_data.Tickets.FirstOrDefault(t=>t.CustomerUserId==userId&&exchange.CandidateTicketNumbers.Contains(t.TicketNumber)&&!exchange.DeclinedTicketNumbers.Contains(t.TicketNumber));
            if(target==null)return(false,"This invitation is not available for your account.");
            if(action.Decision=="decline") {exchange.DeclinedTicketNumbers.Add(target.TicketNumber);ExchangeNotice(target,"Exchange declined","You declined the exchange. Your visit is unchanged.");if(!exchange.CandidateTicketNumbers.Except(exchange.DeclinedTicketNumbers).Any())CloseExchange(exchange,"Declined","The exchange was declined. Your visit is unchanged.");Save();return(true,"Invitation declined. Your visit is unchanged.");}
            if(action.Decision!="accept")return(false,"Choose accept, decline, or withdraw.");
            if(offer==null||offer.ScheduledAt!=exchange.OfferStartsAt||target.ScheduledAt!=exchange.DesiredStartsAt||!PairEligible(offer,target,eligibility)) {CloseExchange(exchange,"Unavailable","The exchange no longer fits both reservations, school rules, or class schedules. No times were exchanged.");Save();return(false,"The exchange is no longer available or conflicts with a saved schedule. No reservations changed.");}
            exchange.Status="Accepted";exchange.AcceptedTicketNumber=target.TicketNumber;
            InvalidateExchangesLocked(offer.TicketNumber);InvalidateExchangesLocked(target.TicketNumber);
            offer.ScheduledAt=exchange.DesiredStartsAt;target.ScheduledAt=exchange.OfferStartsAt;offer.BookingKey="";target.BookingKey="";
            foreach(var ticket in new[]{offer,target})ExchangeNotice(ticket,"Visit exchange confirmed",$"Both sides agreed. Your visit is now {ticket.ScheduledAt:MMM d, yyyy h:mm tt} Manila time. Open My requests to review.");
            foreach(var other in _data.Tickets.Where(t=>exchange.CandidateTicketNumbers.Contains(t.TicketNumber)&&t.TicketNumber!=target.TicketNumber))ExchangeNotice(other,"Exchange closed","Another holder accepted the exchange. Your visit is unchanged.");
            AddLog("Visit exchange accepted",$"{offer.TicketNumber} and {target.TicketNumber} exchanged times after both owners consented.");RefreshQueuePredictionsLocked();Save();return(true,"Exchange confirmed. Both visit times were updated together.");
        }
    }
}

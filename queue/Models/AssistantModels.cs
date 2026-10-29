namespace SmartQueueSystemGUI.Models;

public record CampusClass(string Day, string Start, string End);
public record SchoolFaq(int Id, string Question, string Answer, string SourceLabel);
public record SchoolKnowledge(string SchoolName, List<SchoolFaq> Faqs);
public record BookingSlot(DateTimeOffset StartsAt, string Office, string Reason, int RemainingCapacity, bool Recommended);
public record ChatTurn(string Role, string Content);
public class ChatRequest { public string Message { get; set; } = ""; public List<ChatTurn> History { get; set; } = new(); }
public record AssistantReply(string Answer, string Mode, List<SchoolFaq> Sources, bool SuggestScheduling, string? ServiceType);
public class AppointmentRequest
{
    public string ServiceType { get; set; } = "";
    public string RequestType { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string StudentNumber { get; set; } = "";
    public string RequestNotes { get; set; } = "";
    public string StartsAt { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public bool Confirmed { get; set; }
}
public class TicketAction { public string TicketNumber { get; set; } = ""; }

public class ChangeVisitRequest
{
    public string TicketNumber { get; set; } = "";
    public string ExpectedStartsAt { get; set; } = "";
    public string StartsAt { get; set; } = "";
    public bool Confirmed { get; set; }
}
public class SwapAction
{
    public string Id { get; set; } = "";
    public string Decision { get; set; } = "";
    public bool Confirmed { get; set; }
}
public class VisitExchange
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OfferTicketNumber { get; set; } = "";
    public DateTimeOffset OfferStartsAt { get; set; }
    public DateTimeOffset DesiredStartsAt { get; set; }
    public List<string> CandidateTicketNumbers { get; set; } = new();
    public List<string> DeclinedTicketNumbers { get; set; } = new();
    public string? AcceptedTicketNumber { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset ExpiresAt { get; set; }
}
public record VisitEligibility(int CampusId, bool Active, List<CampusClass> Classes);
public record ExchangeView(string Id, string TicketNumber, DateTimeOffset CurrentStartsAt, DateTimeOffset ProposedStartsAt, string Status, DateTimeOffset ExpiresAt, bool Incoming, bool CanRespond, bool CanCancel);
public record ExchangeTime(DateTimeOffset StartsAt, string Office);

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

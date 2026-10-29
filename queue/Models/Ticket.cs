namespace SmartQueueSystemGUI.Models;

public class Ticket
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int? CustomerUserId { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string RequestNotes { get; set; } = string.Empty;
    public string Office => CampusServices.OfficeFor(ServiceType);
    public string PriorityType { get; set; } = "Regular";
    public string Status { get; set; } = "Waiting";
    public string CounterName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string BookingKey { get; set; } = string.Empty;
    public string? DocumentStatus { get; set; }
    public string ClaimCode { get; set; } = "";
    public List<DocumentProgress> DocumentHistory { get; set; } = new();
    public List<RequestChecklistItem> Checklist { get; set; } = new();
    public DateTimeOffset? VisitReminderFor { get; set; }
    public bool AlmostNextReminded { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }

    public int EstimatedWaitMinutes { get; set; }
    public string EstimatedWaitText { get; set; } = "Calculating";
    public DateTime? EstimatedServiceStartAt { get; set; }
    public string PredictedCounterName { get; set; } = string.Empty;
    public string BestVisitTime { get; set; } = string.Empty;
    public string AiInsight { get; set; } = string.Empty;
}

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

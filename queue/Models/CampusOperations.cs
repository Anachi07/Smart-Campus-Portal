namespace SmartQueueSystemGUI.Models;

public class ServiceRequirement
{
    public int Id { get; set; }
    public string ServiceType { get; set; } = "Document Request";
    public string RequestType { get; set; } = "Certificate of Enrollment";
    public string Title { get; set; } = "";
    public string? Instructions { get; set; } = "";
    public bool Required { get; set; } = true;
    public bool Active { get; set; } = true;
}
public class RequestChecklistItem
{
    public int RequirementId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public bool Required { get; set; }
    public bool Prepared { get; set; }
    public bool Verified { get; set; }
}
public class DocumentProgress
{
    public string Status { get; set; } = "For review";
    public string Note { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public int? StaffId { get; set; }
}
public class OfficeClosure
{
    public int Id { get; set; }
    public string Office { get; set; } = "Registrar";
    public DateOnly? Date { get; set; }
    public int? Weekday { get; set; }
    public bool AllDay { get; set; }
    public string StartTime { get; set; } = "12:00";
    public string EndTime { get; set; } = "13:00";
    public string Title { get; set; } = "";
}
public class ClosureInput : OfficeClosure { public string Mode { get; set; } = "Date"; }
public class ServiceFeedback
{
    public string TicketNumber { get; set; } = "";
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; }
}
public class RequestDetailViewModel
{
    public Ticket Ticket { get; set; } = new();
    public ServiceFeedback? Feedback { get; set; }
    public bool Staff { get; set; }
    public bool CanFeedback { get; set; }
    public string? Message { get; set; }
}
public record MetricRow(string Label,int Count);
public class OperationsReport
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Office { get; set; } = "All Services";
    public int Requests { get; set; }
    public int CompletedVisits { get; set; }
    public double? AverageWaitMinutes { get; set; }
    public int WaitSamples { get; set; }
    public int ReleasedDocuments { get; set; }
    public int FeedbackCount { get; set; }
    public double? AverageRating { get; set; }
    public List<MetricRow> Transactions { get; set; } = new();
    public List<MetricRow> QueueStatuses { get; set; } = new();
    public List<MetricRow> DocumentStatuses { get; set; } = new();
    public List<ServiceFeedback> Feedback { get; set; } = new();
}

namespace SmartQueueSystemGUI.Models;

public class SmartQueueData
{
    public List<User> Users { get; set; } = new();
    public List<Ticket> Tickets { get; set; } = new();
    public List<Counter> Counters { get; set; } = new();
    public List<SmsNotification> SmsNotifications { get; set; } = new();
    public List<AccountNotification> AccountNotifications { get; set; } = new();
    public List<ActivityLog> ActivityLogs { get; set; } = new();
    public List<VisitExchange> VisitExchanges { get; set; } = new();
    public List<ServiceRequirement> ServiceRequirements { get; set; } = new();
    public List<OfficeClosure> OfficeClosures { get; set; } = new();
    public List<ServiceFeedback> Feedback { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}

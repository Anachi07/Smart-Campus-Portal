namespace SmartQueueSystemGUI.Models;

public class SmsNotification
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Logged";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

namespace SmartQueueSystemGUI.Models;

public class AccountNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

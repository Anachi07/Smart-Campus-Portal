namespace SmartQueueSystemGUI.Models;

public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class RegisterViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class TicketRequestViewModel
{
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ServiceType { get; set; } = "Document Request";
    public string RequestType { get; set; } = "Certificate of Enrollment";
    public string StudentNumber { get; set; } = string.Empty;
    public string RequestNotes { get; set; } = string.Empty;
    public string PriorityType { get; set; } = "Regular";
    public string? ErrorMessage { get; set; }
}

public class DashboardViewModel
{
    public int Waiting { get; set; }
    public int NowServing { get; set; }
    public int ServedToday { get; set; }
    public int SkippedToday { get; set; }
    public int TotalToday { get; set; }
    public int ActiveCounters { get; set; }
    public double AverageWaitingMinutes { get; set; }
    public string BestVisitTime { get; set; } = string.Empty;
    public string QueueLoadLabel { get; set; } = string.Empty;
    public int PredictedAverageWaitMinutes { get; set; }
    public List<Ticket> RecentTickets { get; set; } = new();
    public List<SmsNotification> RecentNotifications { get; set; } = new();
    public List<AccountNotification> RecentAccountNotifications { get; set; } = new();
}

public class QueueControlViewModel
{
    public List<Ticket> WaitingTickets { get; set; } = new();
    public List<Ticket> NowServingTickets { get; set; } = new();
    public List<Ticket> ServedTickets { get; set; } = new();
    public List<Ticket> SkippedTickets { get; set; } = new();
    public List<Counter> Counters { get; set; } = new();
    public string BestVisitTime { get; set; } = string.Empty;
    public string QueueLoadLabel { get; set; } = string.Empty;
}


public class ReportViewModel
{
    public int TotalTickets { get; set; }
    public int Waiting { get; set; }
    public int NowServing { get; set; }
    public int Served { get; set; }
    public int Skipped { get; set; }
    public double AverageWaitingMinutes { get; set; }
    public Dictionary<string, int> ByService { get; set; } = new();
    public Dictionary<string, int> ByPriority { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public List<Ticket> Tickets { get; set; } = new();
    public string BestVisitTime { get; set; } = string.Empty;
    public string QueueLoadLabel { get; set; } = string.Empty;
}


public class TicketSearchViewModel
{
    public string SearchText { get; set; } = string.Empty;
    public List<Ticket> Results { get; set; } = new();
    public string? Message { get; set; }
    public List<AccountNotification> AccountNotifications { get; set; } = new();
    public string BestVisitTime { get; set; } = string.Empty;
    public string QueueLoadLabel { get; set; } = string.Empty;
}


public class UserManagementViewModel
{
    public List<User> Users { get; set; } = new();
    public List<User> AdminStaffUsers { get; set; } = new();
    public List<User> CustomerUsers { get; set; } = new();
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
    public string? ErrorMessage { get; set; }
}

public class NotificationCenterViewModel
{
    public List<SmsNotification> PhoneNotifications { get; set; } = new();
    public List<AccountNotification> AccountNotifications { get; set; } = new();
}

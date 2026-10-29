namespace SmartQueueSystemGUI.Models;

public class AppSettings
{
    public string EstablishmentName { get; set; } = "ICAS Philippines";
    public string SmsProviderName { get; set; } = "Phone Gateway";
    public string SmsApiKey { get; set; } = string.Empty;
    public bool EnablePhoneNotifications { get; set; } = true;
    public bool EnableAccountNotifications { get; set; } = true;
    public bool NotifyOnTicketCreated { get; set; } = true;
    public bool NotifyOnAlmostNext { get; set; } = true;
    public bool NotifyOnNowServing { get; set; } = true;
    public bool NotifyOnSkipped { get; set; } = true;
    public bool NotifyOnCompleted { get; set; } = true;
    public bool EnableCsNotifications { get; set; } = true;
    public bool NotifyCsOnTicketCreated { get; set; } = true;
    public bool NotifyCsOnSkipped { get; set; } = true;
    public int AlmostNextPosition { get; set; } = 1;
    public string AccentTheme { get; set; } = "Blue";

    public bool EnableAiRecommendations { get; set; } = true;
    public int PredictionBufferMinutes { get; set; } = 1;
    public int DefaultServiceMinutes { get; set; } = 5;
    public int GeneralInquiryMinutes { get; set; } = 3;
    public int PaymentMinutes { get; set; } = 4;
    public int TuitionMinutes { get; set; } = 8;
    public int RegistrationMinutes { get; set; } = 7;
    public int DocumentRequestMinutes { get; set; } = 6;
    public int AppointmentMinutes { get; set; } = 8;
    public int CustomerServiceMinutes { get; set; } = 6;
    public int PriorityBoostMinutes { get; set; } = 2;
    public int BusinessStartHour { get; set; } = 8;
    public int BusinessEndHour { get; set; } = 17;
    public bool EnableAppointments { get; set; } = true;
    public int AppointmentSlotMinutes { get; set; } = 30;
    public int MaxBookingsPerSlot { get; set; } = 3;
    public int AdvanceBookingDays { get; set; } = 14;
    public List<int> BookingDays { get; set; } = new() { 1,2,3,4,5 };
    public int LowTrafficThreshold { get; set; } = 3;
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartQueueSystemGUI.Models;

namespace SmartQueueSystemGUI.Services;

public class QueueStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private SmartQueueData _data;

    public QueueStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "smartqueue-data.json");
        _data = LoadOrSeed();
    }

    // The SIMS id is namespaced so existing queue account ids remain intact.
    public User CampusUser(int campusId, string fullName, string campusRole, string phone)
    {
        lock (_lock)
        {
            phone = NormalizePhone(phone);
            var username = $"campus:{campusId}";
            var user = _data.Users.FirstOrDefault(u => u.Username == username);
            var role = campusRole == "Admin" ? "Admin" : "Customer";
            if (user == null)
            {
                user = new User { Id = NextId(_data.Users.Select(u => u.Id)), Username = username,
                    PasswordHash = "!external-session-only", FullName = fullName, Role = role, PhoneNumber = phone };
                _data.Users.Add(user);
                Save();
            }
            else if (user.FullName != fullName || user.Role != role || user.PhoneNumber != phone)
            {
                user.FullName = fullName; user.Role = role; user.PhoneNumber = phone; Save();
            }
            return new User { Id = user.Id, FullName = user.FullName, Role = user.Role, PhoneNumber = user.PhoneNumber, Username = user.Username };
        }
    }

    public SmartQueueData Snapshot()
    {
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            return Clone(_data);
        }
    }

    public bool HasAdministrator()
    {
        lock (_lock)
        {
            return _data.Users.Any(u => u.Role == "Admin");
        }
    }

    public static string DefaultAdminUsername => "admin";

    private static User BuildDefaultAdmin()
    {
        return new User
        {
            Id = 1,
            FullName = "System Administrator",
            PhoneNumber = "09999999999",
            Username = DefaultAdminUsername,
            PasswordHash = HashPassword("admin123"),
            Role = "Admin",
            CreatedAt = DateTime.Now
        };
    }

    public static bool IsValidPhilippinePhone(string phone)
    {
        phone = NormalizePhone(phone);
        return phone.Length == 11 && phone.StartsWith("09") && phone.All(char.IsDigit);
    }

    public static string NormalizePhone(string phone)
    {
        return (phone ?? string.Empty).Trim().Replace(" ", "").Replace("-", "");
    }

    public static string MaskPhone(string phone)
    {
        phone = NormalizePhone(phone);
        if (phone.Length < 7) return phone;
        return phone[..4] + "***" + phone[^4..];
    }

    public static string MaskUsername(string username)
    {
        username = (username ?? string.Empty).Trim();
        if (username.Length <= 2) return "••••";
        if (username.Length <= 5) return username[0] + "•••" + username[^1];
        return username[..2] + "••••" + username[^2..];
    }

    public static string StatusCss(string status)
    {
        return (status ?? string.Empty).ToLowerInvariant().Replace(" ", "-");
    }

    public User? Login(string username, string password)
    {
        lock (_lock)
        {
            var passwordHash = HashPassword(password);
            return _data.Users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == passwordHash);
        }
    }

    public (bool ok, string message, User? user) CreateInitialAdmin(string fullName, string phone, string username, string password, string confirmPassword)
    {
        lock (_lock)
        {
            if (_data.Users.Any(u => u.Role == "Admin"))
                return (false, "Administrator setup is already completed.", null);

            var result = ValidateUser(fullName, phone, username, password, confirmPassword, "Admin");
            if (!result.ok) return (false, result.message, null);

            var user = BuildUser(fullName, phone, username, password, "Admin");
            _data.Users.Add(user);
            AddLog("Initial administrator created", $"Administrator account created for {user.FullName}.");
            Save();
            return (true, "Administrator account created successfully.", user);
        }
    }

    public (bool ok, string message, User? user) RegisterCustomer(string fullName, string phone, string username, string password, string confirmPassword)
    {
        lock (_lock)
        {
            var result = ValidateUser(fullName, phone, username, password, confirmPassword, "Customer");
            if (!result.ok) return (false, result.message, null);

            var user = BuildUser(fullName, phone, username, password, "Customer");
            _data.Users.Add(user);
            AddLog("Customer registered", $"{user.FullName} registered with phone {MaskPhone(user.PhoneNumber)}.");
            AddAccountNotification(user.Id, string.Empty, "Account created", "Your customer account is ready. You can now generate and track queue tickets.");
            Save();
            return (true, "Registration successful.", user);
        }
    }

    public List<User> GetUsers()
    {
        lock (_lock)
        {
            return _data.Users
                .OrderBy(u => u.Role == "Admin" ? 0 : u.Role == "Staff" ? 1 : 2)
                .ThenBy(u => u.FullName)
                .ToList();
        }
    }

    public (bool ok, string message, User? user) CreateUser(string fullName, string phone, string username, string password, string confirmPassword, string role)
    {
        lock (_lock)
        {
            role = NormalizeRole(role);
            var result = ValidateUser(fullName, phone, username, password, confirmPassword, role);
            if (!result.ok) return (false, result.message, null);

            var user = BuildUser(fullName, phone, username, password, role);
            _data.Users.Add(user);
            AddLog("User account created", $"{role} account created for {user.FullName}.");
            AddAccountNotification(user.Id, string.Empty, "Account created", $"Your {role.ToLowerInvariant()} account has been created.");
            Save();
            return (true, "User account created successfully.", user);
        }
    }

    public (bool ok, string message) DeleteUser(int id, int currentUserId)
    {
        lock (_lock)
        {
            var user = _data.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return (false, "User not found.");
            if (user.Id == currentUserId) return (false, "You cannot delete your own active account.");
            if (user.Role == "Admin" && _data.Users.Count(u => u.Role == "Admin") <= 1)
                return (false, "At least one administrator account is required.");

            _data.Users.Remove(user);
            _data.AccountNotifications.RemoveAll(n => n.UserId == user.Id);
            AddLog("User account deleted", $"{user.Role} account for {user.FullName} was deleted.");
            Save();
            return (true, "User account deleted.");
        }
    }

    public (bool ok, string message, Ticket? ticket) CreateTicket(string customerName, string phone, string service, string priority, int? customerUserId = null)
    {
        phone = NormalizePhone(phone);
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(customerName)) return (false, "Customer name is required.", null);
            if (!IsValidPhilippinePhone(phone)) return (false, "Please enter a valid 11-digit phone number that starts with 09.", null);
            if (string.IsNullOrWhiteSpace(service)) return (false, "Service type is required.", null);

            var linkedUserId = ResolveCustomerUserId(phone, customerUserId);
            var ticket = new Ticket
            {
                Id = NextId(_data.Tickets.Select(x => x.Id)),
                TicketNumber = GenerateTicketNumber(service),
                CustomerName = customerName.Trim(),
                PhoneNumber = phone,
                CustomerUserId = linkedUserId,
                ServiceType = service.Trim(),
                PriorityType = string.IsNullOrWhiteSpace(priority) ? "Regular" : priority.Trim(),
                Status = "Waiting",
                CreatedAt = DateTime.Now
            };

            _data.Tickets.Add(ticket);
            RefreshQueuePredictionsLocked();
            AddLog("Ticket created", $"{ticket.TicketNumber} created for {ticket.CustomerName}, service: {ticket.ServiceType}, phone: {MaskPhone(phone)}, estimated wait: {ticket.EstimatedWaitText}.");
            NotifyCustomer(ticket, "Ticket created", $"Your queue ticket {ticket.TicketNumber} has been created for {ticket.ServiceType}. Estimated waiting time: {ticket.EstimatedWaitText}. Best time to visit today: {ticket.BestVisitTime}.", "created");
            NotifyCustomerService(ticket, "New customer ticket", $"New ticket {ticket.TicketNumber} for {ticket.ServiceType}. Estimated wait: {ticket.EstimatedWaitText}. Suggested counter: {ticket.PredictedCounterName}.", "created");
            Save();
            return (true, "Ticket created successfully.", ticket);
        }
    }

    public Ticket? FindTicket(string ticketNumber)
    {
        var key = NormalizeTicketKey(ticketNumber);
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            return _data.Tickets.FirstOrDefault(t => NormalizeTicketKey(t.TicketNumber) == key);
        }
    }

    public List<Ticket> SearchTickets(string search)
    {
        var raw = (search ?? string.Empty).Trim();
        var ticketKey = NormalizeTicketKey(raw);
        var phoneKey = NormalizePhone(raw);
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            return _data.Tickets
                .Where(t =>
                    (!string.IsNullOrWhiteSpace(ticketKey) && NormalizeTicketKey(t.TicketNumber).Contains(ticketKey)) ||
                    (!string.IsNullOrWhiteSpace(raw) && t.TicketNumber.Contains(raw, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(phoneKey) && t.PhoneNumber.Contains(phoneKey)))
                .OrderByDescending(t => t.CreatedAt)
                .Take(25)
                .ToList();
        }
    }

    public List<AccountNotification> GetAccountNotifications(int userId)
    {
        lock (_lock)
        {
            return _data.AccountNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .Select(CloneNotification)
                .ToList();
        }
    }

    public AccountNotification? GetLatestAccountNotification(int userId)
    {
        lock (_lock)
        {
            var item = _data.AccountNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefault();
            return item == null ? null : CloneNotification(item);
        }
    }

    public int GetUnreadNotificationCount(int userId)
    {
        lock (_lock)
        {
            return _data.AccountNotifications.Count(n => n.UserId == userId && !n.IsRead);
        }
    }

    public void MarkAccountNotificationsRead(int userId)
    {
        lock (_lock)
        {
            var changed = false;
            foreach (var item in _data.AccountNotifications.Where(n => n.UserId == userId && !n.IsRead))
            {
                item.IsRead = true;
                changed = true;
            }
            if (changed) Save();
        }
    }

    public DashboardViewModel GetDashboard()
    {
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            var today = DateTime.Today;
            return new DashboardViewModel
            {
                Waiting = _data.Tickets.Count(t => t.Status == "Waiting"),
                NowServing = _data.Tickets.Count(t => t.Status == "Now Serving"),
                ServedToday = _data.Tickets.Count(t => t.Status == "Served" && t.CompletedAt?.Date == today),
                SkippedToday = _data.Tickets.Count(t => t.Status == "Skipped" && t.SkippedAt?.Date == today),
                TotalToday = _data.Tickets.Count(t => t.CreatedAt.Date == today),
                ActiveCounters = _data.Counters.Count(c => c.Status != "Offline"),
                AverageWaitingMinutes = GetAverageWaitingMinutes(),
                BestVisitTime = RecommendBestVisitTimeLocked(),
                QueueLoadLabel = GetQueueLoadLabelLocked(),
                PredictedAverageWaitMinutes = GetPredictedAverageWaitLocked(),
                RecentTickets = _data.Tickets.OrderByDescending(t => t.CreatedAt).Take(10).ToList(),
                RecentNotifications = _data.SmsNotifications.OrderByDescending(s => s.CreatedAt).Take(8).ToList(),
                RecentAccountNotifications = _data.AccountNotifications.OrderByDescending(s => s.CreatedAt).Take(8).ToList()
            };
        }
    }

    public QueueControlViewModel GetQueueControl()
    {
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            return new QueueControlViewModel
            {
                WaitingTickets = WaitingOrdered().ToList(),
                NowServingTickets = _data.Tickets.Where(t => t.Status == "Now Serving").OrderByDescending(t => t.CalledAt).ToList(),
                ServedTickets = _data.Tickets.Where(t => t.Status == "Served").OrderByDescending(t => t.CompletedAt).Take(30).ToList(),
                SkippedTickets = _data.Tickets.Where(t => t.Status == "Skipped").OrderByDescending(t => t.SkippedAt).Take(30).ToList(),
                Counters = _data.Counters.OrderBy(c => c.Name).ToList(),
                BestVisitTime = RecommendBestVisitTimeLocked(),
                QueueLoadLabel = GetQueueLoadLabelLocked()
            };
        }
    }

    public Ticket? CallNext(string counterName)
    {
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            var next = WaitingOrdered().FirstOrDefault();
            if (next == null) return null;

            next.Status = "Now Serving";
            next.CounterName = string.IsNullOrWhiteSpace(counterName) ? "Counter 1" : counterName;
            next.CalledAt = DateTime.Now;
            AddLog("Now serving", $"{next.TicketNumber} is now serving at {next.CounterName}.");
            NotifyCustomer(next, "Now serving", $"Now serving {next.TicketNumber} at {next.CounterName}. Please proceed to the counter.", "serving");
            RefreshQueuePredictionsLocked();

            if (_data.Settings.NotifyOnAlmostNext)
            {
                var position = Math.Max(1, _data.Settings.AlmostNextPosition);
                var almostNext = WaitingOrdered().Skip(position - 1).FirstOrDefault();
                if (almostNext != null)
                {
                    NotifyCustomer(almostNext, "Almost next", $"Your ticket {almostNext.TicketNumber} is almost next. Updated estimated waiting time: {almostNext.EstimatedWaitText}. Please stay nearby.", "almost-next");
                }
            }

            RefreshQueuePredictionsLocked();
            Save();
            return next;
        }
    }

    public bool CompleteTicket(string ticketNumber)
    {
        lock (_lock)
        {
            var ticket = _data.Tickets.FirstOrDefault(t => NormalizeTicketKey(t.TicketNumber) == NormalizeTicketKey(ticketNumber));
            if (ticket == null) return false;
            ticket.Status = "Served";
            ticket.CompletedAt = DateTime.Now;
            AddLog("Ticket completed", $"{ticket.TicketNumber} has been served.");
            NotifyCustomer(ticket, "Ticket completed", $"Your ticket {ticket.TicketNumber} has been completed. Thank you.", "completed");
            RefreshQueuePredictionsLocked();
            Save();
            return true;
        }
    }

    public bool SkipTicket(string ticketNumber)
    {
        lock (_lock)
        {
            var ticket = _data.Tickets.FirstOrDefault(t => NormalizeTicketKey(t.TicketNumber) == NormalizeTicketKey(ticketNumber));
            if (ticket == null) return false;
            ticket.Status = "Skipped";
            ticket.SkippedAt = DateTime.Now;
            AddLog("Ticket skipped", $"{ticket.TicketNumber} was skipped.");
            NotifyCustomer(ticket, "Ticket skipped", $"Your ticket {ticket.TicketNumber} was skipped. Please approach the staff if you still need assistance.", "skipped");
            NotifyCustomerService(ticket, "Skipped ticket alert", $"Ticket {ticket.TicketNumber} was skipped at the counter. CS may assist the customer if needed.", "skipped");
            RefreshQueuePredictionsLocked();
            Save();
            return true;
        }
    }

    public bool ReturnTicket(string ticketNumber)
    {
        lock (_lock)
        {
            var ticket = _data.Tickets.FirstOrDefault(t => NormalizeTicketKey(t.TicketNumber) == NormalizeTicketKey(ticketNumber));
            if (ticket == null) return false;
            ticket.Status = "Waiting";
            ticket.CounterName = string.Empty;
            ticket.CalledAt = null;
            ticket.SkippedAt = null;
            AddLog("Ticket returned", $"{ticket.TicketNumber} returned to waiting queue.");
            RefreshQueuePredictionsLocked();
            NotifyCustomer(ticket, "Returned to queue", $"Your ticket {ticket.TicketNumber} has been returned to the waiting queue. New estimated waiting time: {ticket.EstimatedWaitText}.", "returned");
            Save();
            return true;
        }
    }

    public ReportViewModel GetReports()
    {
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            return new ReportViewModel
            {
                TotalTickets = _data.Tickets.Count,
                Waiting = _data.Tickets.Count(t => t.Status == "Waiting"),
                NowServing = _data.Tickets.Count(t => t.Status == "Now Serving"),
                Served = _data.Tickets.Count(t => t.Status == "Served"),
                Skipped = _data.Tickets.Count(t => t.Status == "Skipped"),
                ByService = _data.Tickets.GroupBy(t => t.ServiceType).ToDictionary(g => g.Key, g => g.Count()),
                ByPriority = _data.Tickets.GroupBy(t => t.PriorityType).ToDictionary(g => g.Key, g => g.Count()),
                ByStatus = _data.Tickets.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count()),
                AverageWaitingMinutes = GetAverageWaitingMinutes(),
                BestVisitTime = RecommendBestVisitTimeLocked(),
                QueueLoadLabel = GetQueueLoadLabelLocked(),
                Tickets = _data.Tickets.OrderByDescending(t => t.CreatedAt).ToList()
            };
        }
    }

    public void AddCounter(string name, string staffName)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _data.Counters.Add(new Counter
            {
                Id = NextId(_data.Counters.Select(c => c.Id)),
                Name = name.Trim(),
                StaffName = staffName?.Trim() ?? string.Empty,
                Status = "Available"
            });
            AddLog("Counter added", $"{name} was added.");
            Save();
        }
    }

    public void UpdateCounter(int id, string status)
    {
        lock (_lock)
        {
            var counter = _data.Counters.FirstOrDefault(c => c.Id == id);
            if (counter == null) return;
            counter.Status = status;
            AddLog("Counter updated", $"{counter.Name} status changed to {status}.");
            Save();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_lock)
        {
            _data.Settings.EstablishmentName = string.IsNullOrWhiteSpace(settings.EstablishmentName) ? "SmartQueueSystemGUI" : settings.EstablishmentName.Trim();
            _data.Settings.SmsProviderName = string.IsNullOrWhiteSpace(settings.SmsProviderName) ? "Phone Gateway" : settings.SmsProviderName.Trim();
            _data.Settings.SmsApiKey = settings.SmsApiKey?.Trim() ?? string.Empty;
            _data.Settings.EnablePhoneNotifications = settings.EnablePhoneNotifications;
            _data.Settings.EnableAccountNotifications = settings.EnableAccountNotifications;
            _data.Settings.NotifyOnTicketCreated = settings.NotifyOnTicketCreated;
            _data.Settings.NotifyOnAlmostNext = settings.NotifyOnAlmostNext;
            _data.Settings.NotifyOnNowServing = settings.NotifyOnNowServing;
            _data.Settings.NotifyOnSkipped = settings.NotifyOnSkipped;
            _data.Settings.NotifyOnCompleted = settings.NotifyOnCompleted;
            _data.Settings.EnableCsNotifications = settings.EnableCsNotifications;
            _data.Settings.NotifyCsOnTicketCreated = settings.NotifyCsOnTicketCreated;
            _data.Settings.NotifyCsOnSkipped = settings.NotifyCsOnSkipped;
            _data.Settings.AlmostNextPosition = Math.Clamp(settings.AlmostNextPosition, 1, 5);
            _data.Settings.AccentTheme = string.IsNullOrWhiteSpace(settings.AccentTheme) ? "Blue" : settings.AccentTheme.Trim();
            _data.Settings.EnableAiRecommendations = settings.EnableAiRecommendations;
            _data.Settings.PredictionBufferMinutes = Math.Clamp(settings.PredictionBufferMinutes, 0, 30);
            _data.Settings.DefaultServiceMinutes = Math.Clamp(settings.DefaultServiceMinutes, 1, 120);
            _data.Settings.GeneralInquiryMinutes = Math.Clamp(settings.GeneralInquiryMinutes, 1, 120);
            _data.Settings.PaymentMinutes = Math.Clamp(settings.PaymentMinutes, 1, 120);
            _data.Settings.RegistrationMinutes = Math.Clamp(settings.RegistrationMinutes, 1, 120);
            _data.Settings.DocumentRequestMinutes = Math.Clamp(settings.DocumentRequestMinutes, 1, 120);
            _data.Settings.AppointmentMinutes = Math.Clamp(settings.AppointmentMinutes, 1, 120);
            _data.Settings.CustomerServiceMinutes = Math.Clamp(settings.CustomerServiceMinutes, 1, 120);
            _data.Settings.PriorityBoostMinutes = Math.Clamp(settings.PriorityBoostMinutes, 0, 60);
            _data.Settings.BusinessStartHour = Math.Clamp(settings.BusinessStartHour, 0, 23);
            _data.Settings.BusinessEndHour = Math.Clamp(settings.BusinessEndHour, _data.Settings.BusinessStartHour + 1, 24);
            _data.Settings.LowTrafficThreshold = Math.Clamp(settings.LowTrafficThreshold, 0, 1000);
            RefreshQueuePredictionsLocked();
            AddLog("Settings updated", "Admin updated notification, waiting-time, and AI recommendation settings.");
            Save();
        }
    }

    private IEnumerable<Ticket> WaitingOrdered()
    {
        return _data.Tickets
            .Where(t => t.Status == "Waiting")
            .OrderByDescending(t => string.Equals(t.PriorityType, "Priority", StringComparison.OrdinalIgnoreCase))
            .ThenBy(t => t.CreatedAt);
    }


    public string GetBestVisitTimeRecommendation()
    {
        lock (_lock)
        {
            RefreshQueuePredictionsLocked();
            return RecommendBestVisitTimeLocked();
        }
    }

    private void RefreshQueuePredictionsLocked()
    {
        var now = DateTime.Now;
        var activeCounters = _data.Counters
            .Where(c => !string.Equals(c.Status, "Offline", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();

        if (!activeCounters.Any())
        {
            activeCounters.Add(new Counter { Id = 0, Name = "Counter 1", Status = "Available" });
        }

        var counterLoads = activeCounters.Select(c => 0.0).ToArray();
        var recommendation = RecommendBestVisitTimeLocked();
        var queueLength = _data.Tickets.Count(t => t.Status == "Waiting");

        foreach (var ticket in WaitingOrdered().ToList())
        {
            var minIndex = 0;
            for (var i = 1; i < counterLoads.Length; i++)
            {
                if (counterLoads[i] < counterLoads[minIndex]) minIndex = i;
            }

            var wait = Math.Max(0, (int)Math.Ceiling(counterLoads[minIndex] + _data.Settings.PredictionBufferMinutes));
            if (string.Equals(ticket.PriorityType, "Priority", StringComparison.OrdinalIgnoreCase))
            {
                wait = Math.Max(0, wait - _data.Settings.PriorityBoostMinutes);
            }

            ticket.EstimatedWaitMinutes = wait;
            ticket.EstimatedWaitText = FormatWaitMinutes(wait);
            ticket.EstimatedServiceStartAt = now.AddMinutes(wait);
            ticket.PredictedCounterName = activeCounters[minIndex].Name;
            ticket.BestVisitTime = recommendation;
            ticket.AiInsight = BuildAiInsight(ticket, queueLength, activeCounters.Count);

            counterLoads[minIndex] += EstimateServiceMinutes(ticket.ServiceType, ticket.PriorityType);
        }

        foreach (var ticket in _data.Tickets.Where(t => t.Status == "Now Serving"))
        {
            ticket.EstimatedWaitMinutes = 0;
            ticket.EstimatedWaitText = "Now serving";
            ticket.EstimatedServiceStartAt = ticket.CalledAt ?? now;
            ticket.PredictedCounterName = string.IsNullOrWhiteSpace(ticket.CounterName) ? ticket.PredictedCounterName : ticket.CounterName;
            ticket.BestVisitTime = recommendation;
            ticket.AiInsight = "Your ticket is being served now.";
        }

        foreach (var ticket in _data.Tickets.Where(t => t.Status == "Served"))
        {
            ticket.EstimatedWaitText = "Completed";
            ticket.BestVisitTime = recommendation;
            ticket.AiInsight = "This ticket has already been completed.";
        }

        foreach (var ticket in _data.Tickets.Where(t => t.Status == "Skipped"))
        {
            ticket.EstimatedWaitText = "Skipped";
            ticket.BestVisitTime = recommendation;
            ticket.AiInsight = "This ticket was skipped. Please approach staff for assistance.";
        }
    }

    private int EstimateServiceMinutes(string serviceType, string priorityType)
    {
        var service = (serviceType ?? string.Empty).Trim().ToLowerInvariant();
        var minutes = service switch
        {
            "general inquiry" => _data.Settings.GeneralInquiryMinutes,
            "payment" => _data.Settings.PaymentMinutes,
            "registration" => _data.Settings.RegistrationMinutes,
            "document request" => _data.Settings.DocumentRequestMinutes,
            "appointment" => _data.Settings.AppointmentMinutes,
            "customer service" => _data.Settings.CustomerServiceMinutes,
            _ => _data.Settings.DefaultServiceMinutes
        };

        if (string.Equals(priorityType, "Priority", StringComparison.OrdinalIgnoreCase))
        {
            minutes = Math.Max(1, minutes - 1);
        }

        return Math.Max(1, minutes);
    }

    private string RecommendBestVisitTimeLocked()
    {
        if (!_data.Settings.EnableAiRecommendations)
        {
            return "AI recommendation is disabled by admin.";
        }

        var now = DateTime.Now;
        var startHour = Math.Clamp(_data.Settings.BusinessStartHour, 0, 23);
        var endHour = Math.Clamp(_data.Settings.BusinessEndHour, startHour + 1, 24);
        var firstHour = Math.Max(startHour, now.Hour + (now.Minute > 15 ? 1 : 0));
        if (firstHour >= endHour) firstHour = startHour;

        var candidates = Enumerable.Range(firstHour, endHour - firstHour).ToList();
        if (!candidates.Any()) candidates = Enumerable.Range(startHour, endHour - startHour).ToList();

        var day = now.DayOfWeek;
        var currentWaiting = _data.Tickets.Count(t => t.Status == "Waiting");
        var activeCounters = Math.Max(1, _data.Counters.Count(c => !string.Equals(c.Status, "Offline", StringComparison.OrdinalIgnoreCase)));

        var bestHour = candidates
            .Select(hour => new
            {
                Hour = hour,
                Score = PredictHourlyLoad(day, hour, currentWaiting, activeCounters)
            })
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Hour)
            .First();

        var label = bestHour.Score <= _data.Settings.LowTrafficThreshold ? "low traffic" : "least busy available";
        return $"{FormatHour(bestHour.Hour)} - {FormatHour(Math.Min(bestHour.Hour + 1, 24))} ({label})";
    }

    private double PredictHourlyLoad(DayOfWeek day, int hour, int currentWaiting, int activeCounters)
    {
        var today = DateTime.Today;
        var historical = _data.Tickets
            .Where(t => t.CreatedAt.Date < today && t.CreatedAt.DayOfWeek == day && t.CreatedAt.Hour == hour)
            .ToList();

        var historicalAverage = historical.Any() ? historical.Count / Math.Max(1.0, historical.Select(t => t.CreatedAt.Date).Distinct().Count()) : 0.0;
        var todayCount = _data.Tickets.Count(t => t.CreatedAt.Date == today && t.CreatedAt.Hour == hour);
        var realtimePressure = hour == DateTime.Now.Hour ? currentWaiting / Math.Max(1.0, activeCounters) : 0.0;
        var commonPeakPenalty = ((hour >= 11 && hour <= 13) || (hour >= 15 && hour <= 16)) ? 1.5 : 0.0;
        var earlyQuietBonus = hour == _data.Settings.BusinessStartHour || hour == _data.Settings.BusinessStartHour + 1 ? -0.5 : 0.0;

        return Math.Max(0, historicalAverage + todayCount + realtimePressure + commonPeakPenalty + earlyQuietBonus);
    }

    private string GetQueueLoadLabelLocked()
    {
        var activeCounters = Math.Max(1, _data.Counters.Count(c => !string.Equals(c.Status, "Offline", StringComparison.OrdinalIgnoreCase)));
        var waiting = _data.Tickets.Count(t => t.Status == "Waiting");
        var ratio = waiting / (double)activeCounters;
        if (ratio <= _data.Settings.LowTrafficThreshold) return "Light queue";
        if (ratio <= _data.Settings.LowTrafficThreshold * 2) return "Moderate queue";
        return "Heavy queue";
    }

    private int GetPredictedAverageWaitLocked()
    {
        var waits = _data.Tickets.Where(t => t.Status == "Waiting").Select(t => t.EstimatedWaitMinutes).ToList();
        return waits.Any() ? (int)Math.Round(waits.Average()) : 0;
    }

    private string BuildAiInsight(Ticket ticket, int queueLength, int activeCounters)
    {
        var counter = string.IsNullOrWhiteSpace(ticket.PredictedCounterName) ? "the next available counter" : ticket.PredictedCounterName;
        return $"AI-assisted estimate based on {queueLength} waiting ticket(s), {activeCounters} active counter(s), transaction type, priority, and previous service-time patterns. Suggested counter: {counter}.";
    }

    private static string FormatWaitMinutes(int minutes)
    {
        if (minutes <= 0) return "Less than 1 minute";
        if (minutes == 1) return "1 minute";
        return $"{minutes} minutes";
    }

    private static string FormatHour(int hour)
    {
        if (hour >= 24) hour = 0;
        var dt = DateTime.Today.AddHours(hour);
        return dt.ToString("h:mm tt");
    }

    private static string NormalizeTicketKey(string value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static AccountNotification CloneNotification(AccountNotification n)
    {
        return new AccountNotification
        {
            Id = n.Id,
            UserId = n.UserId,
            TicketNumber = n.TicketNumber,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        };
    }

    private int? ResolveCustomerUserId(string phone, int? requestedUserId)
    {
        if (requestedUserId.HasValue)
        {
            var requested = _data.Users.FirstOrDefault(u => u.Id == requestedUserId.Value);
            if (requested != null) return requested.Id;
        }

        var byPhone = _data.Users
            .Where(u => u.Role == "Customer")
            .OrderByDescending(u => u.CreatedAt)
            .FirstOrDefault(u => NormalizePhone(u.PhoneNumber) == phone);
        return byPhone?.Id;
    }

    private void NotifyCustomer(Ticket ticket, string title, string message, string eventType)
    {
        if (!IsNotificationEventEnabled(eventType)) return;

        if (_data.Settings.EnableAccountNotifications && ticket.CustomerUserId.HasValue)
        {
            AddAccountNotification(ticket.CustomerUserId.Value, ticket.TicketNumber, title, message);
        }

        if (_data.Settings.EnablePhoneNotifications)
        {
            AddPhoneNotification(ticket.PhoneNumber, ticket.TicketNumber, message);
        }
    }

    private void NotifyCustomerService(Ticket ticket, string title, string message, string eventType)
    {
        if (!_data.Settings.EnableCsNotifications) return;
        if (eventType == "created" && !_data.Settings.NotifyCsOnTicketCreated) return;
        if (eventType == "skipped" && !_data.Settings.NotifyCsOnSkipped) return;

        foreach (var staff in _data.Users.Where(u => u.Role == "Admin" || u.Role == "Staff"))
        {
            AddAccountNotification(staff.Id, ticket.TicketNumber, title, message);
        }

        AddLog("CS notification", $"CS alert generated for {ticket.TicketNumber}: {title}.");
    }

    private bool IsNotificationEventEnabled(string eventType)
    {
        return eventType switch
        {
            "created" => _data.Settings.NotifyOnTicketCreated,
            "almost-next" => _data.Settings.NotifyOnAlmostNext,
            "serving" => _data.Settings.NotifyOnNowServing,
            "skipped" => _data.Settings.NotifyOnSkipped,
            "completed" => _data.Settings.NotifyOnCompleted,
            _ => true
        };
    }

    private void AddPhoneNotification(string phone, string ticketNumber, string message)
    {
        _data.SmsNotifications.Add(new SmsNotification
        {
            Id = NextId(_data.SmsNotifications.Select(s => s.Id)),
            PhoneNumber = phone,
            TicketNumber = ticketNumber,
            Message = message,
            Status = string.IsNullOrWhiteSpace(_data.Settings.SmsApiKey) ? "Logged" : $"Queued via {_data.Settings.SmsProviderName}",
            CreatedAt = DateTime.Now
        });
    }

    private void AddAccountNotification(int userId, string ticketNumber, string title, string message)
    {
        _data.AccountNotifications.Add(new AccountNotification
        {
            Id = NextId(_data.AccountNotifications.Select(s => s.Id)),
            UserId = userId,
            TicketNumber = ticketNumber,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.Now
        });
    }

    private void AddLog(string action, string details)
    {
        _data.ActivityLogs.Add(new ActivityLog
        {
            Id = NextId(_data.ActivityLogs.Select(l => l.Id)),
            Action = action,
            Details = details,
            CreatedAt = DateTime.Now
        });
    }

    private double GetAverageWaitingMinutes()
    {
        var completed = _data.Tickets
            .Where(t => t.CalledAt.HasValue)
            .Select(t => (t.CalledAt!.Value - t.CreatedAt).TotalMinutes)
            .Where(x => x >= 0)
            .ToList();

        if (!completed.Any()) return 0;
        return Math.Round(completed.Average(), 1);
    }

    private string GenerateTicketNumber(string service)
    {
        var prefix = new string((service ?? "GEN").Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "GEN";
        var today = DateTime.Today;
        var count = _data.Tickets.Count(t => t.CreatedAt.Date == today && t.TicketNumber.StartsWith(prefix)) + 1;
        return $"{prefix}-{count:000}";
    }

    private SmartQueueData LoadOrSeed()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<SmartQueueData>(json);
                if (loaded != null)
                {
                    loaded.Users ??= new List<User>();
                    loaded.Tickets ??= new List<Ticket>();
                    loaded.Counters ??= new List<Counter>();
                    loaded.SmsNotifications ??= new List<SmsNotification>();
                    loaded.AccountNotifications ??= new List<AccountNotification>();
                    loaded.ActivityLogs ??= new List<ActivityLog>();
                    loaded.Settings ??= new AppSettings();

                    var changed = false;
                    changed |= EnsureDefaultAdmin(loaded);
                    EnsureDefaultSettings(loaded.Settings);
                    EnsureDefaultCounters(loaded);

                    if (changed) SaveData(loaded);
                    return loaded;
                }
            }
            catch { }
        }

        var seeded = new SmartQueueData();
        EnsureDefaultAdmin(seeded);
        EnsureDefaultSettings(seeded.Settings);
        EnsureDefaultCounters(seeded);
        SaveData(seeded);
        return seeded;
    }

    private bool EnsureDefaultAdmin(SmartQueueData data)
    {
        if (data.Users.Any(u => u.Role == "Admin")) return false;

        var admin = BuildDefaultAdmin();
        admin.Id = NextId(data.Users.Select(u => u.Id));
        data.Users.Add(admin);
        data.ActivityLogs.Add(new ActivityLog
        {
            Id = NextId(data.ActivityLogs.Select(l => l.Id)),
            Action = "Default administrator ready",
            Details = "Built-in administrator account is available for system owner login. Credentials are not displayed in the website interface.",
            CreatedAt = DateTime.Now
        });
        return true;
    }

    private static void EnsureDefaultSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.EstablishmentName)) settings.EstablishmentName = "SmartQueueSystemGUI";
        if (string.IsNullOrWhiteSpace(settings.SmsProviderName)) settings.SmsProviderName = "Phone Gateway";
        if (settings.AlmostNextPosition < 1) settings.AlmostNextPosition = 1;
        if (string.IsNullOrWhiteSpace(settings.AccentTheme)) settings.AccentTheme = "Blue";
        if (settings.DefaultServiceMinutes < 1) settings.DefaultServiceMinutes = 5;
        if (settings.GeneralInquiryMinutes < 1) settings.GeneralInquiryMinutes = 3;
        if (settings.PaymentMinutes < 1) settings.PaymentMinutes = 4;
        if (settings.RegistrationMinutes < 1) settings.RegistrationMinutes = 7;
        if (settings.DocumentRequestMinutes < 1) settings.DocumentRequestMinutes = 6;
        if (settings.AppointmentMinutes < 1) settings.AppointmentMinutes = 8;
        if (settings.CustomerServiceMinutes < 1) settings.CustomerServiceMinutes = 6;
        if (settings.BusinessStartHour < 0 || settings.BusinessStartHour > 23) settings.BusinessStartHour = 8;
        if (settings.BusinessEndHour <= settings.BusinessStartHour || settings.BusinessEndHour > 24) settings.BusinessEndHour = 17;
        if (settings.LowTrafficThreshold < 0) settings.LowTrafficThreshold = 3;
    }

    private void EnsureDefaultCounters(SmartQueueData data)
    {
        if (data.Counters.Any()) return;
        data.Counters.AddRange(new[]
        {
            new Counter { Id = 1, Name = "Counter 1", StaffName = string.Empty, Status = "Available" },
            new Counter { Id = 2, Name = "Counter 2", StaffName = string.Empty, Status = "Available" },
            new Counter { Id = 3, Name = "Counter 3", StaffName = string.Empty, Status = "Offline" }
        });
    }

    private (bool ok, string message) ValidateUser(string fullName, string phone, string username, string password, string confirmPassword, string role)
    {
        phone = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(fullName)) return (false, "Full name is required.");
        if (!IsValidPhilippinePhone(phone)) return (false, "Please enter a valid 11-digit phone number that starts with 09.");
        if (string.IsNullOrWhiteSpace(username)) return (false, "Username is required.");
        if (string.IsNullOrWhiteSpace(password)) return (false, "Password is required.");
        if (password.Length < 6) return (false, "Password must be at least 6 characters.");
        if (password != confirmPassword) return (false, "Password and confirm password do not match.");
        if (!new[] { "Admin", "Staff", "Customer" }.Contains(role)) return (false, "Invalid user role.");
        if (_data.Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
            return (false, "Username already exists.");
        return (true, string.Empty);
    }

    private User BuildUser(string fullName, string phone, string username, string password, string role)
    {
        return new User
        {
            Id = NextId(_data.Users.Select(x => x.Id)),
            FullName = fullName.Trim(),
            PhoneNumber = NormalizePhone(phone),
            Username = username.Trim(),
            PasswordHash = HashPassword(password),
            Role = NormalizeRole(role),
            CreatedAt = DateTime.Now
        };
    }

    private static string NormalizeRole(string role)
    {
        return role == "Admin" || role == "Staff" || role == "Customer" ? role : "Customer";
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private void Save()
    {
        SaveData(_data);
    }

    private void SaveData(SmartQueueData data)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, options));
    }

    private static int NextId(IEnumerable<int> ids) => ids.Any() ? ids.Max() + 1 : 1;

    private static SmartQueueData Clone(SmartQueueData data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonSerializer.Deserialize<SmartQueueData>(json) ?? new SmartQueueData();
    }
}

namespace SmartQueueSystemGUI.Models;

public static class CampusServices
{
    public static readonly IReadOnlyDictionary<string, string[]> RequestTypes = new Dictionary<string, string[]>
    {
        ["Document Request"] = new[] { "Certificate of Enrollment", "Good Moral Certificate", "Transcript / Academic Record", "Certified True Copy", "Other Document" },
        ["School Fees"] = new[] { "Miscellaneous Fees", "School Activity Fee", "Document Processing Fee", "Other School Fee" },
        ["Tuition Payment"] = new[] { "Current Tuition Assessment", "Outstanding Tuition Balance", "Tuition Assessment Assistance" }
    };

    public static string OfficeFor(string service) => service switch
    {
        "Document Request" => "Registrar",
        "School Fees" or "Tuition Payment" or "Payment" => "Cashier",
        _ => "Campus Office"
    };
}

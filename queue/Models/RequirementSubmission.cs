namespace SmartQueueSystemGUI.Models;

// Stored only in the private queue data file and full, owner-only workspace backups.
public class RequirementSubmission
{
    public string Id { get; set; } = "";
    public string TicketNumber { get; set; } = "";
    public int RequirementId { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int Length { get; set; }
    public string ContentBase64 { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public string Status { get; set; } = "Pending review";
    public string ReviewNote { get; set; } = "";
    public DateTimeOffset? ReviewedAt { get; set; }
    public int? ReviewedBy { get; set; }
}

// Deliberately separate metadata from the stored file bytes.
public record SubmissionSummary(string Id, int RequirementId, int Version, string FileName,
    int Length, DateTimeOffset UploadedAt, string Status, string ReviewNote,
    DateTimeOffset? ReviewedAt);

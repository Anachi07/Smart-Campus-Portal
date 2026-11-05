using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SmartQueueSystemGUI.Models;

namespace SmartQueueSystemGUI.Services;

public partial class QueueStore
{
    public const int MaxSubmissionBytes = 2 * 1024 * 1024;
    public const int MaxStoredSubmissionBytes = 8 * 1024 * 1024;
    public const int MaxSubmissionVersions = 5;
    public static bool SubmissionsOpen(Ticket t) => t.ServiceType == "Document Request"
        && !ChecklistClosed(t) && t.DocumentStatus != "Ready for pickup"
        && !(t.DocumentStatus == "For review" && t.Status == "Skipped");

    private RequirementSubmission? LatestSubmission(string number, int requirementId) => _data.RequirementSubmissions
        .Where(s => s.TicketNumber == number && s.RequirementId == requirementId).MaxBy(s => s.Version);

    private static string SafeFileName(string name) => Regex.Replace(
        (name ?? "").Replace('\\', '/').Split('/').Last(), @"[^\p{L}\p{N} ._-]", "_").Trim();

    // These checks identify supported containers; they do not claim malware scanning.
    private static string? SubmissionType(string name, byte[] bytes)
    {
        if (bytes.Length < 16 || bytes.Length > MaxSubmissionBytes) return null;
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".pdf" && (bytes.AsSpan().StartsWith("%PDF-1."u8) || bytes.AsSpan().StartsWith("%PDF-2.0"u8))
            && Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 1024), Math.Min(1024, bytes.Length)).TrimEnd().EndsWith("%%EOF"))
            return "application/pdf";
        if (extension is ".jpg" or ".jpeg" && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff
            && bytes[^2] == 0xff && bytes[^1] == 0xd9) return "image/jpeg";
        if (extension == ".png" && bytes.Length >= 45 && bytes.AsSpan(0, 8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
            && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) && bytes.AsSpan(bytes.Length-12).SequenceEqual(new byte[]{0,0,0,0,73,69,78,68,174,66,96,130}))
            return "image/png";
        return null;
    }

    public List<SubmissionSummary> GetSubmissions(string number, int actor, bool staff)
    {
        lock (_lock)
        {
            if (!_data.Tickets.Any(t => t.TicketNumber == number && (staff || t.CustomerUserId == actor))) return new();
            return _data.RequirementSubmissions.Where(s => s.TicketNumber == number)
                .OrderByDescending(s => s.Version).ThenByDescending(s => s.UploadedAt)
                .Select(s => new SubmissionSummary(s.Id,s.RequirementId,s.Version,s.FileName,s.Length,s.UploadedAt,s.Status,s.ReviewNote,s.ReviewedAt)).ToList();
        }
    }

    public (bool ok, string message) SubmitRequirement(string number, int requirementId, int expectedVersion, int actor, string fileName, byte[] bytes)
    {
        lock (_lock)
        {
            if (DateTimeOffset.UtcNow < _reminderPauseUntil) return (false, "A workspace restore is in progress. Try again shortly.");
            var ticket = _data.Tickets.FirstOrDefault(t => t.TicketNumber == number && t.CustomerUserId == actor);
            if (ticket == null) return (false, "Request not found.");
            if (!SubmissionsOpen(ticket)) return (false, "Uploads are closed for this request.");
            var item = ticket.Checklist.FirstOrDefault(c => c.RequirementId == requirementId);
            if (item == null) return (false, "Choose a requirement listed on this request.");
            var latest = LatestSubmission(number, requirementId);
            if ((latest?.Version ?? 0) != expectedVersion) return (false, "A newer submission exists. Refresh the request before uploading again.");
            if ((latest?.Version ?? 0) >= MaxSubmissionVersions) return (false, "The five-version limit has been reached. Contact the Registrar for assistance.");
            fileName = SafeFileName(fileName);
            var type = SubmissionType(fileName, bytes);
            if (fileName.Length is < 5 or > 120 || type == null) return (false, "Upload a valid PDF, PNG, or JPEG file of up to 2 MB, with a filename of up to 120 characters.");
            if (_data.RequirementSubmissions.Count + _data.IssuedDocuments.Count >= 500 || _data.RequirementSubmissions.Sum(s => (long)s.Length) + _data.IssuedDocuments.Sum(d=>(long)d.Length) + bytes.Length > MaxStoredSubmissionBytes)
                return (false, "Demo upload storage is full. Contact the administrator; existing submissions remain available.");
            var before = Clone(_data);
            try
            {
                _data.RequirementSubmissions.Add(new() {Id=Guid.NewGuid().ToString("N"),TicketNumber=number,RequirementId=requirementId,
                    Version=expectedVersion+1,FileName=fileName,ContentType=type,Length=bytes.Length,ContentBase64=Convert.ToBase64String(bytes),
                    Sha256=Convert.ToHexString(SHA256.HashData(bytes)),UploadedAt=CampusNow});
                item.Prepared = true; item.Verified = false;
                AddLog("Requirement submitted", $"Requester #{actor} uploaded version {expectedVersion+1} for requirement #{requirementId}, {number}.");
                Save();
            }
            catch { _data = before; throw; }
            return (true, "File submitted privately for office review. Uploading does not approve the requirement.");
        }
    }

    public (bool ok, string message) ReviewSubmission(string number, string submissionId, int expectedVersion, string decision, string? note, int staffId)
    {
        lock (_lock)
        {
            if (DateTimeOffset.UtcNow < _reminderPauseUntil) return (false, "A workspace restore is in progress. Try again shortly.");
            if (!_data.Users.Any(u => u.Id == staffId && u.Role is "Admin" or "Staff")) return (false, "Office access is required.");
            var ticket = _data.Tickets.FirstOrDefault(t => t.TicketNumber == number);
            if (ticket == null || !SubmissionsOpen(ticket)) return (false, "Review is closed for this request.");
            var submission = _data.RequirementSubmissions.FirstOrDefault(s => s.Id == submissionId && s.TicketNumber == number);
            if (submission == null || submission.Version != expectedVersion || LatestSubmission(number, submission.RequirementId)?.Id != submission.Id
                || submission.Status != "Pending review") return (false, "This submission has changed. Refresh and review the latest pending version.");
            note = (note ?? "").Trim();
            if (decision is not ("accept" or "return") || note.Length > 600 || (decision == "return" && note.Length < 5))
                return (false, "Choose Accept or Return for correction. A return needs a reason of 5–600 characters.");
            var before = Clone(_data);
            try
            {
                submission.Status = decision == "accept" ? "Accepted" : "Returned for correction";
                submission.ReviewNote = note; submission.ReviewedAt = CampusNow; submission.ReviewedBy = staffId;
                ticket.Checklist.Single(c => c.RequirementId == submission.RequirementId).Verified = decision == "accept";
                if (ticket.CustomerUserId.HasValue) AddAccountNotification(ticket.CustomerUserId.Value,number,
                    decision == "accept" ? "Requirement accepted" : "Requirement needs correction",
                    "The Registrar reviewed a submitted requirement. Open your request to see the result and any correction instructions.");
                AddLog("Requirement reviewed", $"Staff #{staffId}: {number}, requirement #{submission.RequirementId}, version {submission.Version}: {submission.Status}.");
                Save();
            }
            catch { _data = before; throw; }
            return (true, decision == "accept" ? "Submission accepted and requirement verified." : "Submission returned. The requester can read the reason and upload a corrected version.");
        }
    }

    public (byte[] bytes, string type, string name)? DownloadSubmission(string submissionId, int actor, bool staff)
    {
        lock (_lock)
        {
            var submission = _data.RequirementSubmissions.FirstOrDefault(s => s.Id == submissionId);
            if (submission == null || !_data.Tickets.Any(t => t.TicketNumber == submission.TicketNumber && (staff || t.CustomerUserId == actor))) return null;
            var bytes = Convert.FromBase64String(submission.ContentBase64);
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(submission.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Stored submission failed its integrity check.");
            AddLog("Requirement downloaded", $"Account #{actor} downloaded {submission.TicketNumber}, requirement #{submission.RequirementId}, version {submission.Version}.");
            Save();
            return (bytes,submission.ContentType,submission.FileName);
        }
    }

    private static void ValidateSubmissionsBackup(SmartQueueData data)
    {
        if (data.RequirementSubmissions == null || data.RequirementSubmissions.Count > 500
            || data.RequirementSubmissions.Any(s => s == null) || data.RequirementSubmissions.Select(s => s.Id).Distinct().Count() != data.RequirementSubmissions.Count)
            throw new InvalidDataException("Invalid requirement submissions.");
        long total = 0;
        foreach (var s in data.RequirementSubmissions)
        {
            var ticket = data.Tickets.FirstOrDefault(t => t.TicketNumber == s.TicketNumber && t.ServiceType == "Document Request");
            if (ticket == null || ticket.Checklist.Count(c => c.RequirementId == s.RequirementId) != 1
                || !Guid.TryParseExact(s.Id,"N",out _) || s.Version < 1 || s.Version > MaxSubmissionVersions
                || s.FileName == null || s.FileName.Length is < 5 or > 120 || s.FileName != SafeFileName(s.FileName)
                || s.ContentBase64 == null || s.ContentBase64.Length > ((MaxSubmissionBytes+2)/3)*4
                || s.Status is not ("Pending review" or "Accepted" or "Returned for correction")
                || s.UploadedAt == default || s.ReviewedAt < s.UploadedAt
                || s.ReviewNote == null || s.ReviewNote.Length > 600
                || s.Status == "Pending review" && (s.ReviewedAt.HasValue || s.ReviewedBy.HasValue || s.ReviewNote.Length != 0)
                || s.Status != "Pending review" && (!s.ReviewedAt.HasValue || !s.ReviewedBy.HasValue)
                || s.Status == "Returned for correction" && s.ReviewNote.Trim().Length < 5)
                throw new InvalidDataException("Invalid requirement submission metadata.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(s.ContentBase64); }
            catch (FormatException) { throw new InvalidDataException("Invalid requirement file encoding."); }
            if (s.Length != bytes.Length || SubmissionType(s.FileName,bytes) != s.ContentType || SubmissionType(s.FileName,bytes) == null
                || Convert.ToHexString(SHA256.HashData(bytes)) != s.Sha256 || (total += bytes.Length) > MaxStoredSubmissionBytes)
                throw new InvalidDataException("Invalid requirement file or upload storage limit exceeded.");
        }
        foreach (var group in data.RequirementSubmissions.GroupBy(s => (s.TicketNumber,s.RequirementId)))
        {
            var versions = group.Select(s => s.Version).Order().ToArray();
            if (!versions.SequenceEqual(Enumerable.Range(1,versions.Length))) throw new InvalidDataException("Invalid submission version history.");
            var latest = group.MaxBy(s => s.Version)!;
            var ticket = data.Tickets.Single(t => t.TicketNumber == latest.TicketNumber);
            var item = ticket.Checklist.Single(c => c.RequirementId == latest.RequirementId);
            if ((item.Verified || item.Required && ticket.DocumentStatus is "Ready for pickup" or "Released") && latest.Status != "Accepted") throw new InvalidDataException("An unaccepted submission cannot be verified.");
        }
    }
}

using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SmartQueueSystemGUI.Models;
using SmartQueueSystemGUI.Services;
namespace SmartQueueSystemGUI.Controllers;

[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ServicesController(QueueStore store):Controller
{
    private int? UserId=>HttpContext.Session.GetInt32("UserId");
    private bool Staff=>HttpContext.Session.GetString("Role") is "Admin" or "Staff";
    private IActionResult? Gate(bool staff=false)=>!UserId.HasValue?Redirect("/sims/login.php"):staff&&!Staff?StatusCode(403):null;
    private Ticket? Visible(string id)=>store.Snapshot().Tickets.FirstOrDefault(t=>t.TicketNumber==id&&(Staff||t.CustomerUserId==UserId));
    private IActionResult Back(string id,(bool ok,string message) result) {TempData["ServiceMessage"]=result.message;return RedirectToAction("Request",new{id});}
    public IActionResult Requests(string? stage=null) {
        var gate=Gate();if(gate!=null)return gate;ViewBag.Staff=Staff;ViewBag.Stage=stage;
        var tickets=store.Snapshot().Tickets.Where(t=>Staff?t.ServiceType=="Document Request":t.CustomerUserId==UserId);
        if(!string.IsNullOrEmpty(stage))tickets=tickets.Where(t=>t.DocumentStatus==stage);
        var shown=tickets.OrderByDescending(t=>t.CreatedAt).Take(200).ToList();
        var numbers=shown.Select(t=>t.TicketNumber).ToHashSet();
        var latest=store.Snapshot().RequirementSubmissions.Where(s=>numbers.Contains(s.TicketNumber)).GroupBy(s=>new{s.TicketNumber,s.RequirementId}).Select(g=>g.MaxBy(s=>s.Version)!);
        ViewBag.SubmissionAttention=latest.GroupBy(s=>s.TicketNumber).ToDictionary(g=>g.Key,g=>Staff
            ? (g.Any(s=>s.Status=="Pending review")?"Files awaiting office review":g.Any(s=>s.Status=="Returned for correction")?"Awaiting requester correction":"")
            : (g.Any(s=>s.Status=="Returned for correction")?"Correction needed":g.Any(s=>s.Status=="Pending review")?"Files awaiting office review":""));
        return View(shown);
    }
    public new IActionResult Request(string id) {
        var gate=Gate();if(gate!=null)return gate;var ticket=Visible(id);if(ticket==null)return NotFound();
        return View(new RequestDetailViewModel{Ticket=ticket,Staff=Staff,Submissions=store.GetSubmissions(id,UserId!.Value,Staff),IssuedDocuments=store.IssuedDocuments(id,UserId.Value,Staff),Feedback=store.Snapshot().Feedback.FirstOrDefault(f=>f.TicketNumber==id),CanFeedback=ticket.CustomerUserId==UserId&&store.CanSubmitFeedback(ticket),Message=TempData["ServiceMessage"] as string});
    }
    [HttpGet]
    public IActionResult CampusOverview() {
        if(!UserId.HasValue)return Unauthorized();
        var data=store.Snapshot();var mine=data.Tickets.Where(t=>t.CustomerUserId==UserId).ToList();
        var selected=Staff?data.Tickets:mine;
        var numbers=selected.Select(t=>t.TicketNumber).ToHashSet();
        var latest=data.RequirementSubmissions.Where(s=>numbers.Contains(s.TicketNumber)).GroupBy(s=>new{s.TicketNumber,s.RequirementId}).Select(g=>g.MaxBy(s=>s.Version)!).ToList();
        var metrics=new List<object[]> {
            new object[]{Staff?"Documents in progress":"My active requests",Staff?selected.Count(t=>t.DocumentStatus is "For review" or "Processing"&&!QueueStore.ChecklistClosed(t)):mine.Count(t=>t.Status is "Scheduled" or "Waiting" or "Now Serving"),"/Services/Requests"},
            new object[]{"Ready for pickup",selected.Count(t=>t.DocumentStatus=="Ready for pickup"),"/Services/Requests?stage=Ready%20for%20pickup"},
            new object[]{Staff?"Files awaiting review":"Requirements needing correction",latest.Count(s=>s.Status==(Staff?"Pending review":"Returned for correction")),"/Services/Requests"},
            new object[]{"Unread office alerts",store.GetUnreadNotificationCount(UserId.Value),Staff?"/Admin/Notifications":"/Queue/Notifications"}
        };
        string Date(DateTime at)=>new DateTimeOffset(DateTime.SpecifyKind(at,DateTimeKind.Unspecified),TimeSpan.FromHours(8)).ToString("O");
        var timeline=mine.OrderByDescending(t=>t.CreatedAt).Take(30).SelectMany(t=>new[]{new{title=t.RequestType+" · Request received",at=Date(t.CreatedAt),url="/Services/Request?id="+Uri.EscapeDataString(t.TicketNumber)}}
            .Concat(t.DocumentHistory.Select(h=>new{title=t.RequestType+" · "+h.Status,at=h.At.ToString("O"),url="/Services/Request?id="+Uri.EscapeDataString(t.TicketNumber)}))).OrderByDescending(e=>e.at).Take(20);
        var visits=mine.Where(t=>t.Status=="Scheduled"&&t.ScheduledAt>=QueueStore.CampusNow).OrderBy(t=>t.ScheduledAt).Take(20).Select(t=>new{title=t.Office+" · "+t.RequestType,at=t.ScheduledAt!.Value.ToString("O"),url="/Services/Request?id="+Uri.EscapeDataString(t.TicketNumber)});
        var today=DateOnly.FromDateTime(QueueStore.CampusNow.DateTime);
        var closures=data.OfficeClosures.Where(c=>!c.Date.HasValue||c.Date>=today).Take(50).Select(c=>new{c.Office,c.Title,when=(c.Date?.ToString("yyyy-MM-dd")??(c.Weekday.HasValue?((DayOfWeek)c.Weekday.Value).ToString()+" weekly":"Every day"))+" · "+(c.AllDay?"All day":c.StartTime+"–"+c.EndTime)+" Manila"});
        return Json(new{metrics,timeline,visits,closures});
    }
    [HttpGet]
    public IActionResult LiveRequest(string id) {
        if(!UserId.HasValue)return Unauthorized();
        var ticket=Visible(id);if(ticket==null)return NotFound();
        return Json(new {
            ticket.Status, ticket.DocumentStatus,
            prepared=ticket.Checklist.Count(i=>i.Prepared),
            verified=ticket.Checklist.Count(i=>i.Verified),
            total=ticket.Checklist.Count,
            history=ticket.DocumentHistory.OrderByDescending(h=>h.At).Select(h=>new{h.Status,h.Note,at=h.At.ToString("MMM d, yyyy h:mm tt")}),
            submissionVersion=store.GetSubmissions(id,UserId!.Value,Staff).Select(s=>new{s.Id,s.Version,s.Status,s.ReviewNote}),
            canClaim=ticket.DocumentStatus is "Ready for pickup" or "Released"
        });
    }
    [HttpPost][ValidateAntiForgeryToken]
    [RequestSizeLimit(QueueStore.MaxSubmissionBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = QueueStore.MaxSubmissionBytes + 65536)]
    public async Task<IActionResult> UploadRequirement(string id, int requirementId, int expectedVersion, IFormFile? file) {
        var gate=Gate();if(gate!=null)return gate;
        var ticket=Visible(id);if(ticket==null||ticket.CustomerUserId!=UserId)return NotFound();
        if(file==null||file.Length==0||file.Length>QueueStore.MaxSubmissionBytes)
            return Back(id,(false,"Choose a PDF, PNG, or JPEG file of up to 2 MB."));
        using var memory=new MemoryStream();
        await file.CopyToAsync(memory,HttpContext.RequestAborted);
        return Back(id,store.SubmitRequirement(id,requirementId,expectedVersion,UserId!.Value,file.FileName,memory.ToArray()));
    }
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult ReviewRequirement(string id,string submissionId,int expectedVersion,string decision,string? note) {
        var gate=Gate(true);if(gate!=null)return gate;if(Visible(id)==null)return NotFound();
        return Back(id,store.ReviewSubmission(id,submissionId,expectedVersion,decision,note,UserId!.Value));
    }
    [HttpGet]
    public IActionResult RequirementFile(string submissionId) {
        if(!UserId.HasValue)return Unauthorized();
        var result=store.DownloadSubmission(submissionId,UserId.Value,Staff);if(result==null)return NotFound();
        Response.Headers["Content-Security-Policy"]="sandbox; default-src 'none'";
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(result.Value.bytes,result.Value.type,result.Value.name);
    }
    [HttpPost][ValidateAntiForgeryToken][RequestSizeLimit(QueueStore.MaxSubmissionBytes+65536)]
    [RequestFormLimits(MultipartBodyLengthLimit=QueueStore.MaxSubmissionBytes+65536)]
    public async Task<IActionResult> RegisterIssued(string id,IFormFile? file){var gate=Gate(true);if(gate!=null)return gate;if(Visible(id)==null)return NotFound();if(file==null||file.Length>QueueStore.MaxSubmissionBytes)return Back(id,(false,"Choose a PDF of up to 2 MB."));using var bytes=new MemoryStream();await file.CopyToAsync(bytes,HttpContext.RequestAborted);return Back(id,store.RegisterIssuedDocument(id,UserId!.Value,file.FileName,bytes.ToArray()));}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult RevokeIssued(string id,string documentId,string? note){var gate=Gate(true);if(gate!=null)return gate;if(!store.IssuedDocuments(id,UserId!.Value,true).Any(d=>d.Id==documentId))return NotFound();return Back(id,store.RevokeIssuedDocument(documentId,UserId.Value,note));}
    public IActionResult IssuedFile(string documentId){if(!UserId.HasValue)return Unauthorized();var doc=store.DownloadIssuedDocument(documentId,UserId.Value,Staff);if(doc==null)return NotFound();Response.Headers["Content-Security-Policy"]="sandbox; default-src 'none'";return File(doc.Value.bytes,"application/pdf",doc.Value.name);}
    public IActionResult IssuedQr(string id,string documentId){var gate=Gate();if(gate!=null)return gate;var doc=store.IssuedDocuments(id,UserId!.Value,Staff).FirstOrDefault(d=>d.Id==documentId);if(doc==null)return NotFound();var link=Url.Action("Document","Verify",new{code=doc.Code},base.Request.Scheme)!;using var generator=new QRCoder.QRCodeGenerator();using var qr=generator.CreateQrCode(link,QRCoder.QRCodeGenerator.ECCLevel.Q);return Content(new QRCoder.SvgQRCode(qr).GetGraphic(6,"#104b2b","#ffffff",true),"image/svg+xml");}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Prepared(string id,List<int>? prepared) {var gate=Gate();if(gate!=null)return gate;return Back(id,store.SetPrepared(id,UserId!.Value,prepared??new()));}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Verify(string id,List<int>? verified) {var gate=Gate(true);if(gate!=null)return gate;return Back(id,store.VerifyChecklist(id,verified??new(),UserId!.Value));}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Progress(string id,string stage,string? note) {var gate=Gate(true);if(gate!=null)return gate;return Back(id,store.AdvanceDocument(id,stage,note,UserId!.Value));}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult Feedback(string id,int rating,string? comment) {var gate=Gate();if(gate!=null)return gate;return Back(id,store.SubmitFeedback(id,UserId!.Value,rating,comment));}
    public IActionResult Claim(string id) {var gate=Gate();if(gate!=null)return gate;var ticket=Visible(id);if(ticket==null||ticket.DocumentStatus is not ("Ready for pickup" or "Released")||string.IsNullOrEmpty(ticket.ClaimCode))return NotFound();return View(ticket);}
    public IActionResult Requirements() {var gate=Gate(true);if(gate!=null)return gate;ViewBag.Message=TempData["ServiceMessage"];return View(store.Snapshot().ServiceRequirements.OrderBy(r=>r.ServiceType).ThenBy(r=>r.RequestType).ToList());}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult SaveRequirement(ServiceRequirement input) {var gate=Gate(true);if(gate!=null)return gate;if(!ModelState.IsValid){TempData["ServiceMessage"]="Check the requirement fields and try again.";return RedirectToAction("Requirements");}var result=store.SaveRequirement(input,UserId!.Value);TempData["ServiceMessage"]=result.message;return RedirectToAction("Requirements");}
    [HttpGet]
    public IActionResult Checklist(string serviceType,string requestType) {var gate=Gate();if(gate!=null)return gate;return Json(new{requirements=store.Requirements(serviceType,requestType),notice="School-managed preparation guidance. The office verifies requirements. If none are listed, contact the office; this does not mean no requirements apply."});}
    public IActionResult OfficeCalendar() {var gate=Gate();if(gate!=null)return gate;var data=store.Snapshot();ViewBag.Staff=Staff;ViewBag.Message=TempData["ServiceMessage"];ViewBag.Settings=data.Settings;return View(data.OfficeClosures.OrderBy(c=>c.Date??DateOnly.MaxValue).ThenBy(c=>c.Weekday).ToList());}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult AddClosure(ClosureInput input) {var gate=Gate(true);if(gate!=null)return gate;if(!ModelState.IsValid){TempData["ServiceMessage"]="Check the date, weekday, and time fields. No closure was added.";return RedirectToAction("OfficeCalendar");}var result=store.SaveClosure(input,UserId!.Value);TempData["ServiceMessage"]=result.message;return RedirectToAction("OfficeCalendar");}
    [HttpPost][ValidateAntiForgeryToken]
    public IActionResult RemoveClosure(int id) {var gate=Gate(true);if(gate!=null)return gate;TempData["ServiceMessage"]=store.RemoveClosure(id,UserId!.Value)?"Office closure removed.":"Closure not found.";return RedirectToAction("OfficeCalendar");}
    public IActionResult Report(string? from,string? to,string office="All Services",string? export=null) {
        var gate=Gate(true);if(gate!=null)return gate;
        var today=DateOnly.FromDateTime(QueueStore.CampusNow.DateTime);
        var first=string.IsNullOrEmpty(from)?today.AddDays(-29):DateOnly.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsedFrom)?parsedFrom:DateOnly.MinValue;
        var last=string.IsNullOrEmpty(to)?today:DateOnly.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsedTo)?parsedTo:DateOnly.MinValue;
        if(first==DateOnly.MinValue||last==DateOnly.MinValue||first>last||last.DayNumber-first.DayNumber>366||!QueueStore.Offices.Contains(office))return BadRequest("Choose an office and a date range of up to 367 days.");
        var report=store.GetOperationsReport(first,last,office);
        if(export=="csv") {
            static string Cell(string value)=>"\""+((value.Length>0&&"=+-@\t\r\n".Contains(value[0]))?"'":"")+value.Replace("\"","\"\"")+"\"";
            var csv=new StringBuilder("Metric,Value\r\n");void Row(string label,string value)=>csv.Append(Cell(label)).Append(',').Append(Cell(value)).Append("\r\n");
            Row("Requests created from",first.ToString("yyyy-MM-dd"));Row("Requests created to",last.ToString("yyyy-MM-dd"));Row("Office",office);Row("Total requests",report.Requests.ToString());Row("Completed queue visits",report.CompletedVisits.ToString());Row("Average wait minutes",report.AverageWaitMinutes?.ToString(CultureInfo.InvariantCulture)??"");Row("Wait samples",report.WaitSamples.ToString());Row("Released documents",report.ReleasedDocuments.ToString());Row("Feedback count",report.FeedbackCount.ToString());Row("Average rating",report.AverageRating?.ToString(CultureInfo.InvariantCulture)??"");foreach(var item in report.Transactions)Row("Transaction: "+item.Label,item.Count.ToString());foreach(var item in report.QueueStatuses)Row("Queue: "+item.Label,item.Count.ToString());foreach(var item in report.DocumentStatuses)Row("Document: "+item.Label,item.Count.ToString());
            return File(Encoding.UTF8.GetBytes(csv.ToString()),"text/csv","icas-service-summary.csv");
        }
        return View(report);
    }
}

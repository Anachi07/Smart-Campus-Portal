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
        return View(tickets.OrderByDescending(t=>t.CreatedAt).Take(200).ToList());
    }
    public new IActionResult Request(string id) {
        var gate=Gate();if(gate!=null)return gate;var ticket=Visible(id);if(ticket==null)return NotFound();
        return View(new RequestDetailViewModel{Ticket=ticket,Staff=Staff,Feedback=store.Snapshot().Feedback.FirstOrDefault(f=>f.TicketNumber==id),CanFeedback=ticket.CustomerUserId==UserId&&store.CanSubmitFeedback(ticket),Message=TempData["ServiceMessage"] as string});
    }
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

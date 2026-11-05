using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SmartQueueSystemGUI.Models;
using SmartQueueSystemGUI.Services;

static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
Environment.SetEnvironmentVariable("TZ","Asia/Manila");TimeZoneInfo.ClearCachedData();
var originalKey=Environment.GetEnvironmentVariable("CAMPUS_AI_API_KEY");var originalProvider=Environment.GetEnvironmentVariable("CAMPUS_AI_PROVIDER");var originalModel=Environment.GetEnvironmentVariable("CAMPUS_AI_MODEL");
try {
 var fake=new FakeClients();var assistant=new SchoolAssistantService(fake);
 var facts=new SchoolKnowledge("ICAS Philippines",new(){new SchoolFaq(7,"How do I request a school document?","The Registrar handles school document requests.","School-approved Registrar guide")});
 var question=new ChatRequest{Message="How do I request a document?"};
 Environment.SetEnvironmentVariable("CAMPUS_AI_API_KEY",null);Environment.SetEnvironmentVariable("CAMPUS_AI_MODEL",null);
 var reply=await assistant.Answer(question,facts,default);Assert(reply.Mode=="guide"&&fake.Calls==0,"Unconfigured AI must use guide without remote calls");
 var key="test-only-"+Guid.NewGuid().ToString("N");Environment.SetEnvironmentVariable("CAMPUS_AI_API_KEY",key);Environment.SetEnvironmentVariable("CAMPUS_AI_MODEL","contract-test-model");
 foreach(var provider in new[]{"gemini","openai"}) {
  Environment.SetEnvironmentVariable("CAMPUS_AI_PROVIDER",provider);fake.Provider=provider;fake.ModelText="{\"answer\":\"The Registrar handles document requests.\",\"sourceIds\":[7]}";
  reply=await assistant.Answer(question,facts,default);Assert(reply.Mode=="ai"&&reply.Sources.Single().Id==7,"Valid cited provider answer should be accepted");
  Assert(fake.Url!.Host==(provider=="gemini"?"generativelanguage.googleapis.com":"api.openai.com"),"Provider destination mismatch");
  Assert(!fake.Body!.Contains(key)&&fake.Body.Contains("School-approved Registrar guide"),"Provider key must never be put in prompt; published context required");
  Assert(fake.HasCredential,"Provider authentication header missing");
 }
 fake.ModelText="{\"answer\":\"Invented policy\",\"sourceIds\":[999]}";reply=await assistant.Answer(question,facts,default);Assert(reply.Mode=="guide","Unknown source ids must fall back");
 fake.ModelText="{\"answer\":\"Your visit is booked.\",\"sourceIds\":[7]}";reply=await assistant.Answer(question,facts,default);Assert(reply.Mode=="guide","Model cannot claim it booked a visit");
 fake.ModelText="{\"answer\":\"Tuition is 12345 pesos.\",\"sourceIds\":[7]}";reply=await assistant.Answer(question,facts,default);Assert(reply.Mode=="guide","Numbers not present in approved facts must be rejected");
 var beforeSecret=fake.Calls;reply=await assistant.Answer(new(){Message="my password is private-fixture"},facts,default);Assert(reply.Mode=="guide"&&fake.Calls==beforeSecret&&!reply.Answer.Contains("private-fixture"),"Secret-like input must not be forwarded or echoed");
 fake.ModelText="not valid JSON";reply=await assistant.Answer(question,facts,default);Assert(reply.Mode=="guide","Malformed provider output must fall back");
 var before=fake.Calls;reply=await assistant.Answer(new(){Message="What is the school's unlisted address?"},facts,default);Assert(reply.Sources.Count==0&&fake.Calls==before,"Unpublished facts must not be invented or sent to model");
 reply=await assistant.Answer(new(){Message="Magpa schedule ako ng tuition"},facts,default);Assert(reply.SuggestScheduling&&reply.Mode=="workflow"&&fake.Calls==before,"Booking intent must use confirmation workflow without model mutation");
 Console.WriteLine("PASS Gemini/OpenAI transport contracts, key isolation, citations, safe fallback, and no model bookings (mocked providers)");
} finally {Environment.SetEnvironmentVariable("CAMPUS_AI_API_KEY",originalKey);Environment.SetEnvironmentVariable("CAMPUS_AI_PROVIDER",originalProvider);Environment.SetEnvironmentVariable("CAMPUS_AI_MODEL",originalModel);}

var folder=Path.Combine(Path.GetTempPath(),"icas-assistant-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var oldData=Environment.GetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR");
try {
 Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",folder);var now=QueueStore.CampusNow;
 var data=new SmartQueueData{Users=new(){new(){Id=1,Username="qa-admin",Role="Admin"},new(){Id=2,Username="qa-two",Role="Customer"},new(){Id=3,Username="qa-three",Role="Customer"}},Counters=new(){new(){Id=1,Name="Registrar QA",Office="Registrar",Status="Available"}},Settings=new(){BookingDays=new(){0,1,2,3,4,5,6},MaxBookingsPerSlot=1},Tickets=new(){new(){Id=1,TicketNumber="QA-CROSSDAY",CustomerUserId=2,ServiceType="Document Request",RequestType="Good Moral Certificate",StudentNumber="QA-002",Status="Scheduled",ScheduledAt=now.AddMinutes(10),CreatedAt=now.DateTime.AddDays(-1)}}};
 File.WriteAllText(Path.Combine(folder,"smartqueue-data.json"),JsonSerializer.Serialize(data));var store=new QueueStore(new FakeHost{ContentRootPath=folder});
 Assert(!store.CheckInVisit("QA-CROSSDAY",3).ok,"Other users cannot check in a booking");Assert(store.CheckInVisit("QA-CROSSDAY",2).ok,"Own check-in should open 15 minutes early");
 Assert(store.Snapshot().Tickets.Single(t=>t.TicketNumber=="QA-CROSSDAY").Status=="Waiting","Yesterday's issue date must not expire today's checked-in appointment");
 var date=DateOnly.FromDateTime(now.AddDays(1).DateTime);var chosen=store.GetBookingSlots("Document Request",date,2,new()).First();
 var request=new AppointmentRequest{Confirmed=true,IdempotencyKey=Guid.NewGuid().ToString(),ServiceType="Document Request",RequestType="Certificate of Enrollment",StudentNumber="QA-002",PhoneNumber="09171111111",StartsAt=chosen.StartsAt.ToString("O")};
 var first=store.BookVisit(request,2,"QA Two",new());Assert(first.ok&&first.ticket!.Status=="Scheduled","Confirmed booking must create a scheduled request");
 var repeat=store.BookVisit(request,2,"QA Two",new());Assert(repeat.ok&&repeat.ticket!.TicketNumber==first.ticket!.TicketNumber,"Confirmation must be idempotent");
 var competing=new AppointmentRequest{Confirmed=true,IdempotencyKey=Guid.NewGuid().ToString(),ServiceType=request.ServiceType,RequestType=request.RequestType,StudentNumber="QA-003",PhoneNumber="09172222222",StartsAt=request.StartsAt};
 Assert(!store.BookVisit(competing,3,"QA Three",new()).ok,"A full slot must not be overbooked");
 Assert(!store.CancelVisit(first.ticket!.TicketNumber,3).ok,"Other users cannot cancel");Assert(!store.CheckInVisit(first.ticket.TicketNumber,2).ok,"Check-in must be denied before the arrival window");
 Assert(store.CancelVisit(first.ticket.TicketNumber,2).ok,"Owner can cancel");Assert(store.BookVisit(competing,3,"QA Three",new()).ok,"Cancellation must release capacity");
 var classDay=date.AddDays(1);var classes=new List<CampusClass>{new(classDay.DayOfWeek.ToString(),"09:00","11:00")};
 var offered=store.GetBookingSlots("Document Request",classDay,2,classes);Assert(offered.Any()&&!offered.Any(s=>s.StartsAt.Hour>=9&&s.StartsAt.Hour<=10),"Class conflicts must be excluded");
 var unconfirmed=new AppointmentRequest{Confirmed=false};Assert(!store.BookVisit(unconfirmed,2,"QA",new()).ok,"No booking without explicit confirmation");
 Console.WriteLine("PASS booking ownership, cross-day check-in, capacity, idempotency, cancellation, timetable conflicts, and explicit confirmation");
} finally {Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",oldData);Directory.Delete(folder,true);}


var exchangeFolder=Path.Combine(Path.GetTempPath(),"icas-exchange-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(exchangeFolder);
try {
 Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",exchangeFolder);
 var seed=new SmartQueueData{Users=Enumerable.Range(2,3).Select(id=>new User{Id=id,Username=$"campus:{id}",Role="Customer"}).ToList(),Counters=new(){new(){Id=1,Name="Registrar",Office="Registrar",Status="Available"}},Settings=new(){BookingDays=new(){0,1,2,3,4,5,6},MaxBookingsPerSlot=2}};
 File.WriteAllText(Path.Combine(exchangeFolder,"smartqueue-data.json"),JsonSerializer.Serialize(seed));var store=new QueueStore(new FakeHost{ContentRootPath=exchangeFolder});
 var date=DateOnly.FromDateTime(QueueStore.CampusNow.AddDays(2).DateTime);var slots=store.GetBookingSlots("Document Request",date,2,new());var source=slots[0].StartsAt;var desired=slots[3].StartsAt;
 Ticket Book(int actor,DateTimeOffset at){var result=store.BookVisit(new(){Confirmed=true,IdempotencyKey=Guid.NewGuid().ToString(),ServiceType="Document Request",RequestType="Good Moral Certificate",StudentNumber=$"QA-{actor}",PhoneNumber="09171111111",StartsAt=at.ToString("O")},actor,"QA",new());Assert(result.ok,result.message);return result.ticket!;}
 var first=Book(2,source);var second=Book(3,desired);var third=Book(4,desired);
 var eligibility=Enumerable.Range(2,3).ToDictionary(id=>id,id=>new VisitEligibility(id,true,new()));
 ChangeVisitRequest Offer()=>new(){Confirmed=true,TicketNumber=first.TicketNumber,ExpectedStartsAt=source.ToString("O"),StartsAt=desired.ToString("O")};
 var conflicting=new Dictionary<int,VisitEligibility>(eligibility){[2]=new(2,true,new(){new(date.DayOfWeek.ToString(),"00:00","23:59")})};
 Assert(!store.RequestExchange(Offer(),2,conflicting).ok,"Request must reject requester's class conflicts");
 Assert(store.RequestExchange(Offer(),2,eligibility).ok,"Both target holders should be invited");var invite=store.GetExchanges(3).Single();
 Assert(store.GetExchanges(4).Single().Id==invite.Id,"A shared full slot must use one exchange offer");
 var changedClasses=new Dictionary<int,VisitEligibility>(eligibility){[2]=new(2,true,new(){new(date.DayOfWeek.ToString(),"00:00","23:59")})};
 var accept=new SwapAction{Id=invite.Id,Decision="accept",Confirmed=true};
 Assert(!store.RespondExchange(accept,3,changedClasses).ok,"Acceptance must recheck requester's current classes");
 Assert(store.Snapshot().Tickets.Single(t=>t.Id==first.Id).ScheduledAt==source,"Class conflict must leave both visits untouched");
 Assert(store.RequestExchange(Offer(),2,eligibility).ok,"Closed exchange can be retried after schedule issue resolves");invite=store.GetExchanges(3).First(e=>e.CanRespond);accept.Id=invite.Id;
 var results=await Task.WhenAll(Task.Run(()=>store.RespondExchange(accept,3,eligibility)),Task.Run(()=>store.RespondExchange(accept,4,eligibility)));
 Assert(results.Count(r=>r.ok)==1,"Only one simultaneous acceptance can win");
 var after=store.Snapshot();Assert(after.Tickets.Count(t=>t.ScheduledAt==desired)==2&&after.Tickets.Count(t=>t.ScheduledAt==source)==1,"Atomic swap must preserve office capacity");
 Assert(after.Tickets.Single(t=>t.Id==first.Id).ScheduledAt==desired,"Requester must receive the accepted time");
 var loser=after.Tickets.Single(t=>t.CustomerUserId!=2&&t.ScheduledAt==desired);Assert(store.GetExchanges(loser.CustomerUserId!.Value).First().Status=="Closed","Other invitees must see exchange closed without their reservation changing");
 var one=store.GetBookingSlots("Document Request",date,2,new()).Last().StartsAt;
 Assert(!store.RescheduleVisit(new(){TicketNumber=first.TicketNumber,ExpectedStartsAt=desired.ToString("O"),StartsAt=one.ToString("O"),Confirmed=false},2,new()).ok,"Reschedule requires explicit consent");
 Assert(store.RescheduleVisit(new(){TicketNumber=first.TicketNumber,ExpectedStartsAt=desired.ToString("O"),StartsAt=one.ToString("O"),Confirmed=true},2,new()).ok,"Confirmed available reschedule must work");
 Console.WriteLine("PASS exchange class revalidation, simultaneous acceptance, atomic capacity, closed competing invitations, and confirmed rescheduling");
} finally {Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",oldData);Directory.Delete(exchangeFolder,true);}


var operationsFolder=Path.Combine(Path.GetTempPath(),"icas-operations-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(operationsFolder);
try {
 Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",operationsFolder);
 var seed=new SmartQueueData{Users=new(){new(){Id=2,Username="campus:2",Role="Customer"},new(){Id=3,Username="campus:3",Role="Customer"}},Counters=new(){new(){Id=1,Name="Registrar",Office="Registrar",Status="Available"},new(){Id=2,Name="Cashier",Office="Cashier",Status="Available"}},Settings=new(){BookingDays=new(){0,1,2,3,4,5,6},MaxBookingsPerSlot=1}};
 var file=Path.Combine(operationsFolder,"smartqueue-data.json");File.WriteAllText(file,JsonSerializer.Serialize(seed));var store=new QueueStore(new FakeHost{ContentRootPath=operationsFolder});
 Assert(store.SaveRequirement(new(){ServiceType="Document Request",RequestType="Other Document",Title="School-approved checklist fixture",Instructions="Test only",Required=true},1).ok,"Approved requirement definition should save");
 var doc=store.CreateTicket("QA","09171111111","Document Request","Regular",2,"Other Document","QA-2").ticket!;
 Assert(doc.DocumentStatus=="For review"&&doc.Checklist.Count==1,"New document must have review state and requirement snapshot");
 var requirement=doc.Checklist.Single().RequirementId;
 Assert(store.SetPrepared(doc.TicketNumber,2,new(){requirement}).ok&&!store.Snapshot().Tickets.Single(t=>t.Id==doc.Id).Checklist.Single().Verified,"Student preparation must not count as verification");
 Assert(!store.SetPrepared(doc.TicketNumber,3,new(){requirement}).ok,"Other accounts cannot change checklist");
 Assert(store.AdvanceDocument(doc.TicketNumber,"Processing","Review complete",1).ok,"Document should advance in order");
 Assert(!store.AdvanceDocument(doc.TicketNumber,"Ready for pickup","",1).ok,"Unverified required items must block readiness");
 Assert(store.VerifyChecklist(doc.TicketNumber,new(){requirement},1).ok,"Staff can verify requirements");
 Assert(store.AdvanceDocument(doc.TicketNumber,"Ready for pickup","Collect at the Registrar",1).ok,"Verified document can be marked ready");
 Assert(!store.AdvanceDocument(doc.TicketNumber,"Released","",1).ok,"Release requires identity-check confirmation note");
 Assert(store.AdvanceDocument(doc.TicketNumber,"Released","Recipient identity checked in person",1).ok,"Staff can confirm actual release");
 var released=store.Snapshot().Tickets.Single(t=>t.Id==doc.Id);Assert(!string.IsNullOrEmpty(released.ClaimCode)&&released.DocumentHistory.Count==4&&released.Status=="Served"&&released.CalledAt==null,"Confirmed release must finish the active queue visit without fabricating a counter-call time");
 Assert(!store.SubmitFeedback(doc.TicketNumber,3,5,"Wrong owner").ok,"Feedback requires ownership");Assert(!store.SubmitFeedback(doc.TicketNumber,2,6,"").ok,"Rating bounds must be enforced");Assert(store.SubmitFeedback(doc.TicketNumber,2,5,"Good service").ok&&!store.SubmitFeedback(doc.TicketNumber,2,4,"Again").ok,"One feedback response per completed transaction");
 Assert(store.SaveRequirement(new(){Id=requirement,ServiceType="Document Request",RequestType="Other Document",Title="Changed for future requests",Required=true},1).ok,"Requirement can be updated");Assert(store.Snapshot().Tickets.Single(t=>t.Id==doc.Id).Checklist.Single().Title=="School-approved checklist fixture","Existing checklist snapshot must not be rewritten");
 var date=DateOnly.FromDateTime(QueueStore.CampusNow.AddDays(2).DateTime);var first=store.GetBookingSlots("Document Request",date,3,new()).First();
 var reserved=store.BookVisit(new(){Confirmed=true,IdempotencyKey=Guid.NewGuid().ToString(),ServiceType="Document Request",RequestType="Certificate of Enrollment",StudentNumber="QA-3",PhoneNumber="09172222222",StartsAt=first.StartsAt.ToString("O")},3,"QA3",new()).ticket!;
 store.ProcessReminders(first.StartsAt.AddMinutes(-20));store.ProcessReminders(first.StartsAt.AddMinutes(-10));Assert(store.GetAccountNotifications(3).Count(n=>n.Title=="Visit reminder"&&n.TicketNumber==reserved.TicketNumber)==1,"Appointment reminders must be deduplicated per reserved time");
 Assert(store.SaveClosure(new(){Mode="Date",Office="Registrar",Date=date,AllDay=true,Title="QA holiday"},1).ok,"Office holiday should save");Assert(!store.GetBookingSlots("Document Request",date,3,new()).Any(),"Holiday must block appointment slots");Assert(store.GetBookingSlots("Tuition Payment",date,3,new()).Any(),"Registrar closure must not close Cashier");Assert(store.GetAccountNotifications(3).Any(n=>n.Title=="Office availability changed"),"Affected existing reservations need a closure notice");
 Assert(store.Snapshot().Tickets.Single(t=>t.Id==reserved.Id).Status=="Scheduled","Closure must preserve the existing reservation");
 store.RemoveClosure(store.Snapshot().OfficeClosures.Single().Id,1);
 Assert(store.SaveClosure(new(){Mode="Weekly",Office="All Services",Weekday=(int)date.DayOfWeek,StartTime="12:00",EndTime="13:00",Title="QA lunch"},1).ok,"Recurring lunch should save");Assert(!store.GetBookingSlots("Document Request",date,3,new()).Any(s=>s.StartsAt.Hour==12),"Lunch break must exclude overlapping slots");
 Assert(!store.SaveClosure(new(){Mode="Date",Date=date,Office="Registrar",StartTime="14:00",EndTime="13:00",Title="Invalid"},1).ok,"Invalid closure ranges must fail");
 var report=store.GetOperationsReport(DateOnly.FromDateTime(QueueStore.CampusNow.Date),DateOnly.FromDateTime(QueueStore.CampusNow.Date),"Registrar");Assert(report.ReleasedDocuments==1&&report.FeedbackCount==1&&report.AverageRating==5&&report.AverageWaitMinutes==null,"Reports must not count uncalled tickets as zero-minute waits");
 QueueStore.ValidateBackup(JsonSerializer.Serialize(store.Snapshot()));store.Reload();Assert(store.Snapshot().Feedback.Count==1&&store.Snapshot().Tickets.Single(t=>t.Id==doc.Id).ClaimCode==released.ClaimCode,"Document, calendar, feedback, and checklist data must survive reload");

 var fixture=store.Snapshot();var actual=QueueStore.CampusNow;
 fixture.Tickets.Add(new(){Id=100,TicketNumber="QA-REAL-WAIT",CustomerUserId=3,ServiceType="Tuition Payment",RequestType="Current Tuition Assessment",Status="Served",CreatedAt=actual.DateTime.AddDays(-2),ScheduledAt=actual.AddMinutes(-20),CheckedInAt=actual.DateTime.AddMinutes(-20),CalledAt=actual.DateTime.AddMinutes(-10),CompletedAt=actual.DateTime.AddMinutes(-5)});
 fixture.Tickets.Add(new(){Id=101,TicketNumber="QA-LATE-CLOSURE",CustomerUserId=3,ServiceType="Tuition Payment",RequestType="Tuition Assessment Assistance",Status="Scheduled",CreatedAt=actual.DateTime,ScheduledAt=actual.AddMinutes(10)});
 File.WriteAllText(file,JsonSerializer.Serialize(fixture));store.Reload();
 var actualReport=store.GetOperationsReport(DateOnly.FromDateTime(actual.Date).AddDays(-2),DateOnly.FromDateTime(actual.Date),"Cashier");Assert(actualReport.WaitSamples==1&&actualReport.AverageWaitMinutes==10,"Actual wait must start at check-in, not days-old booking creation");
 Assert(store.SaveClosure(new(){Mode="Date",Office="Cashier",Date=DateOnly.FromDateTime(actual.Date),AllDay=true,Title="Last-minute closure"},1).ok,"Late closure should save");
 var affected=store.Snapshot().Tickets.Single(t=>t.TicketNumber=="QA-LATE-CLOSURE");Assert(store.CanChangeVisit(affected),"Closure must allow an affected visit to move even within the usual one-hour deadline");Assert(!store.CheckInVisit(affected.TicketNumber,3).ok,"Closed-office visits cannot check in");
 var alternative=store.GetChangeSlots(affected.TicketNumber,3,new()).First();Assert(store.RescheduleVisit(new(){TicketNumber=affected.TicketNumber,ExpectedStartsAt=affected.ScheduledAt!.Value.ToString("O"),StartsAt=alternative.StartsAt.ToString("O"),Confirmed=true},3,new()).ok,"Closure exception must still recheck the new slot");
 var continuing=store.CreateTicket("QA","09172222222","Document Request","Regular",3,"Other Document","QA-3").ticket!;Assert(store.AdvanceDocument(continuing.TicketNumber,"Processing","Started",1).ok,"Office can start processing");store.CancelVisit(continuing.TicketNumber,3);Assert(!QueueStore.ChecklistClosed(store.Snapshot().Tickets.Single(t=>t.Id==continuing.Id)),"Cancelling a queue visit must not erase processing already started by the Registrar");
 Console.WriteLine("PASS document lifecycle, immutable requirements, owner checklist, verified release, feedback, calendar exclusions, deduplicated reminders, reports, and reload");
}finally{Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",oldData);Directory.Delete(operationsFolder,true);}

var submissionFolder=Path.Combine(Path.GetTempPath(),"icas-submission-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(submissionFolder);
try {
 Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",submissionFolder);
 var seed=new SmartQueueData{Users=new(){new(){Id=1,Username="admin",Role="Admin"},new(){Id=2,Username="campus:2",Role="Customer"},new(){Id=3,Username="campus:3",Role="Customer"}}};
 var file=Path.Combine(submissionFolder,"smartqueue-data.json");File.WriteAllText(file,JsonSerializer.Serialize(seed));var store=new QueueStore(new FakeHost{ContentRootPath=submissionFolder});
 Assert(store.SaveRequirement(new(){RequestType="Other Document",Title="QA submission",Required=true},1).ok,"Requirement must save");
 var requirement=store.Requirements("Document Request","Other Document").Single().Id;
 var number=store.CreateTicket("QA","09171111111","Document Request","Regular",2,"Other Document","QA-2").ticket!.TicketNumber;
 var pdf=Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n%%EOF\n");
 Assert(!store.SubmitRequirement(number,requirement,0,3,"private.pdf",pdf).ok,"Other accounts cannot upload");
 Assert(!store.SubmitRequirement(number,999,0,2,"private.pdf",pdf).ok,"Unknown checklist item cannot receive uploads");
 Assert(!store.SubmitRequirement(number,requirement,0,2,"fake.png",pdf).ok,"Extension and signature must agree");
 Assert(!store.SubmitRequirement(number,requirement,0,2,"file.pdf",new byte[QueueStore.MaxSubmissionBytes+1]).ok,"Oversize file must fail");
 Assert(store.SubmitRequirement(number,requirement,0,2,"../../private.pdf",pdf).ok,"Owner can submit a valid PDF");
 var first=store.GetSubmissions(number,2,false).Single();Assert(first.FileName=="private.pdf"&&first.Version==1,"Filenames must be sanitized");
 Assert(!store.GetSubmissions(number,3,false).Any()&&store.DownloadSubmission(first.Id,3,false)==null,"Metadata and bytes remain private");
 Assert(store.DownloadSubmission(first.Id,2,false)!.Value.bytes.SequenceEqual(pdf),"Owner download must match original bytes");
 Assert(!store.VerifyChecklist(number,new(){requirement},1).ok,"Manual verification cannot bypass pending upload review");
 Assert(!store.ReviewSubmission(number,first.Id,1,"accept","",2).ok,"Requester cannot review their submission");
 Assert(!store.ReviewSubmission(number,first.Id,1,"return","",1).ok,"Correction reason is mandatory");
 Assert(store.ReviewSubmission(number,first.Id,1,"return","Please provide a clear scan.",1).ok,"Office can return a file");
 Assert(!store.SubmitRequirement(number,requirement,0,2,"private.pdf",pdf).ok,"Stale upload cannot overwrite a newer version");
 var uploads=await Task.WhenAll(Task.Run(()=>store.SubmitRequirement(number,requirement,1,2,"corrected.pdf",pdf)),Task.Run(()=>store.SubmitRequirement(number,requirement,1,2,"corrected.pdf",pdf)));
 Assert(uploads.Count(r=>r.ok)==1,"Only one concurrent submission can use a version");
 var second=store.GetSubmissions(number,2,false).First();
 Assert(!store.ReviewSubmission(number,first.Id,1,"accept","",1).ok,"Old versions cannot be reviewed");
 Assert(store.ReviewSubmission(number,second.Id,2,"accept","Legible copy.",1).ok,"Latest upload can be accepted");
 Assert(store.SubmitRequirement(number,requirement,2,2,"replacement.pdf",pdf).ok,"Owner may replace an accepted file before readiness");
 Assert(!store.Snapshot().Tickets.Single(t=>t.TicketNumber==number).Checklist.Single().Verified,"Replacement must revoke old verification");
 Assert(store.AdvanceDocument(number,"Processing","Review started",1).ok,"Processing must start");
 Assert(!store.AdvanceDocument(number,"Ready for pickup","",1).ok,"Pending replacement blocks readiness");
 var latest=store.GetSubmissions(number,2,false).First();Assert(store.ReviewSubmission(number,latest.Id,3,"accept","",1).ok,"New version accepted");
 Assert(store.AdvanceDocument(number,"Ready for pickup","Collect at Registrar",1).ok,"Accepted required files enable readiness");
 Assert(!store.SubmitRequirement(number,requirement,3,2,"late.pdf",pdf).ok,"Ready requests cannot be altered");
 var archive=JsonSerializer.Serialize(store.Snapshot());QueueStore.ValidateBackup(archive);store.Reload();
 Assert(store.DownloadSubmission(first.Id,2,false)!.Value.bytes.SequenceEqual(pdf)&&store.GetSubmissions(number,2,false).Count==3,"Files and review history survive reload");
 var broken=JsonSerializer.Deserialize<SmartQueueData>(archive)!;broken.RequirementSubmissions[0].Sha256="tampered";
 var rejected=false;try{QueueStore.ValidateBackup(JsonSerializer.Serialize(broken));}catch(InvalidDataException){rejected=true;}Assert(rejected,"Backup hash tampering must be rejected");
 broken=JsonSerializer.Deserialize<SmartQueueData>(archive)!;broken.RequirementSubmissions[1].Version=1;
 rejected=false;try{QueueStore.ValidateBackup(JsonSerializer.Serialize(broken));}catch(InvalidDataException){rejected=true;}Assert(rejected,"Duplicate versions cannot restore");
 Assert(!store.RegisterIssuedDocument(number,2,"issued.pdf",pdf).ok,"Students cannot register issued documents");
 Assert(store.RegisterIssuedDocument(number,1,"issued.pdf",pdf).ok,"Office can register final PDF");var issued=store.IssuedDocuments(number,2,false).Single();
 Assert(store.VerifyIssuedDocument(issued.Code) is {RevokedAt:null},"Public registry lookup must project an active entry");
 Assert(store.DownloadIssuedDocument(issued.Id,3,false)==null&&store.DownloadIssuedDocument(issued.Id,2,false)!.Value.bytes.SequenceEqual(pdf),"Issued PDF downloads enforce ownership");
 Assert(!store.RegisterIssuedDocument(number,1,"duplicate.pdf",pdf).ok,"Duplicate active issuance is blocked");
 Assert(store.RevokeIssuedDocument(issued.Id,1,"QA correction needed").ok&&store.VerifyIssuedDocument(issued.Code)!.RevokedAt.HasValue,"Revocation remains visible publicly");
 QueueStore.ValidateBackup(JsonSerializer.Serialize(store.Snapshot()));store.Reload();Assert(store.VerifyIssuedDocument(issued.Code)!.RevokedAt.HasValue,"Issued file and revocation survive reload");
 Console.WriteLine("PASS issued PDF authorization, registry projection, private download, active-record uniqueness, revocation, and backup/reload");
 Console.WriteLine("PASS private uploads, signature/size validation, correction reasons, concurrent versions, staff review, readiness gates, file integrity, and reload");
}finally{Environment.SetEnvironmentVariable("CAMPUS_QUEUE_DATA_DIR",oldData);Directory.Delete(submissionFolder,true);}

class FakeClients:IHttpClientFactory {
 public int Calls;public string Provider="gemini",ModelText="";public Uri? Url;public string? Body;public bool HasCredential;
 public HttpClient CreateClient(string name)=>new(new FakeHandler(async request=>{Calls++;Url=request.RequestUri;Body=await request.Content!.ReadAsStringAsync();HasCredential=request.Headers.Contains("x-goog-api-key")||request.Headers.Authorization?.Scheme=="Bearer";object payload=Provider=="gemini"?new{candidates=new[]{new{content=new{parts=new[]{new{text=ModelText}}}}}}:new{choices=new[]{new{message=new{content=ModelText}}}};return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json")};}));
}
class FakeHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>send(request);}
class FakeHost:IWebHostEnvironment {
 public string ApplicationName{get;set;}="Checks";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}="";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
}

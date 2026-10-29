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

class FakeClients:IHttpClientFactory {
 public int Calls;public string Provider="gemini",ModelText="";public Uri? Url;public string? Body;public bool HasCredential;
 public HttpClient CreateClient(string name)=>new(new FakeHandler(async request=>{Calls++;Url=request.RequestUri;Body=await request.Content!.ReadAsStringAsync();HasCredential=request.Headers.Contains("x-goog-api-key")||request.Headers.Authorization?.Scheme=="Bearer";object payload=Provider=="gemini"?new{candidates=new[]{new{content=new{parts=new[]{new{text=ModelText}}}}}}:new{choices=new[]{new{message=new{content=ModelText}}}};return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json")};}));
}
class FakeHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>send(request);}
class FakeHost:IWebHostEnvironment {
 public string ApplicationName{get;set;}="Checks";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}="";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
}

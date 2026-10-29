"""HTTP tests for the document services, calendar, feedback and report authorization. Disposable data only."""
import os,sys,json,uuid,urllib.request,urllib.parse,urllib.error,http.cookiejar,re,html,datetime,concurrent.futures
if os.environ.get('CAMPUS_ALLOW_DESTRUCTIVE_TESTS')!='1':sys.exit('Use disposable data and set CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1. This test restores a workspace backup.')
BASE=os.environ.get('CAMPUS_TEST_URL','http://127.0.0.1:18080')
class Client:
 def __init__(self):self.c=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()));self.token=''
 def get(self,path):
  with self.c.open(BASE+path,timeout=30) as r:return r.geturl(),r.read().decode()
 def ctx(self):
  value=json.loads(self.get('/Assistant/Context')[1]);self.token=value['token'];return value
 def api(self,path,data=None,expected=200,csrf=True):
  headers={'Content-Type':'application/json'}
  if csrf:headers['RequestVerificationToken']=self.token
  req=urllib.request.Request(BASE+'/Assistant/'+path,data=json.dumps(data).encode() if data is not None else None,headers=headers)
  try:
   with self.c.open(req,timeout=30) as r:status=r.status;body=r.read().decode()
  except urllib.error.HTTPError as e:status=e.code;body=e.read().decode()
  assert status==expected,(path,status,expected,body[:500])
  return json.loads(body) if body else {}
 def form(self,path,data):
  _,body=self.get(path);value=csrf_token(body)
  with self.c.open(BASE+path,urllib.parse.urlencode({'csrf':value,**data}).encode()) as r:return r.geturl(),r.read().decode()
def csrf_token(body):
 for tag in re.findall(r'<input\b[^>]*>',body):
  attrs=dict(re.findall(r'([\w]+)="([^"]*)"',tag))
  if attrs.get('name')=='csrf':return html.unescape(attrs['value'])
 raise AssertionError('Missing token')
def login(username,password):
 c=Client();url,_=c.form('/sims/login.php',{'username':username,'password':password});assert url.endswith('/portal');c.ctx();return c
def backup(c):return json.loads(c.get('/sims/modules/admin/backup.php?download=1')[1])
def restore(c,archive):
 _,body=c.get('/sims/modules/admin/restore.php');boundary='assistant-'+uuid.uuid4().hex
 payload=''
 for name,value in {'csrf':csrf_token(body),'confirm_restore':'RESTORE'}.items():payload+=f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'
 payload+=f'--{boundary}\r\nContent-Disposition: form-data; name="backup"; filename="backup.json"\r\nContent-Type: application/json\r\n\r\n'+json.dumps(archive)+f'\r\n--{boundary}--\r\n'
 req=urllib.request.Request(BASE+'/sims/modules/admin/restore.php',data=payload.encode(),headers={'Content-Type':f'multipart/form-data; boundary={boundary}'})
 with c.c.open(req,timeout=30) as r:assert 'restored=1' in r.geturl(),r.read().decode()[-500:]

def endpoint(c,path,data=None,expected=200,csrf=True):
 headers={}
 if csrf:headers['RequestVerificationToken']=c.token
 req=urllib.request.Request(BASE+path,data=urllib.parse.urlencode(data,doseq=True).encode() if data is not None else None,headers=headers)
 try:
  with c.c.open(req,timeout=30) as r:status=r.status;body=r.read().decode();url=r.geturl()
 except urllib.error.HTTPError as e:status=e.code;body=e.read().decode();url=BASE+path
 assert status==expected,(path,status,expected,body[:500])
 return url,body
owner=login('ADMIN','ADMIN');original=backup(owner)
try:
 student=login('2024-0002','student123');other=login('2024-0001','student123')
 for path in ['/Services/Requirements','/Services/Report']:
  endpoint(student,path,expected=403)
 endpoint(student,'/Services/AddClosure',{'Office':'Registrar','Title':'Forged'},expected=403)
 endpoint(student,'/Services/SaveRequirement',{'Title':'Forged'},expected=403)
 endpoint(owner,'/Services/SaveRequirement',{'Title':'No token'},expected=400,csrf=False)
 endpoint(owner,'/Services/SaveRequirement',{'ServiceType':'Document Request','RequestType':'Other Document','Title':'QA school-approved requirement','Instructions':'QA approved school instructions','Required':'true','Active':'true'})
 endpoint(owner,'/Services/SaveRequirement',{'ServiceType':'School Fees','RequestType':'Other School Fee','Title':'QA requirement without extra instructions','Instructions':'','Required':'false','Active':'true'})
 assert json.loads(endpoint(student,'/Services/Checklist?serviceType=School%20Fees&requestType=Other%20School%20Fee')[1])['requirements']
 data=json.loads(endpoint(student,'/Services/Checklist?serviceType=Document%20Request&requestType=Other%20Document')[1]);requirement=data['requirements'][0]['id'];assert data['requirements'][0]['required']
 url,_=endpoint(student,'/Queue/GetTicket',{'ServiceType':'Document Request','RequestType':'Other Document','PhoneNumber':'09121110002','StudentNumber':'2024-0002','PriorityType':'Regular','RequestNotes':'Operations HTTP QA'})
 number=urllib.parse.parse_qs(urllib.parse.urlparse(url).query).get('id',[urllib.parse.unquote(urllib.parse.urlparse(url).path.rsplit('/',1)[-1])])[0];assert number.startswith('DOC-'),url
 request='/Services/Request?id='+urllib.parse.quote(number)
 _,body=endpoint(student,request);assert 'For review' in body and 'QA school-approved requirement' in body
 endpoint(other,request,expected=404);endpoint(other,'/Services/Claim?id='+number,expected=404)
 endpoint(student,'/Services/Progress',{'id':number,'stage':'Released','note':'Forged'},expected=403)
 endpoint(student,'/Services/Prepared',{'id':number,'prepared':[requirement]})
 ticket=lambda:next(t for t in backup(owner)['queue_data']['Tickets'] if t['TicketNumber']==number)
 assert ticket()['Checklist'][0]['Prepared'] and not ticket()['Checklist'][0]['Verified']
 endpoint(other,'/Services/Prepared',{'id':number,'prepared':[requirement]},expected=404);assert ticket()['Checklist'][0]['Prepared']
 endpoint(student,'/Services/Verify',{'id':number,'verified':[requirement]},expected=403)
 endpoint(student,'/Services/Feedback',{'id':number,'rating':'5','comment':'Too early'});assert not backup(owner)['queue_data']['Feedback']
 endpoint(owner,'/Services/Progress',{'id':number,'stage':'Ready for pickup'});assert ticket()['DocumentStatus']=='For review'
 endpoint(owner,'/Services/Progress',{'id':number,'stage':'Processing','note':'Review complete'});assert ticket()['DocumentStatus']=='Processing'
 endpoint(owner,'/Services/Progress',{'id':number,'stage':'Ready for pickup','note':'Collect at the office'});assert ticket()['DocumentStatus']=='Processing'
 endpoint(owner,'/Services/Verify',{'id':number,'verified':[requirement]})
 endpoint(owner,'/Services/Progress',{'id':number,'stage':'Ready for pickup','note':'Collect at the Registrar'});assert ticket()['DocumentStatus']=='Ready for pickup'
 claim=ticket()['ClaimCode'];_,body=endpoint(student,'/Services/Claim?id='+number);assert claim in body and 'code alone does not prove identity' in body
 endpoint(other,'/Services/Claim?id='+number,expected=404)
 _,public=endpoint(other,'/Home/Display');assert claim not in public
 endpoint(owner,'/Services/Progress',{'id':number,'stage':'Released','note':''});assert ticket()['DocumentStatus']=='Ready for pickup'
 endpoint(owner,'/Services/Progress',{'id':number,'stage':'Released','note':'Recipient identity verified in person'});assert ticket()['DocumentStatus']=='Released'
 endpoint(student,'/Services/Feedback',{'id':number,'rating':'6','comment':'Invalid'});assert not backup(owner)['queue_data']['Feedback']
 endpoint(student,'/Services/Feedback',{'id':number,'rating':'5','comment':'<script>QA feedback text</script>'});assert len(backup(owner)['queue_data']['Feedback'])==1
 endpoint(student,'/Services/Feedback',{'id':number,'rating':'4','comment':'Duplicate'});assert len(backup(owner)['queue_data']['Feedback'])==1
 endpoint(owner,'/Services/SaveRequirement',{'Id':requirement,'ServiceType':'Document Request','RequestType':'Other Document','Title':'Requirement updated for new requests','Instructions':'Future instructions','Required':'true','Active':'false'})
 assert ticket()['Checklist'][0]['Title']=='QA school-approved requirement'
 assert not json.loads(endpoint(student,'/Services/Checklist?serviceType=Document%20Request&requestType=Other%20Document')[1])['requirements']
 print('PASS service permissions, immutable checklists, student preparation versus office verification, staged release, private claims, and one eligible feedback')
 # The calendar is consulted by the same slot engine used for booking, changes and exchanges.
 slots=student.api('Slots?serviceType=Document%20Request')['slots'];assert slots;chosen=slots[0]['startsAt'];day=chosen[:10]
 payload={'serviceType':'Document Request','requestType':'Certificate of Enrollment','phoneNumber':'09121110002','studentNumber':'2024-0002','startsAt':chosen,'idempotencyKey':str(uuid.uuid4()),'confirmed':True}
 reserved=student.api('Book',payload)['booking']
 endpoint(owner,'/Services/AddClosure',{'Office':'Registrar','Mode':'Date','Date':day,'AllDay':'true','Title':'QA Registrar holiday','StartTime':'12:00','EndTime':'13:00'})
 assert not student.api('Slots?serviceType=Document%20Request&date='+day)['slots']
 assert other.api('Slots?serviceType=Tuition%20Payment&date='+day)['slots']
 assert next(b for b in student.api('Bookings')['bookings'] if b['ticketNumber']==reserved['ticketNumber'])['status']=='Scheduled'
 student.api('Reschedule',{'ticketNumber':reserved['ticketNumber'],'expectedStartsAt':chosen,'startsAt':day+'T10:00:00+08:00','confirmed':True},expected=409)
 student.api('Book',dict(payload,requestType='Good Moral Certificate',idempotencyKey=str(uuid.uuid4())),expected=409)
 alternatives=student.api('ChangeSlots?ticketNumber='+reserved['ticketNumber'])['slots'];assert alternatives and not any(s['startsAt'][:10]==day for s in alternatives)
 notifications=backup(owner)['queue_data']['AccountNotifications'];assert any(n['Title']=='Office availability changed' and n['TicketNumber']==reserved['ticketNumber'] for n in notifications)
 endpoint(owner,'/Services/AddClosure',{'Office':'All Services','Mode':'Weekly','Title':'QA recurring lunch','StartTime':'12:00','EndTime':'13:00'})
 assert not any(datetime.datetime.fromisoformat(s['startsAt']).hour==12 for s in other.api('Slots?serviceType=Tuition%20Payment')['slots'])
 count=len(backup(owner)['queue_data']['OfficeClosures'])
 endpoint(owner,'/Services/AddClosure',{'Office':'Registrar','Mode':'Date','Date':day,'Title':'Invalid reverse range','StartTime':'14:00','EndTime':'13:00'})
 assert len(backup(owner)['queue_data']['OfficeClosures'])==count
 endpoint(owner,'/Services/AddClosure',{'Office':'Registrar','Mode':'Weekly','Weekday':'not-a-day','Title':'Invalid weekday','StartTime':'12:00','EndTime':'13:00'})
 assert len(backup(owner)['queue_data']['OfficeClosures'])==count
 endpoint(student,'/Services/RemoveClosure',{'id':1},expected=403)
 print('PASS office-specific holidays, recurring lunch, existing-visit notices, and booking/reschedule exclusion')
 _,report=endpoint(owner,'/Services/Report');assert 'QA feedback text' in report and '&lt;script&gt;' in report and '<script>QA feedback text</script>' not in report
 _,csv=endpoint(owner,'/Services/Report?export=csv');assert 'Released documents' in csv and 'QA feedback text' not in csv and claim not in csv and '2024-0002' not in csv
 endpoint(owner,'/Services/Report?from=2026-12-01&to=2026-01-01',expected=400)
 # Full backup includes all six features' state and restores it together.
 archive=backup(owner);restore(owner,archive);owner=login('ADMIN','ADMIN');student=login('2024-0002','student123')
 assert ticket()['DocumentStatus']=='Released' and ticket()['ClaimCode']==claim and len(backup(owner)['queue_data']['Feedback'])==1
 print('PASS escaped private feedback, aggregate-only CSV, report validation, and full workspace round-trip')
finally:
 restore(owner,original)
print('ALL CAMPUS OPERATIONS HTTP CHECKS PASSED')

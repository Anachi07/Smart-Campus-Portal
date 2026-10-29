"""HTTP tests for the school guide and confirmed reservations. Disposable data only."""
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
admin=login('ADMIN','ADMIN');original=backup(admin);guest=Client();ctx=guest.ctx()
assert not ctx['authenticated'] and not ctx['aiConfigured'],'Run these integration tests with provider keys unset.'
for path in ['/','/sims/login.php','/sims/register.php']:
 _,body=guest.get(path)
 for password in ['student123','teacher123','parent123','<code>ADMIN','value="ADMIN"']:assert password not in body,(path,'credential hint exposed')
guest.api('Chat',{'message':'How do I request documents?','history':[]},expected=400,csrf=False)
reply=guest.api('Chat',{'message':'How do I request documents?','history':[]});assert reply['mode']=='guide' and 'Registrar' in reply['answer'] and reply['sources']
unknown=guest.api('Chat',{'message':'Tell me about xyzzebra campus spaceship policy','history':[]});assert not unknown['sources'] and 'approved' in unknown['answer']
count=len(backup(admin)['queue_data']['Tickets']);intent=guest.api('Chat',{'message':'Magpa schedule ako ng tuition','history':[]});assert intent['suggestScheduling'] and len(backup(admin)['queue_data']['Tickets'])==count
guest.api('Slots?serviceType=Document%20Request',expected=401);guest.api('Book',{'confirmed':True,'userId':2},expected=401)
print('PASS no public credential hints, CSRF, truthful guide mode, unknown facts, guest restrictions, and no booking from chat')
question='What does the qaverifiedoffice notice say?'
admin.form('/sims/modules/admin/knowledge.php',{'question':question,'answer':'QA approved answer for the office test.','source_label':'QA school office'})
assert not guest.api('Chat',{'message':question,'history':[]})['sources']
_,body=admin.get('/sims/modules/admin/knowledge.php');id=re.findall(r'\?edit=(\d+)',body)[0]
admin.form('/sims/modules/admin/knowledge.php?edit='+id,{'id':id,'question':question,'answer':'QA approved answer for the office test.','source_label':'QA school office','published':'1','approved':'1'})
reply=guest.api('Chat',{'message':question,'history':[]});assert any(source['id']==int(id) for source in reply['sources']) and 'QA approved answer' in reply['answer']
print('PASS draft/public FAQ boundaries and school-approved citations')
student=login('2024-0002','student123');context=student.ctx();assert context['hasTimetable']
now=datetime.datetime.fromisoformat(context['now']);date=now.date()+datetime.timedelta(days=1)
while date.weekday()!=2:date+=datetime.timedelta(days=1)
slots=student.api('Slots?serviceType=Document%20Request&date='+date.isoformat())['slots'];assert slots and any(s['recommended'] for s in slots)
assert not any(13<=datetime.datetime.fromisoformat(s['startsAt']).hour<=15 and datetime.datetime.fromisoformat(s['startsAt']).minute==0 for s in slots),'Wednesday class conflict was offered'
slot=slots[0]
payload={'serviceType':'Document Request','requestType':'Other Document','studentNumber':'2024-0002','phoneNumber':'09121110002','requestNotes':'QA appointment','startsAt':slot['startsAt'],'idempotencyKey':str(uuid.uuid4()),'confirmed':False}
student.api('Book',payload,expected=409)
invalid=dict(payload,confirmed=True,startsAt='2000-01-01T09:00:00+08:00');student.api('Book',invalid,expected=409)
invalid=dict(payload,confirmed=True,startsAt=date.isoformat()+'T13:00:00+08:00');student.api('Book',invalid,expected=409)
payload['confirmed']=True;created=student.api('Book',payload)['booking'];again=student.api('Book',payload)['booking'];assert created['ticketNumber']==again['ticketNumber'] and created['status']=='Scheduled'
assert any(b['ticketNumber']==created['ticketNumber'] for b in student.api('Bookings')['bookings'])
student.api('CheckIn',{'ticketNumber':created['ticketNumber']},expected=409)
other=login('2024-0001','student123');other.api('Cancel',{'ticketNumber':created['ticketNumber']},expected=409);assert created['ticketNumber'] not in json.dumps(other.api('Bookings'))
print('PASS timetable-aware recommendations, confirmation, future/class validation, idempotent reservation, early check-in rejection, and ownership')
# The default office capacity is three; concurrent attempts must not exceed it.
actors=[other,login('PARENT001','parent123'),login('TCH002','teacher123')]
def compete(c):
 p=dict(payload,studentNumber=c.ctx()['studentNumber'] or 'QA-REF',idempotencyKey=str(uuid.uuid4()))
 try:return c,c.api('Book',p),p
 except AssertionError as e:
  if "409" in str(e):return c,None,p
  raise
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:outcomes=list(pool.map(compete,actors))
assert sum(outcome[1] is not None for outcome in outcomes)==2,outcomes
student.api('Cancel',{'ticketNumber':created['ticketNumber']})
failed=next(outcome for outcome in outcomes if outcome[1] is None);assert failed[0].api('Book',failed[2])['booking']['status']=='Scheduled'
print('PASS concurrent office capacity and capacity release after cancellation')
# Prevent private keys from being approved into public knowledge.
_,error=admin.form('/sims/modules/admin/knowledge.php',{'question':'Unsafe QA entry','answer':'password: do-not-publish','source_label':'QA','published':'1','approved':'1'})
assert 'Do not publish passwords' in error
restore(admin,original)
print('PASS credential-publication guard and workspace restore with appointments')
print('ALL ASSISTANT HTTP CHECKS PASSED')

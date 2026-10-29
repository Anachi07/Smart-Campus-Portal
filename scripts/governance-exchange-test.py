"""HTTP tests for the Super Admin governance and consent-based visit changes. Disposable data only."""
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
import base64,sqlite3,tempfile,copy

def query(archive,sql,values=()):
 with tempfile.NamedTemporaryFile() as f:
  f.write(base64.b64decode(archive['academic_database']));f.flush()
  with sqlite3.connect(f.name) as db:return db.execute(sql,values).fetchall()
def expect_http(c,path,status,data=None):
 req=urllib.request.Request(BASE+path,data=urllib.parse.urlencode(data).encode() if data else None)
 try:
  with c.c.open(req,timeout=30) as r:assert r.status==status,(path,r.status,status)
 except urllib.error.HTTPError as e:assert e.code==status,(path,e.code,status)
def own(c,number):return next(b for b in c.api('Bookings')['bookings'] if b['ticketNumber']==number)
def request_change(ticket,at,confirmed=True):return {'ticketNumber':ticket['ticketNumber'],'expectedStartsAt':ticket['scheduledAt'],'startsAt':at,'confirmed':confirmed}
owner=login('ADMIN','ADMIN');original=backup(owner)
try:
 # Configure a one-seat office in the disposable fixture so swaps cannot conceal overbooking.
 fixture=copy.deepcopy(original);fixture['queue_data']['Settings']['MaxBookingsPerSlot']=1
 restore(owner,fixture);owner=login('ADMIN','ADMIN');archive=backup(owner)
 assert query(archive,'SELECT r.name FROM users u JOIN roles r ON r.id=u.role_id WHERE u.username="ADMIN"')[0][0]=='Super Admin'
 roles=dict((name,id) for id,name in query(archive,'SELECT id,name FROM roles'))
 owner.form('/sims/modules/admin/manage.php?entity=users',{'action':'create','username':'qa_regular_admin','full_name':'QA Regular Admin','email':'qa-admin@example.test','role_id':roles['Admin'],'status':'Active','new_password':'RegularAdminPassword123!'})
 regular=login('qa_regular_admin','RegularAdminPassword123!')
 for path in ['/sims/modules/admin/manage.php?entity=users','/sims/modules/admin/backup.php?download=1','/sims/modules/admin/restore.php','/Admin/Users']:
  expect_http(regular,path,403)
 _,body=regular.get('/portal');assert 'entity=users' not in body and 'admin/restore.php' not in body
 expect_http(regular,'/sims/modules/admin/manage.php?entity=users',403,{'csrf':csrf_token(body),'action':'create','username':'attack','role_id':roles['Super Admin']})
 assert regular.get('/sims/modules/admin/manage.php?entity=students')[0].endswith('entity=students')
 url,queue_page=regular.get('/Admin/Dashboard');assert url.endswith('/Admin/Dashboard') and 'entity=users' not in queue_page
 print('PASS one-time Super Admin migration, regular Admin CRUD/approval/backup denial, and retained office access')
 # Registration is pending; POSTed status cannot bypass approval.
 guest=Client();name='qa_approval_'+uuid.uuid4().hex[:6];password='ApprovalPassword123!'
 guest.form('/sims/register.php',{'full_name':'QA Approval Student','username':name,'email':name+'@example.test','password':password,'confirm_password':password,'status':'Active'})
 archive=backup(owner);uid=query(archive,'SELECT id FROM users WHERE username=?',(name,))[0][0]
 assert query(archive,'SELECT status FROM users WHERE id=?',(uid,))[0][0]=='Pending'
 url,_=guest.form('/sims/login.php',{'username':name,'password':password});assert not url.endswith('/portal')
 owner.form('/sims/modules/admin/manage.php?entity=users',{'action':'approve','id':uid})
 approved=login(name,password);assert approved.ctx()['authenticated']
 assert query(backup(owner),'SELECT COUNT(*) FROM activity_logs WHERE action="Approve account"')[0][0]
 # Deactivation revokes a live session; no self-demotion or last-owner deletion.
 owner.form('/sims/modules/admin/manage.php?entity=users',{'action':'update','id':uid,'username':name,'full_name':'QA Approval Student','email':name+'@example.test','role_id':roles['Student'],'status':'Inactive'})
 assert approved.get('/portal')[0].endswith('/sims/login.php')
 owner_id=query(backup(owner),'SELECT id FROM users WHERE username="ADMIN"')[0][0]
 _,body=owner.form('/sims/modules/admin/manage.php?entity=users',{'action':'delete','id':owner_id});assert 'active Super Admin' in body
 name2='qa_reject_'+uuid.uuid4().hex[:6]
 guest.form('/sims/register.php',{'full_name':'QA Rejected Student','username':name2,'email':name2+'@example.test','password':password,'confirm_password':password})
 uid2=query(backup(owner),'SELECT id FROM users WHERE username=?',(name2,))[0][0]
 owner.form('/sims/modules/admin/manage.php?entity=users',{'action':'reject','id':uid2})
 url,_=guest.form('/sims/login.php',{'username':name2,'password':password});assert not url.endswith('/portal')
 print('PASS pending registration, explicit approval/rejection, audit log, session revocation, and last-owner protection')
 a=login('2024-0002','student123');b=login('PARENT001','parent123');outsider=login('2024-0001','student123')
 service='Document%20Request'
 aslots=a.api('Slots?serviceType='+service)['slots'];bslots={s['startsAt'] for s in b.api('Slots?serviceType='+service)['slots']}
 common=[s['startsAt'] for s in aslots if s['startsAt'] in bslots]
 assert len(common)>5
 source,target,newtime,later=common[:4]
 def book(c,at,ref):return c.api('Book',{'serviceType':'Document Request','requestType':'Other Document','studentNumber':ref,'phoneNumber':'09121110001','startsAt':at,'idempotencyKey':str(uuid.uuid4()),'confirmed':True})['booking']
 ta=book(a,source,'2024-0002');tb=book(b,target,'2024-0001');assert ta['canChange']
 change=request_change(ta,newtime)
 a.api('Reschedule',dict(change,confirmed=False),expected=409);outsider.api('Reschedule',change,expected=409);a.api('Reschedule',change,expected=400,csrf=False)
 a.api('Reschedule',request_change(ta,target),expected=409)
 moved=a.api('Reschedule',change)['booking'];assert moved['scheduledAt']==newtime
 assert a.api('Reschedule',change)['booking']['scheduledAt']==newtime
 a.api('Reschedule',request_change(ta,later),expected=409)
 assert any(s['startsAt']==source for s in b.api('Slots?serviceType='+service)['slots'])
 assert own(b,tb['ticketNumber'])['scheduledAt']==target
 print('PASS reschedule ownership, CSRF, explicit confirmation, full-slot denial, stale-state rejection, and capacity release')
 def ask():return a.api('RequestExchange',request_change(own(a,ta['ticketNumber']),target))
 def incoming():return next(e for e in b.api('Exchanges')['exchanges'] if e['canRespond'])
 options=a.api('ExchangeTimes?ticketNumber='+urllib.parse.quote(ta['ticketNumber']));assert any(t['startsAt']==target for t in options['times'])
 assert tb['ticketNumber'] not in json.dumps(options) and '2024-0001' not in json.dumps(options)
 a.api('RequestExchange',request_change(moved,target,False),expected=409)
 ask();invite=incoming();assert invite['ticketNumber']==tb['ticketNumber'] and ta['ticketNumber'] not in json.dumps(invite)
 assert own(a,ta['ticketNumber'])['scheduledAt']==newtime and own(b,tb['ticketNumber'])['scheduledAt']==target
 assert b.ctx()['pendingExchanges']==1
 action={'id':invite['id'],'decision':'accept','confirmed':True}
 outsider.api('RespondExchange',action,expected=409);a.api('RespondExchange',action,expected=409);b.api('RespondExchange',dict(action,confirmed=False),expected=409)
 b.api('RespondExchange',dict(action,decision='decline'));assert own(b,tb['ticketNumber'])['scheduledAt']==target
 ask();invite=incoming();a.api('RespondExchange',{'id':invite['id'],'decision':'withdraw','confirmed':True});assert own(a,ta['ticketNumber'])['scheduledAt']==newtime
 ask();invite=incoming();b.api('RespondExchange',{'id':invite['id'],'decision':'accept','confirmed':True})
 assert own(a,ta['ticketNumber'])['scheduledAt']==target and own(b,tb['ticketNumber'])['scheduledAt']==newtime
 b.api('RespondExchange',{'id':invite['id'],'decision':'accept','confirmed':True},expected=409)
 archive=backup(owner);assert all(sum(t['Status']=='Scheduled' and t.get('ScheduledAt')==time for t in archive['queue_data']['Tickets'])==1 for time in [target,newtime])
 print('PASS private invitations, no changes before acceptance, decline/withdraw, owner-only acceptance, atomic swap, and repeat rejection')
 # Changing the reservation invalidates an outstanding invitation.
 target=newtime;ask();invite=incoming()
 a.api('Reschedule',request_change(own(a,ta['ticketNumber']),later))
 b.api('RespondExchange',{'id':invite['id'],'decision':'accept','confirmed':True},expected=409)
 assert own(b,tb['ticketNumber'])['scheduledAt']==newtime
 expect_http(a,'/sims/assistant-schedules.php',404)
 intent=a.api('Chat',{'message':'Gusto ko makipagpalit ng schedule','history':[]});assert intent['mode']=='workflow' and intent['suggestScheduling']
 print('PASS stale invitation invalidation, internal schedule endpoint privacy, and chat routing to confirmed workflows')
finally:
 restore(owner,original)
print('ALL GOVERNANCE AND EXCHANGE HTTP CHECKS PASSED')

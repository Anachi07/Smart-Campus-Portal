"""Exercises permissions, CRUD, attendance, recovery and queue isolation on disposable data."""
import os,sys,urllib.request,urllib.parse,urllib.error,http.cookiejar,re,html,json,base64,sqlite3,tempfile,uuid
if os.environ.get('CAMPUS_ALLOW_DESTRUCTIVE_TESTS')!='1':
    sys.exit('Use disposable data, then set CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1. This suite creates records and restores a backup.')
BASE=os.environ.get('CAMPUS_TEST_URL','http://127.0.0.1:18080')
def client():return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(c,path,data=None):
    if isinstance(data,dict):data=urllib.parse.urlencode(data).encode()
    with c.open(BASE+path,data,timeout=25) as r:return r.geturl(),r.read().decode()
def token(body,name="csrf"):
    for tag in re.findall(r'<input\b[^>]*>',body):
        attrs=dict(re.findall(r'([\w]+)="([^"]*)"',tag))
        if attrs.get('name')==name:return html.unescape(attrs['value'])
    raise AssertionError('CSRF token missing')
def post(c,path,data):
    _,body=request(c,path)
    return request(c,path,{'csrf':token(body),**data})
def login(name,password):
    c=client();url,body=post(c,'/sims/login.php',{'username':name,'password':password})
    assert url.endswith('/portal'),(name,url,body[:200])
    return c
def status(c,path,expected,data=None):
    try:
        url,body=request(c,path,data)
        assert expected==200,(path,expected,'returned 200')
        return body
    except urllib.error.HTTPError as e:assert e.code==expected,(path,e.code,expected)
def backup(c):return json.loads(request(c,'/sims/modules/admin/backup.php?download=1')[1])
def rows(archive,sql,params=()):
    with tempfile.NamedTemporaryFile() as f:
        f.write(base64.b64decode(archive['academic_database']));f.flush()
        with sqlite3.connect(f.name) as db:return db.execute(sql,params).fetchall()
def upload(c,archive):
    _,body=request(c,'/sims/modules/admin/restore.php');boundary='campus-'+uuid.uuid4().hex
    chunks=[]
    for key,value in {'csrf':token(body),'confirm_restore':'RESTORE'}.items():
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
    chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="backup"; filename="backup.json"\r\nContent-Type: application/json\r\n\r\n'.encode()+json.dumps(archive).encode()+b'\r\n')
    chunks.append(f'--{boundary}--\r\n'.encode())
    req=urllib.request.Request(BASE+'/sims/modules/admin/restore.php',data=b''.join(chunks),headers={'Content-Type':f'multipart/form-data; boundary={boundary}'})
    with c.open(req,timeout=30) as r:return r.geturl(),r.read().decode()
admin=login('ADMIN','ADMIN');original=backup(admin)
student=login('2024-0002','student123');other=login('2024-0001','student123');teacher=login('TCH001','teacher123');parent=login('PARENT001','parent123')
for path in ['/sims/modules/shared/attendance_ai.php','/sims/modules/shared/qr_attendance.php','/sims/modules/shared/predictive.php','/sims/modules/admin/manage.php']:
    status(student,path,403)
for c,allowed,forbidden in [(student,'2024-0002','2024-0001'),(parent,'2024-0001','2024-0002')]:
    for kind in ['attendance','performance','risk']:
        _,report=request(c,f'/sims/modules/shared/reports.php?type={kind}&export=csv')
        assert forbidden not in report,(kind,'cross-account records leaked')
_,report=request(teacher,'/sims/modules/teacher/students.php');assert 'Anna Lopez' not in report
status(student,'/sims/modules/student/profile.php',419,{'full_name':'spoof','email':'spoof@example.test'})
status(client(),'/internal/queue-reload',404)
status(student,'/sims/modules/student/profile.php',419,{'csrf[]':'bad'})
print('PASS role boundaries, report isolation, teacher scope, CSRF, and internal route blocking')
# Registration cannot create staff accounts, and an unassigned student sees no other records.
anon=client();_,body=request(anon,'/sims/register.php')
status(anon,'/sims/register.php',403,{'csrf':token(body),'role':'Teacher','username':'badteacher','full_name':'Bad Teacher','email':'bad@example.test','password':'TestPassword123!','confirm_password':'TestPassword123!'})
newname='qa_'+uuid.uuid4().hex[:10];password='StudentPassword123!'
post(anon,'/sims/register.php',{'full_name':'QA Student','username':newname,'email':newname+'@example.test','password':password,'confirm_password':password})
pending_id=rows(backup(admin),'SELECT id FROM users WHERE username=?',(newname,))[0][0]
url,body=post(anon,'/sims/login.php',{'username':newname,'password':password});assert not url.endswith('/portal')
post(admin,'/sims/modules/admin/manage.php?entity=users',{'action':'approve','id':pending_id})
newstudent=login(newname,password)
_,body=request(newstudent,'/sims/modules/student/grades.php');assert 'Juan Dela Cruz' not in body
_,report=request(newstudent,'/sims/modules/shared/reports.php?export=csv');assert '2024-0001' not in report and '2024-0002' not in report
# Password rotation invalidates a second session.
oldsession=login(newname,password)
post(newstudent,'/sims/modules/student/profile.php',{'action':'password','current_password':password,'new_password':'NewStudentPassword123!','confirm_password':'NewStudentPassword123!'})
assert request(oldsession,'/portal')[0].endswith('/sims/login.php')
login(newname,'NewStudentPassword123!')
# Lockout is enforced after repeated failures without affecting other accounts.
locked=client()
for _ in range(5):post(locked,'/sims/login.php',{'username':'qa_nonexistent_lockout','password':'invalid'})
_,body=request(locked,'/sims/login.php')
status(locked,'/sims/login.php',429,{'csrf':token(body),'username':'qa_nonexistent_lockout','password':'invalid'})
print('PASS login throttling after repeated failed attempts')
print('PASS student-only registration, missing-profile isolation, and password session invalidation')
# Admin CRUD, foreign-key choices, required passwords, and last-admin protection.
roleids=dict((name,str(id)) for id,name in rows(original,'SELECT id,name FROM roles'))
_,body=post(admin,'/sims/modules/admin/manage.php?entity=users',{'action':'create','username':'qa_no_password','full_name':'No Password','email':'no-password@example.test','role_id':roleids['Student'],'status':'Active'})
assert '12–200' in body and not rows(backup(admin),'SELECT id FROM users WHERE username="qa_no_password"')
_,body=post(admin,'/sims/modules/admin/manage.php?entity=users',{'action':'create','username':'qa_staff','full_name':'QA Staff','email':'qa-staff@example.test','role_id':roleids['Teacher'],'status':'Active','new_password':'TeacherPassword123!'})
archive=backup(admin);uid=rows(archive,'SELECT id FROM users WHERE username="qa_staff"')[0][0]
assert rows(archive,'SELECT id FROM teachers WHERE user_id=?',(uid,))
adminid=rows(archive,'SELECT id FROM users WHERE username="ADMIN"')[0][0]
_,body=post(admin,'/sims/modules/admin/manage.php?entity=users',{'action':'delete','id':adminid})
assert 'super admin' in body.lower() and rows(backup(admin),'SELECT id FROM users WHERE id=?',(adminid,))
print('PASS validated account CRUD, linked staff profile creation, and last-admin protection')
# Grades are validated, saved, and reflected in scoped reports.
_,body=post(admin,'/sims/modules/teacher/grades.php',{'student_id':2,'subject_id':1,'prelim':120,'midterm':90,'final':90})
assert '0 to 100' in body
post(admin,'/sims/modules/teacher/grades.php',{'student_id':2,'subject_id':1,'prelim':95,'midterm':96,'final':97})
assert rows(backup(admin),'SELECT gpa FROM grades WHERE student_id=2 AND subject_id=1')[0][0]==1.25
# Repeated class attendance updates rather than creates duplicate records.
import datetime
from zoneinfo import ZoneInfo
today=datetime.datetime.now(ZoneInfo('Asia/Manila')).date().isoformat()
for _ in range(2):post(admin,'/sims/modules/teacher/attendance.php?subject_id=1',{'subject_id':1,'date':today,'status[2]':'Present'})
assert rows(backup(admin),'SELECT COUNT(*) FROM attendance WHERE student_id=2 AND subject_id=1 AND date=?',(today,))[0][0]==1
_,body=post(admin,'/sims/modules/teacher/attendance.php?subject_id=1',{'subject_id':1,'date':'2099-01-01','status[2]':'Present'})
assert 'valid attendance' in body
print('PASS grade validation and GPA calculation, attendance upsert, and future-date rejection')
# Own tickets only; repeat submission returns the same active ticket.
_,body=request(student,'/Queue/GetTicket');qt=token(body,'__RequestVerificationToken')
payload={'__RequestVerificationToken':qt,'PhoneNumber':'09121110002','ServiceType':'School Fees','RequestType':'Miscellaneous Fees','StudentNumber':'2024-0002','PriorityType':'Regular'}
first,ticket=request(student,'/Queue/GetTicket',payload);second,_=request(student,'/Queue/GetTicket',payload)
assert first==second,(first,second)
path=urllib.parse.urlsplit(first).path+'?'+urllib.parse.urlsplit(first).query
status(other,path,404)
_,found=request(other,'/Queue/MyTicket?searchText=09121110002');assert 'School Fees</strong>' not in found
# A counter serves one ticket at a time; waiting tickets cannot be completed.
number=urllib.parse.parse_qs(urllib.parse.urlsplit(first).query).get('id',[urllib.parse.urlsplit(first).path.rsplit('/',1)[-1]])[0]
def queuepost(path,values,form='/Admin/QueueControl'):
    _,body=request(admin,form)
    return request(admin,path,{'__RequestVerificationToken':token(body,'__RequestVerificationToken'),**values})
queuepost('/Admin/Complete',{'ticketNumber':number})
assert next(t for t in backup(admin)['queue_data']['Tickets'] if t['TicketNumber']==number)['Status']=='Waiting'
queuepost('/Admin/AddCounter',{'name':'QA Counter','staffName':'QA Staff'},'/Admin/Counters')
queuepost('/Admin/CallNext',{'counterName':'QA Counter'})
queuepost('/Admin/CallNext',{'counterName':'QA Counter'})
queue=backup(admin)['queue_data']
serving=[t for t in queue['Tickets'] if t['Status']=='Now Serving' and t['CounterName']=='QA Counter']
assert len(serving)==1
queuepost('/Admin/Complete',{'ticketNumber':serving[0]['TicketNumber']})
assert next(c for c in backup(admin)['queue_data']['Counters'] if c['Name']=='QA Counter')['Status']=='Available'
print('PASS single-ticket counters, valid queue transitions, and automatic counter release')
print('PASS ticket ownership and duplicate-submit handling')

# Mixed office queues: Registrar must not call a tuition ticket even if it has priority.
def school_ticket(service,kind,priority='Regular'):
    _,body=request(student,'/Queue/GetTicket')
    url,body=request(student,'/Queue/GetTicket',{'__RequestVerificationToken':token(body,'__RequestVerificationToken'),'PhoneNumber':'09121110002','ServiceType':service,'RequestType':kind,'StudentNumber':'2024-0002','PriorityType':priority,'RequestNotes':'QA transaction'})
    assert '/Queue/Ticket' in url,(service,url)
    return urllib.parse.parse_qs(urllib.parse.urlsplit(url).query).get('id',[urllib.parse.urlsplit(url).path.rsplit('/',1)[-1]])[0],body
newdoc,_=school_ticket('Document Request','Good Moral Certificate')
tuition,tuitionbody=school_ticket('Tuition Payment','Current Tuition Assessment','Priority')
assert 'Not an official receipt or proof of payment' in tuitionbody
queuepost('/Admin/AddCounter',{'name':'QA Registrar','staffName':'Registrar','office':'Registrar'},'/Admin/Counters')
queuepost('/Admin/AddCounter',{'name':'QA Cashier','staffName':'Cashier','office':'Cashier'},'/Admin/Counters')
queuepost('/Admin/CallNext',{'counterName':'QA Registrar'})
queuepost('/Admin/CallNext',{'counterName':'QA Cashier'})
queued=backup(admin)['queue_data']
assert next(t for t in queued['Tickets'] if t['TicketNumber']==newdoc)['CounterName']=='QA Registrar'
assert next(t for t in queued['Tickets'] if t['TicketNumber']==tuition)['CounterName']=='QA Cashier'
_,body=request(student,'/Queue/GetTicket')
_,invalid=request(student,'/Queue/GetTicket',{'__RequestVerificationToken':token(body,'__RequestVerificationToken'),'PhoneNumber':'09121110002','ServiceType':'General Inquiry','RequestType':'Anything','PriorityType':'Regular'})
assert 'Choose a school service' in invalid
_,body=request(student,'/Queue/GetTicket')
_,invalid=request(student,'/Queue/GetTicket',{'__RequestVerificationToken':token(body,'__RequestVerificationToken'),'PhoneNumber':'09121110002','ServiceType':'Tuition Payment','RequestType':'Outstanding Tuition Balance','StudentNumber':'','PriorityType':'Regular'})
assert 'Enter the student number' in invalid
print('PASS document/tuition request validation, Registrar/Cashier routing, and payment disclaimer')

_,display=request(client(),'/Home/Display')
assert newdoc in display and tuition in display and 'Maria Santos' not in display
# With every Registrar/general counter offline, a document must not be assigned to Cashier.
for item in list(queued['Tickets']):
    if item['Status']=='Now Serving' and item['Office']=='Registrar':queuepost('/Admin/Complete',{'ticketNumber':item['TicketNumber']})
for counter in backup(admin)['queue_data']['Counters']:
    if counter['Office'] in ['Registrar','All Services']:queuepost('/Admin/UpdateCounter',{'id':counter['Id'],'status':'Offline'},'/Admin/Counters')
unavailable,_=school_ticket('Document Request','Transcript / Academic Record')
waiting=next(t for t in backup(admin)['queue_data']['Tickets'] if t['TicketNumber']==unavailable)
assert waiting['EstimatedServiceStartAt'] is None and waiting['PredictedCounterName']=='Awaiting Registrar'
print('PASS multi-office public display, student privacy, and honest unavailable-office estimates')


# Calendar/events and settings persist across fresh page requests.
post(admin,'/sims/modules/admin/manage.php?entity=events',{'action':'create','title':'QA Campus Event','event_date':today,'description':'Verified event'})
assert 'QA Campus Event' in request(student,'/sims/modules/shared/calendar.php')[1]
post(admin,'/sims/modules/admin/settings.php',{'settings[school_name]':'QA Campus','settings[academic_year]':'2026-2027'})
assert 'QA Campus' in request(admin,'/sims/modules/admin/settings.php')[1]
print('PASS calendar event CRUD and persisted school settings')
# Round-trip restore validates before replacing and invalidates all older sessions.
invalid=dict(original,academic_database=base64.b64encode(b'not sqlite').decode())
_,body=upload(admin,invalid);assert 'valid academic database' in body
assert rows(backup(admin),'SELECT id FROM users WHERE username="qa_staff"')
url,body=upload(admin,original);assert '/sims/login.php?restored=1' in url,(url,body[-500:])
assert request(student,'/portal')[0].endswith('/sims/login.php')
restored=login('ADMIN','ADMIN');archive=backup(restored)
assert not rows(archive,'SELECT id FROM users WHERE username="qa_staff"')
assert len(archive['queue_data']['Tickets'])==len(original['queue_data']['Tickets'])
print('PASS backup integrity rejection, academic/queue restore, and session revocation')
print('ALL REGRESSION GROUPS PASSED')

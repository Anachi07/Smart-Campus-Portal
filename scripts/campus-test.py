"""Private requirement upload/review and large-backup regression. Disposable demo data only."""
import os, sys, json, uuid, re, html, base64, urllib.request, urllib.parse, urllib.error, http.cookiejar, concurrent.futures, sqlite3, hmac, hashlib, struct, time
if os.environ.get('CAMPUS_ALLOW_DESTRUCTIVE_TESTS') != '1':
    sys.exit('Use disposable data and CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1; this suite restores backups.')
BASE = os.environ.get('CAMPUS_TEST_URL', 'http://127.0.0.1:8080')
class Client:
    def __init__(self):
        self.http = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.token = ''
    def request(self, path, data=None, headers=None, expected=200):
        try:
            with self.http.open(urllib.request.Request(BASE+path, data=data, headers=headers or {}), timeout=60) as response:
                result = (response.status, response.read(), response.headers, response.geturl())
        except urllib.error.HTTPError as error:
            result = (error.code, error.read(), error.headers, BASE+path)
        assert result[0] == expected, (path, result[0], expected, result[1][:150])
        return result
    def form(self, path, values, expected=200, csrf=True):
        return self.request(path, urllib.parse.urlencode(values, doseq=True).encode(), {'RequestVerificationToken':self.token} if csrf else {}, expected)
    def multipart(self, path, values, filename, content, expected=200, csrf=True, field='file'):
        boundary = 'campus-'+uuid.uuid4().hex
        parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode() for key,value in values.items()]
        parts += [f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode(),content,f'\r\n--{boundary}--\r\n'.encode()]
        headers = {'Content-Type':'multipart/form-data; boundary='+boundary}
        if csrf: headers['RequestVerificationToken'] = self.token
        return self.request(path,b''.join(parts),headers,expected)
def csrf(body):
    for tag in re.findall(r'<input\b[^>]*>',body.decode()):
        attributes = dict(re.findall(r'([\w]+)="([^"]*)"',tag))
        if attributes.get('name') == 'csrf': return html.unescape(attributes['value'])
    raise AssertionError('Missing CSRF token')
def login(username,password):
    client=Client();page=client.request('/sims/login.php')[1]
    result=client.form('/sims/login.php',{'csrf':csrf(page),'username':username,'password':password},csrf=False)
    assert result[3].endswith('/portal')
    client.token=json.loads(client.request('/Assistant/Context')[1])['token']
    return client
def backup(owner): return json.loads(owner.request('/sims/modules/admin/backup.php?download=1')[1])
def restore(owner,archive):
    page=owner.request('/sims/modules/admin/restore.php')[1]
    response=owner.multipart('/sims/modules/admin/restore.php',{'csrf':csrf(page),'confirm_restore':'RESTORE'},'backup.json',json.dumps(archive).encode(),field='backup')
    assert 'restored=1' in response[3], response[1][-200:]
def create(client):
    result=client.form('/Queue/GetTicket',{'ServiceType':'Document Request','RequestType':'Other Document','PhoneNumber':'09121110002','StudentNumber':'2024-0002','PriorityType':'Regular'})
    url=urllib.parse.urlparse(result[3]);number=urllib.parse.parse_qs(url.query).get('id',[url.path.rsplit('/',1)[-1]])[0]
    assert number.startswith('DOC-'),result[3]
    return number
def database(owner):
    connection=sqlite3.connect(':memory:');connection.deserialize(base64.b64decode(backup(owner)['academic_database']));connection.row_factory=sqlite3.Row;return connection
def record(owner,table,id):
    with database(owner) as db:return dict(db.execute('SELECT * FROM '+table+' WHERE id=?',(id,)).fetchone())
def page_post(client,path,values,expected=200):
    page=client.request(path)[1];values={'csrf':csrf(page),**values};return client.form(path,values,expected,False)
def new_work(client,kind,details='QA test request details',**values):
    path='/sims/modules/shared/workflows.php?kind='+kind
    page=client.request(path)[1].decode();key=re.search(r'name="request_key" value="([a-f0-9]+)"',page)[1]
    result=page_post(client,path,{'action':'create','details':details,'request_key':key,**values})
    id=int(urllib.parse.parse_qs(urllib.parse.urlparse(result[3]).query)['id'][0]);return id
owner=login('ADMIN','ADMIN');original=backup(owner)
try:
    student=login('2024-0002','student123');other=login('2024-0001','student123');teacher=login('TCH001','teacher123');teacher2=login('TCH002','teacher123');parent=login('PARENT001','parent123')
    with database(owner) as db:
        users={r['username']:r['id'] for r in db.execute('SELECT id,username FROM users')}
        offices=list(db.execute('SELECT * FROM campus_offices ORDER BY id'));section=db.execute('SELECT section_id,course_id FROM students WHERE user_id=?',(users['2024-0002'],)).fetchone();section1=db.execute('SELECT section_id,course_id FROM students WHERE user_id=?',(users['2024-0001'],)).fetchone()
        schedule=dict(db.execute('SELECT * FROM schedules WHERE section_id=? AND teacher_id=(SELECT id FROM teachers WHERE user_id=?)',(section['section_id'],users['TCH001'])).fetchone())
    for i,office in enumerate(offices):
        page_post(owner,'/sims/modules/admin/workflow_settings.php',{'action':'office','id':office['id'],'name':office['name'],'reviewer_id':users['TCH002'] if i==1 else users['TCH001'],'active':'on'})
    other.request('/sims/modules/admin/workflow_settings.php',expected=403)
    clr=new_work(student,'clearance');path='/sims/modules/shared/workflows.php?kind=clearance&id='+str(clr)
    other.request(path,expected=404);parent.request(path,expected=404)
    def steps(id):
        with database(owner) as db:return [dict(r) for r in db.execute('SELECT * FROM campus_steps WHERE request_id=? ORDER BY position',(id,))]
    def update(client,kind,id,**values):return page_post(client,'/sims/modules/shared/workflows.php?kind='+kind+'&id='+str(id),{'revision':record(owner,'campus_requests',id)['revision'],**values})
    step=steps(clr)
    update(teacher,'clearance',clr,action='review',step_id=step[1]['id'],decision='Approved');assert steps(clr)[1]['status']=='Pending'
    update(teacher,'clearance',clr,action='review',step_id=step[0]['id'],decision='Approved')
    update(teacher2,'clearance',clr,action='review',step_id=step[1]['id'],decision='Returned',note='Please provide the school reference.')
    update(student,'clearance',clr,action='resubmit',note='Added the requested reference to my office submission.')
    assert steps(clr)[0]['status']=='Approved' and steps(clr)[1]['status']=='Pending'
    update(teacher2,'clearance',clr,action='review',step_id=step[1]['id'],decision='Approved')
    update(teacher,'clearance',clr,action='review',step_id=step[2]['id'],decision='Approved');assert record(owner,'campus_requests',clr)['status']=='Approved'
    update(student,'clearance',clr,action='cancel');assert record(owner,'campus_requests',clr)['status']=='Approved'
    print('PASS clearance office permissions, independent approvals, return/resubmission, and completed-request protection')
    page_post(owner,'/sims/modules/admin/workflow_settings.php',{'action':'form','name':'QA sequential form','route':str(offices[0]['id'])+','+str(offices[1]['id']),'active':'on'})
    with database(owner) as db:ft=db.execute('SELECT id FROM campus_form_types WHERE name="QA sequential form"').fetchone()[0]
    form=new_work(student,'form',form_type=ft);formsteps=steps(form)
    update(teacher2,'form',form,action='review',step_id=formsteps[1]['id'],decision='Approved');assert steps(form)[1]['status']=='Pending'
    update(teacher,'form',form,action='review',step_id=formsteps[0]['id'],decision='Approved')
    update(teacher2,'form',form,action='review',step_id=formsteps[1]['id'],decision='Approved');assert record(owner,'campus_requests',form)['status']=='Approved'
    concern=new_work(student,'helpdesk',title='QA support concern',office_id=offices[0]['id']);concernpath='/sims/modules/shared/workflows.php?kind=helpdesk&id='+str(concern)
    teacher2.request(concernpath,expected=404)
    update(teacher,'helpdesk',concern,action='message',note='QA private internal note',internal='1');assert b'QA private internal note' not in student.request(concernpath)[1]
    update(teacher,'helpdesk',concern,action='message',note='QA public staff reply');assert b'QA public staff reply' in student.request(concernpath)[1]
    revision=record(owner,'campus_requests',concern)['revision'];update(student,'helpdesk',concern,action='message',note='QA requester reply')
    page_post(teacher,concernpath,{'action':'review','revision':revision,'step_id':steps(concern)[0]['id'],'decision':'Approved'});assert record(owner,'campus_requests',concern)['status']=='In review'
    update(teacher,'helpdesk',concern,action='review',step_id=steps(concern)[0]['id'],decision='Approved');assert record(owner,'campus_requests',concern)['status']=='Resolved'
    print('PASS sequential forms, assigned helpdesk, internal-note privacy, replies, stale-action checks, and resolution')
    ann='/sims/modules/shared/announcements.php';title='QA restricted section notice '+uuid.uuid4().hex[:8]
    values={'title':title,'body':'Only the correct class should see this notice.','target_role':'Student','target_course_id':section['course_id'],'target_section_id':section['section_id']}
    page_post(teacher,ann,values);assert title.encode() in student.request('/portal')[1] and title.encode() not in other.request('/portal')[1]
    count=lambda:database(owner).execute('SELECT COUNT(*) FROM announcements').fetchone()[0]
    before=count();page_post(teacher2,ann,{**values,'title':'QA forged section','target_course_id':3,'target_section_id':3});assert count()==before
    page_post(teacher,ann,{**values,'target_role':'All','target_section_id':''});assert count()==before
    student.form(ann,values,419,False)
    print('PASS role/course/section targeting and teacher scope enforcement')
    attendance='/sims/modules/shared/rotating_attendance.php'
    result=page_post(teacher,attendance,{'action':'open','schedule_id':schedule['id']});sid=int(urllib.parse.parse_qs(urllib.parse.urlparse(result[3]).query)['session'][0]);wpath=attendance+'?session='+str(sid)
    teacher2.request(wpath,expected=404);other.request(wpath,expected=404)
    response=page_post(teacher,wpath,{'action':'rotate','session':sid});challenge_data=json.loads(response[1]);token=challenge_data['code']
    page_post(student,wpath,{'action':'checkin','session':sid,'code':'0'*32});
    with database(owner) as db:assert db.execute('SELECT COUNT(*) FROM attendance_window_entries WHERE window_id=?',(sid,)).fetchone()[0]==0
    time.sleep(max(0,challenge_data['expiresAt']-time.time())+1)
    expired=page_post(student,wpath,{'action':'checkin','session':sid,'code':token});assert b'expired or is invalid' in expired[1]
    current=json.loads(page_post(teacher,wpath,{'action':'rotate','session':sid})[1]);assert current['code']!=token;token=current['code']
    page_post(student,wpath,{'action':'checkin','session':sid,'code':token});page_post(student,wpath,{'action':'checkin','session':sid,'code':token})
    with database(owner) as db:assert db.execute('SELECT COUNT(*) FROM attendance_window_entries WHERE window_id=?',(sid,)).fetchone()[0]==1
    page_post(teacher,wpath,{'action':'close','session':sid});page_post(student,wpath,{'action':'checkin','session':sid,'code':token})
    assert b'closed' in student.request(wpath)[1]
    print('PASS assigned class windows, signed-in check-in, invalid/closed codes, and replay deduplication')
    ticket_response=student.form('/Queue/GetTicket',{'ServiceType':'Document Request','RequestType':'Other Document','PhoneNumber':'09121110002','StudentNumber':'2024-0002','PriorityType':'Regular'})
    url=urllib.parse.urlparse(ticket_response[3]);number=urllib.parse.parse_qs(url.query).get('id',[url.path.rsplit('/',1)[-1]])[0]
    owner.form('/Services/Progress',{'id':number,'stage':'Processing','note':'QA document'})
    owner.form('/Services/Progress',{'id':number,'stage':'Ready for pickup','note':'QA ready'})
    pdf=b'%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n%%EOF\n'
    student.multipart('/Services/RegisterIssued',{'id':number},'issued.pdf',pdf,403)
    owner.multipart('/Services/RegisterIssued',{'id':number},'issued.pdf',pdf)
    issued=backup(owner)['queue_data']['IssuedDocuments'][-1];assert issued['Sha256']==hashlib.sha256(pdf).hexdigest().upper()
    public=Client().request('/Verify/Document?code='+issued['Code'])[1];assert b'Registered document' in public and b'2024-0002' not in public and b'Maria Santos' not in public
    other.request('/Services/IssuedFile?documentId='+issued['Id'],expected=404)
    assert student.request('/Services/IssuedFile?documentId='+issued['Id'])[1]==pdf
    assert b'<svg' in student.request('/Services/IssuedQr?id='+number+'&documentId='+issued['Id'])[1]
    owner.form('/Services/RevokeIssued',{'id':number,'documentId':issued['Id'],'note':'QA registry replacement'})
    assert b'Registration revoked' in Client().request('/Verify/Document?code='+issued['Code'])[1]
    summary=json.loads(student.request('/Services/CampusOverview')[1]);assert len(summary['metrics'])==4 and 'closures' in summary and 'timeline' in summary
    print('PASS registered PDF fingerprint, public privacy, private downloads, QR, revocation, and unified dashboard feed')
    # End-to-end MFA: never print setup keys, codes, recovery material, or backup contents.
    security='/sims/modules/shared/security.php';enrolled=page_post(owner,security,{'action':'begin','password':'ADMIN'})[1].decode()
    secret=html.unescape(re.search(r'data-mfa-secret>([^<]+)',enrolled)[1])
    def totp(secret):
        digest=hmac.new(base64.b32decode(secret),struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest();offset=digest[-1]&15;return str((struct.unpack('>I',digest[offset:offset+4])[0]&0x7fffffff)%1000000).zfill(6)
    setup_code=totp(secret)
    enrolled=page_post(owner,security,{'action':'confirm','password':'ADMIN','code':setup_code})[1].decode();recovery=re.findall(r'data-recovery-code>([A-F0-9]+)',enrolled);assert len(recovery)==10
    with database(owner) as db:mfa=dict(db.execute('SELECT * FROM account_mfa WHERE user_id=?',(users['ADMIN'],)).fetchone());assert secret not in mfa['secret']
    second=Client();loginpage=second.request('/sims/login.php')[1];challenge=second.form('/sims/login.php',{'csrf':csrf(loginpage),'username':'ADMIN','password':'ADMIN'},csrf=False);assert challenge[3].endswith('/sims/mfa.php')
    assert second.request('/portal')[3].endswith('/sims/login.php')
    challenge=page_post(second,'/sims/mfa.php',{'code':setup_code});assert b'already-used' in challenge[1]
    challenge=page_post(second,'/sims/mfa.php',{'code':recovery[0]});assert challenge[3].endswith('/portal')
    archive=backup(owner)
    incomplete=dict(archive);incomplete.pop('security_material')
    page=owner.request('/sims/modules/admin/restore.php')[1]
    failed=owner.multipart('/sims/modules/admin/restore.php',{'csrf':csrf(page),'confirm_restore':'RESTORE'},'backup.json',json.dumps(incomplete).encode(),field='backup');assert b'missing its MFA security material' in failed[1]
    restore(owner,archive)
    owner=Client();page=owner.request('/sims/login.php')[1];owner.form('/sims/login.php',{'csrf':csrf(page),'username':'ADMIN','password':'ADMIN'},csrf=False)
    signed=page_post(owner,'/sims/mfa.php',{'code':recovery[1]});assert signed[3].endswith('/portal')
    owner.token=json.loads(owner.request('/Assistant/Context')[1])['token']
    with database(owner) as db:assert len(json.loads(db.execute('SELECT recovery_hashes FROM account_mfa WHERE user_id=?',(users['ADMIN'],)).fetchone()[0]))==8
    replaced=page_post(owner,security,{'action':'regenerate','password':'ADMIN','code':recovery[2]})[1].decode();replacement=re.findall(r'data-recovery-code>([A-F0-9]+)',replaced);assert len(replacement)==10
    denied=page_post(owner,security,{'action':'disable','password':'ADMIN','code':recovery[3]});assert b'valid authenticator' in denied[1]
    page_post(owner,security,{'action':'disable','password':'ADMIN','code':replacement[0]});assert not database(owner).execute('SELECT COUNT(*) FROM account_mfa').fetchone()[0]
    print('PASS encrypted authenticator setup, password+MFA challenge, TOTP replay rejection, one-use recovery, and MFA backup/restore')
finally:
    restore(owner,original)
    print('RESTORED original disposable campus workspace')

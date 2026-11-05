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
    fixture=backup(owner);db=sqlite3.connect(':memory:');db.deserialize(base64.b64decode(fixture['academic_database']));db.execute("UPDATE students SET course_id=(SELECT id FROM courses WHERE code='IT') WHERE user_id=(SELECT id FROM users WHERE username='2024-0002')");db.commit();fixture['academic_database']=base64.b64encode(db.serialize()).decode();db.close();restore(owner,fixture);owner=login('ADMIN','ADMIN')
    student=login('2024-0002','student123');other=login('2024-0001','student123');parent=login('PARENT001','parent123');teacher=login('TCH001','teacher123');teacher2=login('TCH002','teacher123')
    with database(owner) as db:
        year=db.execute('SELECT id FROM school_years WHERE is_active=1').fetchone()[0]
        offices=list(db.execute('SELECT * FROM campus_offices ORDER BY id'))
        users={r['username']:r['id'] for r in db.execute('SELECT * FROM users')}
        course=db.execute('SELECT course_id FROM students WHERE user_id=?',(users['2024-0002'],)).fetchone()[0]
    config='/sims/modules/admin/enrollment_requirements.php';base='/sims/modules/shared/enrollment.php'
    student.request(config,expected=403)
    no_rules=page_post(student,base,{'action':'start'});assert b'has not published enrollment requirements' in no_rules[1]
    for office,reviewer in [(offices[0],users['TCH001']),(offices[1],users['TCH002'])]:page_post(owner,'/sims/modules/admin/workflow_settings.php',{'action':'office','id':office['id'],'name':office['name'],'reviewer_id':reviewer,'active':'on'})
    def requirement(title,office,course_id='',id=0):return page_post(owner,config,{'id':id,'year_id':year,'course_id':course_id,'office_id':office,'title':title,'instructions':'QA school-approved checklist instructions.','required':'on','active':'on'})
    requirement('QA global requirement',offices[0]['id'])
    requirement('QA course-specific requirement',offices[1]['id'],course)
    requirement('QA other-course requirement',offices[0]['id'],1)
    def start(client):
        r=page_post(client,base,{'action':'start'});return int(urllib.parse.parse_qs(urllib.parse.urlparse(r[3]).query)['id'][0])
    id=start(student);assert start(student)==id
    def items(checklist=id):
        with database(owner) as db:return [dict(r) for r in db.execute('SELECT * FROM enrollment_items WHERE checklist_id=? ORDER BY id',(checklist,))]
    assert len(items())==2 and all('other-course' not in i['title'] for i in items())
    other.request(base+'?id='+str(id),expected=404);parent.request(base+'?id='+str(id),expected=404)
    linked=start(other);assert parent.request(base+'?id='+str(linked))[0]==200
    assert b'Only the student' in page_post(parent,base+'?id='+str(linked),{'action':'prepare','revision':0})[1]
    path=base+'?id='+str(id)
    student.form(path,{'action':'prepare'},419,False)
    def update(client,**values):return page_post(client,path,{'revision':record(owner,'enrollment_checklists',id)['revision'],**values})
    first,second=items();update(student,action='prepare',**{'prepared[]':[first['id'],second['id']]});assert all(i['prepared'] for i in items()) and all(i['status']=='Pending' for i in items())
    before=record(owner,'enrollment_checklists',id)['revision']
    update(student,action='review',item_id=first['id'],status='Verified');assert all(i['status']=='Pending' for i in items())
    update(teacher,action='review',item_id=second['id'],status='Verified');assert items()[1]['status']=='Pending'
    update(teacher,action='review',item_id=first['id'],status='Returned',note='');assert items()[0]['status']=='Pending'
    update(teacher,action='review',item_id=first['id'],status='Returned',note='Please prepare the corrected school form.');assert items()[0]['status']=='Returned' and not items()[0]['prepared']
    update(student,action='prepare',**{'prepared[]':[first['id'],second['id']]});assert items()[0]['status']=='Pending'
    stale=page_post(teacher,path,{'action':'review','revision':before,'item_id':first['id'],'status':'Verified'});assert b'This checklist changed' in stale[1]
    update(teacher,action='review',item_id=first['id'],status='Verified');update(teacher2,action='review',item_id=second['id'],status='Verified')
    update(student,action='prepare');assert all(i['status']=='Verified' and i['prepared'] for i in items())
    assert b'Required checklist verified' in student.request(path)[1] and b'does not confirm admission' in student.request(path)[1]
    requirement('QA updated future title',offices[0]['id'],id=first['requirement_id']);assert items()[0]['title']=='QA global requirement'
    archive=backup(owner);restore(owner,archive);owner=login('ADMIN','ADMIN');student=login('2024-0002','student123')
    assert all(i['status']=='Verified' for i in items()) and b'Please prepare the corrected school form.' in student.request(path)[1]
    print('PASS school publication/course targeting, idempotent start, ownership/parent access, CSRF, preparation vs verification, assigned review, correction history, stale checks, immutable snapshots, and backup/restore')
finally:
    restore(owner,original)
    print('RESTORED original disposable enrollment workspace')

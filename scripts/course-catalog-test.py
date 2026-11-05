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
    with database(owner) as db:
        offered=[r['code'] for r in db.execute('SELECT code FROM courses WHERE is_offered=1 ORDER BY code')]
        assert offered==['BSHM','BSIT','HRS','IT']
        official={r['code']:r['id'] for r in db.execute('SELECT * FROM courses WHERE is_offered=1')};assert official['IT']!=official['BSIT']
        legacy=dict(db.execute('SELECT * FROM courses WHERE code="BSCS"').fetchone())
        student=dict(db.execute('SELECT * FROM students WHERE course_id=? LIMIT 1',(legacy['id'],)).fetchone())
        other=dict(db.execute('SELECT * FROM students WHERE course_id=? LIMIT 1',(official['BSIT'],)).fetchone())
    def course_options(body):
        select=re.search(r'<select[^>]+id="field-course_id"[^>]*>(.*?)</select>',body.decode(),re.S)[1]
        return [html.unescape(text) for text in re.findall(r'<option[^>]+value="[1-9][0-9]*"[^>]*>(.*?)</option>',select)]
    new=owner.request('/sims/modules/admin/manage.php?entity=students')[1];assert set(course_options(new))=={'HRS','IT','BSHM','BSIT'}
    edit='/sims/modules/admin/manage.php?entity=students&edit='+str(student['id']);assert any('BSCS (historical' in label for label in course_options(owner.request(edit)[1]))
    fields=['user_id','student_no','course_id','section_id','year_level','birthdate','gender','address','phone']
    payload={key:student[key] if student[key] is not None else '' for key in fields}
    page_post(owner,edit,{'action':'update','id':student['id'],**payload,'address':'QA historical course preserved'})
    assert record(owner,'students',student['id'])['course_id']==legacy['id']
    other_payload={key:other[key] if other[key] is not None else '' for key in fields}
    denied=page_post(owner,'/sims/modules/admin/manage.php?entity=students&edit='+str(other['id']),{'action':'update','id':other['id'],**other_payload,'course_id':legacy['id']})
    assert b'Choose a valid Course' in denied[1] and record(owner,'students',other['id'])['course_id']==official['BSIT']
    rejected=page_post(owner,'/sims/modules/admin/manage.php?entity=courses',{'action':'create','code':'BSA','name':'Unsupported course','description':'QA'})
    assert b'School course codes are HRS' in rejected[1]
    assigned=page_post(owner,edit,{'action':'update','id':student['id'],**payload,'course_id':official['IT']});assert record(owner,'students',student['id'])['course_id']==official['IT']
    restore(owner,backup(owner));owner=login('ADMIN','ADMIN')
    with database(owner) as db:assert {r[0] for r in db.execute('SELECT code FROM courses WHERE is_offered=1')}=={'HRS','IT','BSHM','BSIT'}
    print('PASS exact four offered courses, separate IT/BSIT, filtered choices, preserved historical assignments, blocked new legacy assignments, explicit reassignment, and backup/restore')
finally:
    restore(owner,original)
    print('RESTORED original disposable course workspace')

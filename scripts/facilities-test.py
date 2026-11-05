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
import datetime
owner=login('ADMIN','ADMIN');original=backup(owner)
try:
    student=login('2024-0002','student123');other=login('2024-0001','student123');teacher=login('TCH001','teacher123');unassigned=login('TCH002','teacher123');parent=login('PARENT001','parent123')
    base='/sims/modules/shared/facilities.php';config='/sims/modules/admin/facilities.php'
    student.request(config,expected=403);parent.request(base,expected=403)
    with database(owner) as db:
        office=dict(db.execute('SELECT * FROM campus_offices ORDER BY id LIMIT 1').fetchone());reviewer=db.execute('SELECT id FROM users WHERE username="TCH001"').fetchone()[0]
    page_post(owner,'/sims/modules/admin/workflow_settings.php',{'action':'office','id':office['id'],'name':office['name'],'reviewer_id':reviewer,'active':'on'})
    page_post(owner,config,{'name':'QA school laboratory','kind':'Laboratory','location':'QA demo location','capacity':20,'office_id':office['id'],'approval_required':'on','active':'on'})
    with database(owner) as db:resource=dict(db.execute('SELECT * FROM campus_resources WHERE name="QA school laboratory"').fetchone())
    date=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8)))+datetime.timedelta(days=2)
    def payload(day=date,hour=9,end=11,key=None):return {'action':'create','resource_id':resource['id'],'starts_at':day.replace(hour=hour,minute=0,second=0).strftime('%Y-%m-%dT%H:%M'),'ends_at':day.replace(hour=end,minute=0,second=0).strftime('%Y-%m-%dT%H:%M'),'purpose':'QA private meeting purpose','request_key':key or uuid.uuid4().hex}
    values=payload();created=page_post(student,base,values);id=int(urllib.parse.parse_qs(urllib.parse.urlparse(created[3]).query)['id'][0]);assert record(owner,'campus_bookings',id)['status']=='Pending'
    replay=page_post(student,base,values);assert int(urllib.parse.parse_qs(urllib.parse.urlparse(replay[3]).query)['id'][0])==id
    conflict=page_post(other,base,payload());assert b'already has a pending or approved reservation' in conflict[1]
    path=base+'?id='+str(id);other.request(path,expected=404);unassigned.request(path,expected=404)
    assert b'QA private meeting purpose' not in other.request(base+'?date='+date.strftime('%Y-%m-%d'))[1]
    student.form(path,{'action':'cancel'},419,False)
    denied=page_post(student,path,{'action':'review','revision':0,'status':'Approved'});assert record(owner,'campus_bookings',id)['status']=='Pending'
    page_post(teacher,path,{'action':'review','revision':0,'status':'Approved'});assert record(owner,'campus_bookings',id)['status']=='Approved'
    stale=page_post(teacher,path,{'action':'review','revision':0,'status':'Cancelled','note':'QA cancellation'});assert b'This reservation changed' in stale[1]
    page_post(student,path,{'action':'cancel','revision':1});assert record(owner,'campus_bookings',id)['status']=='Cancelled'
    replacement=page_post(other,base,payload());assert '?id=' in replacement[3]
    adjacent=page_post(student,base,payload(hour=11,end=12));assert '?id=' in adjacent[3]
    bad=page_post(student,base,payload(hour=15,end=14));assert b'duration of up to eight hours' in bad[1]
    race_date=date+datetime.timedelta(days=1)
    def reserve(client):return page_post(client,base,payload(day=race_date))
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(reserve,[student,other]))
    assert sum('?id=' in r[3] for r in results)==1
    with database(owner) as db:assert db.execute('SELECT COUNT(*) FROM campus_bookings WHERE resource_id=? AND status IN("Pending","Approved")',(resource['id'],)).fetchone()[0]==3
    current=record(owner,'campus_resources',resource['id']);page_post(owner,config,{'id':resource['id'],'revision':current['revision'],'name':resource['name'],'kind':resource['kind'],'location':resource['location'],'capacity':20,'office_id':office['id'],'approval_required':'on'})
    unavailable=page_post(student,base,payload(day=date+datetime.timedelta(days=3)));assert b'unavailable for new reservations' in unavailable[1]
    archive=backup(owner);restore(owner,archive);owner=login('ADMIN','ADMIN');other=login('2024-0001','student123')
    other.request(replacement[3].replace(BASE,''));assert not record(owner,'campus_resources',resource['id'])['active']
    print('PASS configuration/booking permissions, private purpose, idempotence, approval/cancellation, stale checks, conflict races, adjacent slots, invalid times, resource archive, and full backup/restore')
finally:
    restore(owner,original)
    print('RESTORED original disposable facilities workspace')

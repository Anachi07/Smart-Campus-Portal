"""Survey permissions, privacy, validation, concurrency and backup regression. Disposable demo data only."""
import os, sys, json, uuid, re, html, base64, urllib.request, urllib.parse, urllib.error, http.cookiejar, concurrent.futures, sqlite3
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
def database(owner):
    connection=sqlite3.connect(':memory:');connection.deserialize(base64.b64decode(backup(owner)['academic_database']));connection.row_factory=sqlite3.Row;return connection
def record(owner,table,id):
    with database(owner) as db:return dict(db.execute('SELECT * FROM '+table+' WHERE id=?',(id,)).fetchone())
def page_post(client,path,values,expected=200):
    page=client.request(path)[1];values={'csrf':csrf(page),**values};return client.form(path,values,expected,False)
owner=login('ADMIN','ADMIN'); original=backup(owner);path='/sims/modules/shared/surveys.php'
def make(**values):
    defaults=dict(action='create',title='QA feedback '+uuid.uuid4().hex[:8],description='School service feedback',privacy='Anonymous',audience='All',questions='rating|Service quality?\nchoice|Session preference?|Morning|Afternoon\ntext|What can improve?'); defaults.update(values)
    result=page_post(owner,path,defaults)
    return int(urllib.parse.parse_qs(urllib.parse.urlparse(result[3]).query)['id'][0])
def post(c,id,**values):return page_post(c,path+'?id='+str(id),values)
def count(id):
    with database(owner) as db:return db.execute('SELECT COUNT(*) FROM campus_survey_responses WHERE survey_id=?',(id,)).fetchone()[0]
answers={'answers[0]':'5','answers[1]':'Morning','answers[2]':'QA private response <script>unsafe</script>'}
try:
    student=login('2024-0001','student123');other=login('2024-0002','student123');teacher=login('TCH001','teacher123');parent=login('PARENT001','parent123')
    student.request(path+'?results=1',expected=403)
    assert b'Administrator access required' in page_post(student,path,dict(action='create',title='Forbidden'))[1]
    id=make();student.request(path+'?id='+str(id),expected=404)
    post(owner,id,action='publish')
    # Invalid and missing answers cannot consume participation.
    post(student,id,action='submit',**{**answers,'answers[0]':'6'});assert count(id)==0
    post(student,id,action='submit',**{'answers[0]':'4'});assert count(id)==0
    post(student,id,action='submit',**{**answers,'answers[1]':'Forged'});assert count(id)==0
    # Two independently authenticated sessions race for the same account.
    second=login('2024-0001','student123')
    with concurrent.futures.ThreadPoolExecutor(2) as pool:
        results=list(pool.map(lambda c:post(c,id,action='submit',**answers),[student,second]))
    assert count(id)==1
    assert b'QA private response' not in owner.request(path+'?id='+str(id))[1]
    post(other,id,action='submit',**answers);post(teacher,id,action='submit',**answers);assert count(id)==3
    # Anonymous results stay hidden while open, even after reaching the threshold.
    assert b'QA private response' not in owner.request(path+'?id='+str(id))[1]
    post(owner,id,action='close');body=owner.request(path+'?id='+str(id))[1]
    assert b'QA private response &lt;script&gt;' in body and b'<script>unsafe</script>' not in body
    assert b'QA private response' not in student.request(path+'?id='+str(id))[1]
    with database(owner) as db:
        assert db.execute('SELECT COUNT(*) FROM campus_survey_responses WHERE survey_id=? AND user_id IS NOT NULL',(id,)).fetchone()[0]==0
        assert db.execute('PRAGMA table_info(campus_survey_participation)').fetchall()[0]['name']=='survey_id'
    post(parent,id,action='submit',**answers);assert count(id)==3
    post(owner,id,action='publish');assert record(owner,'campus_surveys',id)['state']=='Closed'
    # Privacy/configuration tampering is never applied by lifecycle actions.
    small=make();post(owner,small,action='publish',privacy='Identified',questions='text|Changed question?')
    assert record(owner,'campus_surveys',small)['privacy']=='Anonymous'
    post(student,small,action='submit',**answers);post(owner,small,action='close')
    assert b'QA private response' not in owner.request(path+'?id='+str(small))[1]
    print('PASS validation, concurrent duplicate prevention, anonymous separation, threshold, escaping, close and privacy freeze')
    # Identified reports and actual school course / linked-parent eligibility.
    with database(owner) as db:
        course=db.execute("SELECT id FROM courses WHERE code='BSIT'").fetchone()[0]
        section=db.execute("SELECT section_id FROM students WHERE student_no='2024-0001'").fetchone()[0]
    result=page_post(owner,path,dict(action='create',title='QA identified',description='',privacy='Identified',audience='Student',course_id=course,section_id=section,questions='rating|Service quality?'))
    identified=int(urllib.parse.parse_qs(urllib.parse.urlparse(result[3]).query)['id'][0]);post(owner,identified,action='publish')
    other.request(path+'?id='+str(identified),expected=404);teacher.request(path+'?id='+str(identified),expected=404)
    post(student,identified,action='submit',**{'answers[0]':'4'})
    with database(owner) as db:
        name=db.execute("SELECT full_name FROM users WHERE username='2024-0001'").fetchone()[0]
        assert db.execute('SELECT user_id FROM campus_survey_responses WHERE survey_id=?',(identified,)).fetchone()[0] is not None
    assert name.encode() in owner.request(path+'?id='+str(identified))[1]
    result=page_post(owner,path,dict(action='create',title='QA parent audience',description='',privacy='Anonymous',audience='Parent',course_id=course,section_id=section,questions='text|Any suggestions?'))
    pid=int(urllib.parse.parse_qs(urllib.parse.urlparse(result[3]).query)['id'][0]);post(owner,pid,action='publish')
    parent.request(path+'?id='+str(pid));student.request(path+'?id='+str(pid),expected=404)
    print('PASS identified staff reports, course/section targeting and linked-parent audience')
    saved=backup(owner)
    # Restore must reject identity leakage into anonymous response rows.
    corrupt=json.loads(json.dumps(saved));probe=sqlite3.connect(':memory:');probe.deserialize(base64.b64decode(corrupt['academic_database']))
    probe.execute('UPDATE campus_survey_responses SET user_id=(SELECT id FROM users WHERE username=?) WHERE id=(SELECT id FROM campus_survey_responses WHERE survey_id=? LIMIT 1)',('2024-0001',id));probe.commit();corrupt['academic_database']=base64.b64encode(probe.serialize()).decode();probe.close()
    restore_page=owner.request('/sims/modules/admin/restore.php')[1]
    rejected=owner.multipart('/sims/modules/admin/restore.php',{'csrf':csrf(restore_page),'confirm_restore':'RESTORE'},'invalid.json',json.dumps(corrupt).encode(),field='backup')
    assert b'Invalid survey response privacy' in rejected[1] and 'restored=1' not in rejected[3]
    assert count(id)==3
    restore(owner,saved);owner=login('ADMIN','ADMIN');assert count(id)==3
    print('PASS survey backup/restore round trip')
finally:
    owner=login('ADMIN','ADMIN');restore(owner,original)
print('PASS survey integration suite; original disposable snapshot restored')

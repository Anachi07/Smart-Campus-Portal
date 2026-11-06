"""Chatbot public school facts and enrollment-draft privacy regression. Disposable demo data only."""
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
owner=login('ADMIN','ADMIN');original=backup(owner);guest=Client()
ctx=json.loads(guest.request('/Assistant/Context')[1]);guest.token=ctx['token'];assert not ctx['aiConfigured'],'Use a local demo without model provider keys.'
def ask(question):return json.loads(guest.request('/Assistant/Chat',json.dumps({'message':question,'history':[]}).encode(),{'Content-Type':'application/json','RequestVerificationToken':guest.token})[1])
path='/sims/modules/admin/settings.php'
try:
    guest.request('/sims/assistant-data.php',expected=404)
    cases=[('Anong courses ang offered?',-101,['HRS','IT','BSHM','BSIT']),('Saan ang ICAS Sucat?',-102,['8347 CAP Building','Nissan Sucat']),('Contact details ng ICAS Sucat?',-103,['0995-922-2204','facebook.com/icasparanaque']),('Paano mag enroll?',-104,['school']),('How do I get a portal account?',-107,['Super Admin']),('How can I change or recover my password?',-4,['My profile']),('Paano ireset account?',-4,['Super Admin']),('Magkano tuition?',-3,['Cashier'])]
    for question,id,words in cases:
        reply=ask(question);assert reply['mode']=='guide' and reply['sources'][0]['id']==id,(question,reply)
        assert all(word in reply['answer'] for word in words),(question,reply['answer'])
    page_post(owner,path,{'settings[school_admissions_status]':'Draft','settings[school_admissions_new]':'QA HIDDEN NEW REQUIREMENT','settings[school_admissions_transferee]':'QA HIDDEN TRANSFER REQUIREMENT','settings[school_admissions_steps]':'QA HIDDEN ENROLL STEP'})
    for question in ['Paano mag enroll?','New student enrollment requirements','Transferee enrollment requirements']:
        reply=ask(question);assert 'QA HIDDEN' not in json.dumps(reply) and 'published' in reply['answer']
    page_post(owner,path,{'settings[school_admissions_status]':'Published','settings[school_admissions_new]':'QA approved new requirement','settings[school_admissions_transferee]':'QA approved transfer requirement','settings[school_admissions_steps]':'QA approved step one\nQA approved step two'})
    for question,expected in [('Paano mag enroll?','QA approved step one'),('New student enrollment requirements','QA approved new requirement'),('Transferee enrollment requirements','QA approved transfer requirement')]:
        reply=ask(question);assert expected in reply['answer'],(question,reply)
    page_post(owner,path,{'settings[school_mobile]':'0995-000-1111','settings[school_office_hours]':'QA public office hours'})
    reply=ask('Contact details ng ICAS Sucat?');assert '0995-000-1111' in reply['answer'] and 'QA public office hours' in reply['answer'] and '0995-922-2204' not in reply['answer']
    page_post(owner,path,{'settings[school_admissions_status]':'Draft'})
    reply=ask('Transferee enrollment requirements');assert 'QA approved' not in json.dumps(reply)
    print('PASS anonymous chatbot courses/location/contact/account answers, internal-route protection, hidden drafts, published enrollment and immediate settings updates')
finally:
    restore(owner,original)
print('PASS original disposable snapshot restored')

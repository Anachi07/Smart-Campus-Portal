"""Public school information publishing, validation and privacy regression. Disposable demo data only."""
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
owner=login('ADMIN','ADMIN');original=backup(owner);path='/sims/modules/admin/settings.php';public=Client()
try:
    student=login('2024-0001','student123');student.request(path,expected=403)
    # Confirm only the user-supplied school identity and four courses are published by default.
    home=public.request('/')[1].decode()
    assert 'Infotech College of Arts and Sciences (ICAS)' in home and 'Sucat branch' in home
    assert all('>'+code+'</h3>' in home for code in ['HRS','IT','BSHM','BSIT'])
    assert 'Learning connected' not in home and 'portal-preview' not in home
    assert 'Our mission' not in home
    assert '8347 CAP Building' in home and '0995-922-2204' in home and 'facebook.com/icasparanaque' in home
    owner.form(path,{'settings[school_public_name]':'Forbidden without CSRF'},expected=419,csrf=False)
    valid={'settings[school_name]':'ICAS Philippines','settings[academic_year]':'2026-2027','settings[school_public_name]':'Infotech College of Arts and Sciences (ICAS)','settings[school_branch]':'Sucat branch','settings[school_about]':'QA approved school description <script>unsafe</script>','settings[school_mission]':'QA approved mission','settings[school_vision]':'QA approved vision','settings[school_address]':'QA campus address\nSecond address line','settings[school_email]':'office@example.test','settings[school_mobile]':'0995-000-0000','settings[school_phone]':'QA public number','settings[school_facebook]':'https://www.facebook.com/example-school','settings[school_website]':'https://example.test','settings[school_office_hours]':'QA office hours','settings[school_hrs_name]':'QA HRS official title','settings[school_it_name]':'QA IT official title'}
    page_post(owner,path,valid);home=public.request('/')[1].decode()
    assert 'QA approved school description &lt;script&gt;unsafe&lt;/script&gt;' in home and '<script>unsafe</script>' not in home
    assert 'QA campus address\nSecond address line' in home and 'mailto:office@example.test' in home
    assert 'QA approved mission' in home and 'QA approved vision' in home and 'QA HRS official title' in home
    assert 'QA public number' in home and 'https://www.facebook.com/example-school' in home
    # Older clients must not erase newly published public school information.
    page_post(owner,path,{'settings[school_name]':'ICAS Philippines','settings[academic_year]':'2027-2028'})
    assert 'QA approved mission' in public.request('/')[1].decode()
    for key,value in [('school_facebook','javascript:alert(1)'),('school_website','https://user:password@example.test'),('school_email','bad-email'),('school_public_name',''),('school_address','x'*501)]:
        bad={**valid,'settings['+key+']':value};result=page_post(owner,path,bad)
        assert b'alert-danger' in result[1]
        assert 'QA approved mission' in public.request('/')[1].decode()
    # Removing optional fields removes their public sections and links.
    blanks={k:('' if k not in ['settings[school_name]','settings[academic_year]','settings[school_public_name]','settings[school_branch]'] else v) for k,v in valid.items()}
    page_post(owner,path,blanks);home=public.request('/')[1].decode()
    assert 'QA approved mission' not in home and 'Campus address' not in home and 'office@example.test' not in home
    print('PASS homepage identity/programs, approved copy, escaping, URL/email/length validation, blank-field hiding and older-client compatibility')
finally:
    restore(owner,original)
print('PASS public settings permission/CSRF checks; original disposable snapshot restored')

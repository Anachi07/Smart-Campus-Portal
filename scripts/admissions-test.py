"""Public enrollment information draft/publish and validation regression. Disposable demo data only."""
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
fields={'settings[school_admissions_status]':'Draft','settings[school_admissions_new]':'QA original document\n\nQA student reference','settings[school_admissions_transferee]':'QA transfer reference','settings[school_admissions_steps]':'QA visit the school office\nQA complete the school assessment','settings[school_admissions_notes]':'QA approved note <script>unsafe</script>'}
def save(**updates):return page_post(owner,path,{**fields,**updates})
try:
    student=login('2024-0001','student123');student.request(path,expected=403)
    owner.form(path,fields,expected=419,csrf=False)
    save();home=public.request('/')[1].decode();assert 'id="enrollment"' not in home and 'QA original document' not in home
    rejected=save(**{'settings[school_admissions_status]':'Published','settings[school_admissions_steps]':''})
    assert b'Before publishing' in rejected[1] and 'QA original document' not in public.request('/')[1].decode()
    save(**{'settings[school_admissions_status]':'Published'});home=public.request('/')[1].decode()
    assert 'id="enrollment"' in home and 'QA original document' in home and 'QA transfer reference' in home
    assert home.index('QA visit the school office')<home.index('QA complete the school assessment')
    assert 'QA approved note &lt;script&gt;unsafe&lt;/script&gt;' in home and '<script>unsafe</script>' not in home
    page_post(owner,path,{'settings[school_name]':'ICAS Philippines','settings[academic_year]':'2026-2027'})
    assert 'QA original document' in public.request('/')[1].decode()
    # Validation is atomic: invalid lists/status cannot replace the published content.
    for bad in [{'settings[school_admissions_steps]':'x'*501},{'settings[school_admissions_new]':'\n'.join(['item']*31)},{'settings[school_admissions_status]':'Unexpected'},{'settings[school_admissions_new]':'','settings[school_admissions_transferee]':''}]:
        rejected=save(**{'settings[school_admissions_status]':'Published',**bad});assert b'alert-danger' in rejected[1]
        assert 'QA original document' in public.request('/')[1].decode()
    saved=backup(owner);restore(owner,saved);owner=login('ADMIN','ADMIN');assert 'QA original document' in public.request('/')[1].decode()
    save(**{'settings[school_admissions_status]':'Published','settings[school_admissions_transferee]':''})
    home=public.request('/')[1].decode();assert 'Please contact the school for the requirements for this applicant type.' in home
    save();home=public.request('/')[1].decode();assert 'id="enrollment"' not in home and 'QA original document' not in home
    print('PASS enrollment draft privacy, publish completeness, lists/order, escaped notes, permissions/CSRF, atomic validation, partial applicant information and backup restore')
finally:
    owner=login('ADMIN','ADMIN');restore(owner,original)
print('PASS original disposable snapshot restored')

"""Public campus photo uploads, validation and paired-backup regression. Disposable demo data only."""
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
from pathlib import Path
owner=login('ADMIN','ADMIN');original=backup(owner);path='/sims/modules/admin/settings.php';public=Client()
fixture=(Path(__file__).resolve().parent.parent/'public/pwa/icon-512.png').read_bytes()
def upload(client,slot,content,filename='fixture.png'):
    token=csrf(client.request(path)[1]);return client.multipart(path,{'csrf':token,'photo_caption_'+str(slot):'QA campus photo '+str(slot)},filename,content,field='school_photo_'+str(slot))
try:
    for slot in [1,2,3]:upload(owner,slot,fixture)
    body=public.request('/')[1];assert b'id="campus-photos"' in body and b'QA campus photo 2' in body
    with database(owner) as db:
        values=dict(db.execute("SELECT key,value FROM settings WHERE key LIKE 'school_photo_%'"));assert values['school_photo_1'].startswith('data:image/png;base64,')
    for content,filename in [(b'<svg xmlns="http://www.w3.org/2000/svg"></svg>','bad.svg'),(b'not a photo','bad.png'),(fixture+b'x'*768000,'large.png')]:
        result=upload(owner,1,content,filename);assert b'alert-danger' in result[1]
        with database(owner) as db:assert db.execute("SELECT value FROM settings WHERE key='school_photo_1'").fetchone()[0]==values['school_photo_1']
    page_post(owner,path,{'photo_caption_3':'<script>caption</script>'});body=public.request('/')[1];assert b'&lt;script&gt;caption&lt;/script&gt;' in body and b'<script>caption</script>' not in body
    saved=backup(owner);restore(owner,saved);owner=login('ADMIN','ADMIN');assert b'id="campus-photos"' in public.request('/')[1]
    for slot in [1,2,3]:page_post(owner,path,{'remove_photo_'+str(slot):'1'})
    assert b'id="campus-photos"' not in public.request('/')[1]
    print('PASS original photo upload, public gallery/hero, invalid/oversized image rejection, backup restore and removal')
finally:
    owner=login('ADMIN','ADMIN');restore(owner,original)
print('PASS original disposable snapshot restored; no fixture photos retained')

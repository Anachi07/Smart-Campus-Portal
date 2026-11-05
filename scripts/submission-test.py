"""Private requirement upload/review and large-backup regression. Disposable demo data only."""
import os, sys, json, uuid, re, html, base64, urllib.request, urllib.parse, urllib.error, http.cookiejar, concurrent.futures
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
owner=login('ADMIN','ADMIN');original=backup(owner)
try:
    student=login('2024-0002','student123');other=login('2024-0001','student123')
    owner.form('/Services/SaveRequirement',{'ServiceType':'Document Request','RequestType':'Other Document','Title':'QA private upload','Required':'true','Active':'true'})
    requirement=json.loads(student.request('/Services/Checklist?serviceType=Document%20Request&requestType=Other%20Document')[1])['requirements'][0]['id']
    number=create(student)
    def ticket(): return next(t for t in backup(owner)['queue_data']['Tickets'] if t['TicketNumber']==number)
    def submissions(): return sorted([s for s in backup(owner)['queue_data'].get('RequirementSubmissions',[]) if s['TicketNumber']==number],key=lambda s:s['Version'])
    def upload(client,version,name,content,expected=200,csrf=True):
        return client.multipart('/Services/UploadRequirement',{'id':number,'requirementId':requirement,'expectedVersion':version},name,content,expected,csrf)
    def review(submission,decision,note='',client=owner,expected=200):
        return client.form('/Services/ReviewRequirement',{'id':number,'submissionId':submission['Id'],'expectedVersion':submission['Version'],'decision':decision,'note':note},expected)
    jpeg=base64.b64decode('/9j/4AAQSkZJRgABAQAAAQABAAD/4gHYSUNDX1BST0ZJTEUAAQEAAAHIAAAAAAQwAABtbnRyUkdCIFhZWiAH4AABAAEAAAAAAABhY3NwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAA9tYAAQAAAADTLQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAlkZXNjAAAA8AAAACRyWFlaAAABFAAAABRnWFlaAAABKAAAABRiWFlaAAABPAAAABR3dHB0AAABUAAAABRyVFJDAAABZAAAAChnVFJDAAABZAAAAChiVFJDAAABZAAAAChjcHJ0AAABjAAAADxtbHVjAAAAAAAAAAEAAAAMZW5VUwAAAAgAAAAcAHMAUgBHAEJYWVogAAAAAAAAb6IAADj1AAADkFhZWiAAAAAAAABimQAAt4UAABjaWFlaIAAAAAAAACSgAAAPhAAAts9YWVogAAAAAAAA9tYAAQAAAADTLXBhcmEAAAAAAAQAAAACZmYAAPKnAAANWQAAE9AAAApbAAAAAAAAAABtbHVjAAAAAAAAAAEAAAAMZW5VUwAAACAAAAAcAEcAbwBvAGcAbABlACAASQBuAGMALgAgADIAMAAxADb/2wBDAAMCAgICAgMCAgIDAwMDBAYEBAQEBAgGBgUGCQgKCgkICQkKDA8MCgsOCwkJDRENDg8QEBEQCgwSExIQEw8QEBD/2wBDAQMDAwQDBAgEBAgQCwkLEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBD/wAARCAACAAIDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAn/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFAEBAAAAAAAAAAAAAAAAAAAAAP/EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAMAwEAAhEDEQA/AJVAA//Z')
    from pathlib import Path
    png=(Path(__file__).resolve().parent.parent/'public/pwa/icon-192.png').read_bytes()
    pdf=b'%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n%%EOF\n'
    upload(other,0,'private.pdf',pdf,404)
    upload(student,0,'private.pdf',pdf,400,False)
    upload(student,0,'program.php',pdf);assert not submissions()
    upload(student,0,'fake.png',pdf);assert not submissions()
    upload(student,0,'oversize.pdf',b'x'*(2*1024*1024+1));assert not submissions()
    upload(student,0,'private.pdf',pdf);first=submissions()[0]
    assert first['Status']=='Pending review' and not ticket()['Checklist'][0]['Verified']
    download=student.request('/Services/RequirementFile?submissionId='+first['Id'])
    assert download[1]==pdf and 'attachment;' in download[2]['Content-Disposition'] and 'sandbox' in download[2]['Content-Security-Policy']
    assert download[2]['Cache-Control']=='no-store' and download[2]['X-Content-Type-Options']=='nosniff'
    other.request('/Services/RequirementFile?submissionId='+first['Id'],expected=404)
    Client().request('/Services/RequirementFile?submissionId='+first['Id'],expected=401)
    review(first,'accept',client=student,expected=403)
    owner.form('/Services/ReviewRequirement',{'id':number,'submissionId':first['Id'],'expectedVersion':1,'decision':'accept'},400,False)
    owner.form('/Services/Verify',{'id':number,'verified':[requirement]});assert not ticket()['Checklist'][0]['Verified']
    review(first,'return');assert submissions()[0]['Status']=='Pending review'
    review(first,'return','Please replace the blurred scan. <img src=x onerror=alert(1)>')
    body=student.request('/Services/Request?id='+number)[1].decode();assert '&lt;img' in body and '<img src=x onerror=' not in body
    upload(student,0,'stale.pdf',pdf);assert len(submissions())==1
    upload(student,1,'corrected.png',png);second=submissions()[1]
    review(first,'accept');assert submissions()[1]['Status']=='Pending review'
    review(second,'accept','Clear copy accepted.');assert ticket()['Checklist'][0]['Verified']
    upload(student,2,'replacement.jpg',jpeg);assert not ticket()['Checklist'][0]['Verified']
    owner.form('/Services/Progress',{'id':number,'stage':'Processing','note':'Started'})
    owner.form('/Services/Progress',{'id':number,'stage':'Ready for pickup'});assert ticket()['DocumentStatus']=='Processing'
    latest=submissions()[-1];review(latest,'accept')
    owner.form('/Services/Progress',{'id':number,'stage':'Ready for pickup'});assert ticket()['DocumentStatus']=='Ready for pickup'
    upload(student,3,'late.pdf',pdf);assert len(submissions())==3
    owner.form('/Services/Progress',{'id':number,'stage':'Released','note':'QA recipient checked in person'})
    live=json.loads(student.request('/Services/LiveRequest?id='+number)[1]);assert 'ContentBase64' not in json.dumps(live) and 'contentBase64' not in json.dumps(live)
    assert 'Correction' not in json.dumps(json.loads(Client().request('/Home/LiveQueue')[1]))
    print('PASS upload CSRF/ownership, file limits, forced private downloads, escaped correction feedback, version checks, and release gates')
    # Four near-2MB files exercise workspace backups above the old 10MB restore ceiling.
    number=create(student)
    large=b'%PDF-1.4\n'+b' '*(2*1024*1024-16400)+b'\n%%EOF\n'
    for version in range(4): upload(student,version,'large.pdf',large)
    assert len(submissions())==4
    upload(student,4,'quota.pdf',large);assert len(submissions())==4
    archive=backup(owner);assert len(json.dumps(archive).encode())>10*1024*1024
    restore(owner,archive)
    owner=login('ADMIN','ADMIN');student=login('2024-0002','student123');other=login('2024-0001','student123')
    saved=submissions()[-1];assert student.request('/Services/RequirementFile?submissionId='+saved['Id'])[1]==large
    other.request('/Services/RequirementFile?submissionId='+saved['Id'],expected=404)
    assert len(submissions())==4
    tampered=json.loads(json.dumps(archive));tampered['queue_data']['RequirementSubmissions'][0]['Sha256']='invalid'
    page=owner.request('/sims/modules/admin/restore.php')[1]
    result=owner.multipart('/sims/modules/admin/restore.php',{'csrf':csrf(page),'confirm_restore':'RESTORE'},'backup.json',json.dumps(tampered).encode(),field='backup')
    assert 'queue backup is invalid' in result[1].decode()
    assert student.request('/Services/RequirementFile?submissionId='+saved['Id'])[1]==large
    print('PASS bounded upload storage, >10MB complete backup round-trip, restored ownership, and tamper rejection without data replacement')
finally:
    owner=login('ADMIN','ADMIN');restore(owner,original)
    print('RESTORED original disposable workspace')

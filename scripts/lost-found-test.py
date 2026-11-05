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
    reporter=login('2024-0001','student123');claimant=login('2024-0002','student123');competitor=login('TCH001','teacher123');bystander=login('PARENT001','parent123')
    base='/sims/modules/shared/lost_found.php';secret='QA PRIVATE ITEM MARK 6382';proof='QA PRIVATE PROOF: inside label and contents';title='QA generic school bag';today=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).date().isoformat()
    def report(client,kind='Found',name=title):
        return page_post(client,base,{'action':'report','kind':kind,'category':'Bag','title':name,'location':'Main building','item_date':today,'private_details':secret,'submission_key':uuid.uuid4().hex})
    def query(sql,params=()):
        with database(owner) as db:return [dict(r) for r in db.execute(sql,params)]
    def item(id):return query('SELECT * FROM lost_found_items WHERE id=?',(id,))[0]
    def claim(id):return query('SELECT * FROM lost_found_claims WHERE id=?',(id,))[0]
    def mutate(client,path,table,id,**values):return page_post(client,path,{'revision':(item(id) if table=='item' else claim(id))['revision'],**values})
    created=report(reporter);id=int(urllib.parse.parse_qs(urllib.parse.urlparse(created[3]).query)['id'][0]);path=base+'?id='+str(id)
    claimant.request(path,expected=404);assert title.encode() not in claimant.request(base)[1]
    blocked=mutate(reporter,path,'item',id,action='publish',title=title,location='Main building',safe_publication='1',confirm_received='1');assert item(id)['listing_state']=='Pending'
    missing=mutate(owner,path,'item',id,action='publish',title=title,location='Main building',safe_publication='1');assert item(id)['listing_state']=='Pending'
    mutate(owner,path,'item',id,action='publish',title=title,location='Main building',safe_publication='1',confirm_received='1')
    assert secret.encode() not in claimant.request(path)[1] and secret.encode() not in claimant.request(base)[1]
    assert title.encode() in claimant.request(base+'?q=school&kind=Found&category=Bag&after='+today+'&before='+today)[1]
    claimant.form(path,{'action':'claim','proof':proof},419,False)
    filed=page_post(claimant,path,{'action':'claim','proof':proof});cid=int(urllib.parse.parse_qs(urllib.parse.urlparse(filed[3]).query)['claim'][0]);cp=base+'?claim='+str(cid)
    again=page_post(claimant,path,{'action':'claim','proof':'A repeated submission must not replace the prior proof.'});assert int(urllib.parse.parse_qs(urllib.parse.urlparse(again[3]).query)['claim'][0])==cid and claim(cid)['proof']==proof
    bystander.request(cp,expected=404);reporter.request(cp,expected=404);assert proof.encode() not in reporter.request(path)[1]
    mutate(claimant,cp,'claim',cid,action='review',state='Verified',note='Unauthorized review');assert claim(cid)['state']=='Pending'
    mutate(owner,cp,'claim',cid,action='review',state='Released',note='Cannot skip verification',confirm_handover='1');assert claim(cid)['state']=='Pending'
    mutate(owner,cp,'claim',cid,action='review',state='Needs information',note='Describe a unique inside marking.');assert claim(cid)['state']=='Needs information'
    mutate(claimant,cp,'claim',cid,action='resubmit',proof=proof+' corrected detail');assert claim(cid)['state']=='Pending' and proof.encode() in claimant.request(cp)[1]
    filed2=page_post(competitor,path,{'action':'claim','proof':'QA competitor ownership evidence for office inspection.'});cid2=int(urllib.parse.parse_qs(urllib.parse.urlparse(filed2[3]).query)['claim'][0]);cp2=base+'?claim='+str(cid2)
    # Competing verifications are serialized; at most one claimant can be held for release.
    owner2=login('ADMIN','ADMIN')
    def verify(pair):
        client,cid=pair;return mutate(client,base+'?claim='+str(cid),'claim',cid,action='review',state='Verified',note='Evidence inspected by school desk.')
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(verify,[(owner,cid),(owner2,cid2)]))
    winners=query('SELECT * FROM lost_found_claims WHERE item_id=? AND state="Verified"',(id,));assert len(winners)==1;winner=winners[0];wp=base+'?claim='+str(winner['id'])
    stale=page_post(owner,wp,{'action':'review','revision':0,'state':'Released','note':'Stale handover','confirm_handover':'1'});assert b'This claim changed' in stale[1]
    mutate(owner,path,'item',id,action='archive',note='Cannot archive a held verified item.');assert item(id)['listing_state']=='Published'
    mutate(owner,wp,'claim',winner['id'],action='review',state='Released',note='Recipient identity verified in person.');assert claim(winner['id'])['state']=='Verified'
    mutate(owner,wp,'claim',winner['id'],action='review',state='Released',note='Recipient identity verified and item handed over.',confirm_handover='1')
    assert item(id)['resolution']=='Returned' and claim(winner['id'])['state']=='Released'
    assert len(query('SELECT * FROM lost_found_claims WHERE item_id=? AND state="Released"',(id,)))==1
    assert len(query('SELECT * FROM lost_found_claims WHERE item_id=? AND state="Rejected"',(id,)))==1
    assert ('href="?id='+str(id)+'"').encode() not in bystander.request(base+'?q='+urllib.parse.quote(title))[1]
    lost=report(reporter,'Lost','QA missing umbrella');lid=int(urllib.parse.parse_qs(urllib.parse.urlparse(lost[3]).query)['id'][0]);lp=base+'?id='+str(lid)
    mutate(reporter,lp,'item',lid,action='reunited',note='I recovered the item from a classroom.');assert item(lid)['resolution']=='Returned'
    archive=backup(owner);restore(owner,archive);owner=login('ADMIN','ADMIN');claimant=login('2024-0002','student123');assert claim(winner['id'])['state']=='Released' and item(id)['resolution']=='Returned'
    print('PASS moderation/desk receipt, hidden identifiers, filters, CSRF, private proofs, duplicate claims, staff review, correction, concurrent verification, stale/early release rejection, handover, competing-claim closure, recovery, and backup/restore')
finally:
    restore(owner,original)
    print('RESTORED original disposable lost-and-found workspace')

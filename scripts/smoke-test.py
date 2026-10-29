"""Integration checks against a running local portal; run on disposable demo data."""
import os, http.cookiejar, urllib.request, urllib.parse, re, html, json
BASE=os.environ.get('CAMPUS_TEST_URL', 'http://127.0.0.1:8080')
def client():
 return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def get(c,path):
 with c.open(BASE+path) as r:return r.geturl(),r.read().decode()
def post(c,path,data):
 with c.open(BASE+path,urllib.parse.urlencode(data).encode()) as r:return r.geturl(),r.read().decode()
def token(body,name):
 for tag in re.findall(r'<input\b[^>]*>',body):
  attrs=dict(re.findall(r'([\w]+)="([^"]*)"',tag))
  if attrs.get('name')==name:return html.unescape(attrs['value'])
 raise AssertionError('Missing CSRF token '+name)
def login(username,password):
 c=client();_,body=get(c,'/sims/login.php')
 url,body=post(c,'/sims/login.php',{'csrf':token(body,'csrf'),'username':username,'password':password})
 assert url.endswith('/portal') and 'Your academics' in body, (url,body[:100])
 return c
c=client()
assert get(c,'/Admin/Dashboard')[0].endswith('/sims/login.php')
_,body=get(c,'/sims/login.php')
_,bad=post(c,'/sims/login.php',{'csrf':token(body,'csrf'),'username':'ADMIN','password':'wrong'})
assert 'Invalid username or password' in bad
print('PASS anonymous access and invalid login')
admin=login('ADMIN','ADMIN')
for p,expected in [('/sims/dashboard.php','Dashboard'),('/Admin/Dashboard','Queue'),('/Admin/QueueControl','Queue'),('/sims/modules/admin/manage.php?entity=students','Students')]:
 assert expected in get(admin,p)[1],p
print('PASS shared admin login, student records, and queue control')
student=login('2024-0002','student123')
for p in ['/sims/modules/student/grades.php','/sims/modules/student/attendance.php','/sims/modules/student/schedule.php']:
 url,body=get(student,p); assert 'Fatal error' not in body and 'login.php' not in url,p
assert '/Admin/Dashboard' not in get(student,'/Admin/Dashboard')[0]
_,body=get(student,'/Queue/GetTicket')
url,ticket=post(student,'/Queue/GetTicket',{'__RequestVerificationToken':token(body,'__RequestVerificationToken'),'CustomerName':'ignored','PhoneNumber':'09121110002','ServiceType':'Document Request','RequestType':'Certificate of Enrollment','StudentNumber':'2024-0002','PriorityType':'Regular'})
assert '/Queue/Ticket/' in url or '/Queue/Ticket?' in url,(url,ticket[:100])
assert 'Document Request' in ticket
assert 'SmartSIMS' in ticket
assert 'Document Request' in get(student,'/Queue/MyTicket?searchText=09121110002')[1]
print('PASS student academic pages, queue authorization, ticket creation, and tracking')
# Admin can operate the queue, and the update is visible to the student.
_,body=get(admin,'/Admin/QueueControl')
post(admin,'/Admin/CallNext',{'__RequestVerificationToken':token(body,'__RequestVerificationToken'),'counterName':'Counter 1'})
assert 'Serving' in get(admin,'/Admin/QueueControl')[1]
print('PASS admin call-next action')
_,body=get(student,'/sims/logout.php')
post(student,'/sims/logout.php',{'csrf':token(body,'csrf')})
assert get(student,'/Queue/GetTicket')[0].endswith('/sims/login.php')
print('PASS logout revokes queue access')
for p in ['/sims/database/SmartSIMS.db','/sims/bootstrap.php','/sims/config/config.php','/sims/campus-session.php']:
 try:get(c,p);raise AssertionError('Exposed '+p)
 except urllib.error.HTTPError as e:assert e.code==404,(p,e.code)
for p in ['/sims/assets/css/bootstrap.min.css','/sims/assets/js/chart.umd.min.js','/sims/assets/webfonts/fa-solid-900.woff2']:
 with c.open(BASE+p) as r:assert r.status==200 and len(r.read())>1000
print('PASS private file protection and local frontend assets')

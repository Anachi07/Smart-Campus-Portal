"""Live projections and PWA contract checks; uses disposable demo data."""
import runpy, json, urllib.request, urllib.error
from pathlib import Path
suite = runpy.run_path(str(Path(__file__).with_name('smoke-test.py')))
base, login, get = suite['BASE'], suite['login'], suite['get']
student = login('2024-0002', 'student123')
admin = login('ADMIN', 'ADMIN')
def data(client, path):
    with client.open(base + path) as response:
        assert 'no-store' in response.headers.get('Cache-Control', '')
        return json.load(response)
def denied(client, path, expected):
    try:
        client.open(base + path)
        raise AssertionError('Unexpected access: ' + path)
    except urllib.error.HTTPError as error:
        assert error.code == expected, (path, error.code)
public = data(suite['client'](), '/Home/LiveQueue')
allowed = {'ticketNumber','office','counterName','predictedCounterName','serviceType','requestType'}
for ticket in public['serving'] + public['waiting']:
    assert set(ticket) == allowed
print('PASS public queue projection excludes personal data')
rows = data(student, '/Queue/LiveTickets')
assert rows and all('customerName' not in t and 'phoneNumber' not in t for t in rows)
ticket = rows[0]['ticketNumber']
progress = data(student, '/Services/LiveRequest?id=' + ticket)
assert {'status','documentStatus','prepared','verified','total','history','submissionVersion','canClaim'} == set(progress)
assert data(admin, '/Services/LiveRequest?id=' + ticket)['documentStatus'] == progress['documentStatus']
denied(suite['client'](), '/Queue/LiveTickets', 401)
denied(suite['client'](), '/Services/LiveRequest?id=' + ticket, 401)
denied(student, '/Services/LiveRequest?id=not-a-real-ticket', 404)
admin_tickets = data(admin, '/Queue/LiveTickets')
student_ids = {t['ticketNumber'] for t in rows}
foreign = next((t['ticketNumber'] for t in admin_tickets if t['ticketNumber'] not in student_ids), None)
if foreign:
    denied(student, '/Services/LiveRequest?id=' + foreign, 404)
    assert data(student, '/Queue/LiveTickets?searchText=' + foreign) == []
else:
    raise AssertionError('Ownership check needs a foreign fixture ticket')
print('PASS authenticated live updates and cross-account isolation')
for path, expected in [('/Home/Display','data-live-display'),('/Queue/MyTicket','data-live-tickets'),('/Services/Request?id='+ticket,'data-live-request'),('/Services/Requests','request-search')]:
    assert expected in get(student,path)[1]
with urllib.request.urlopen(base+'/manifest.webmanifest') as response:
    assert 'application/manifest+json' in response.headers['Content-Type']
    manifest=json.load(response)
    assert manifest['scope']=='/' and manifest['display']=='standalone'
with urllib.request.urlopen(base+'/service-worker.js') as response:
    assert response.headers['Service-Worker-Allowed']=='/'
    assert 'no-cache' in response.headers['Cache-Control']
assert 'manifest.webmanifest' in get(student,'/sims/dashboard.php')[1]
print('PASS live views, shared app manifest, and service worker scope')

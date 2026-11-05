"""Local backup-worker integrity, retention, permissions, and restore checks. Disposable data only."""
import os,sys,subprocess,json,uuid,pathlib,base64,sqlite3
if os.environ.get('CAMPUS_ALLOW_DESTRUCTIVE_TESTS')!='1':sys.exit('Use disposable data and CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1.')
repo=pathlib.Path(__file__).resolve().parent.parent
source=pathlib.Path(os.environ.get('CAMPUS_DATA_DIR',repo/'.runtime/campus-validation')).resolve()
folder=repo/'.runtime'/('backup-check-'+uuid.uuid4().hex);folder.mkdir(mode=0o700)
(folder/'keep-me.txt').write_text('Unrelated user file: do not prune.')
php=pathlib.Path('/workspace/tools/php');binary=php/'usr/bin/php8.4'
args=['-n','-d',f'extension={php}/usr/lib/php/20240924/pdo.so','-d',f'extension={php}/usr/lib/php/20240924/pdo_sqlite.so','-d',f'extension={php}/usr/lib/php/20240924/sqlite3.so'] if binary.exists() else []
env={**os.environ,'CAMPUS_SIMS_DB':str(source/'SmartSIMS.db'),'CAMPUS_QUEUE_DATA_DIR':str(source/'queue'),'CAMPUS_BACKUP_DIR':str(folder),'CAMPUS_BACKUP_KEEP':'2','CAMPUS_BACKUP_PHP':str(binary) if binary.exists() else 'php','CAMPUS_BACKUP_PHP_ARGS':json.dumps(args)}
if binary.exists():env['LD_LIBRARY_PATH']=str(php/'usr/lib/x86_64-linux-gnu')
for i in range(3):subprocess.run(['node','scripts/backup-worker.mjs','--once'],cwd=repo,env=env,check=True,stdout=subprocess.DEVNULL)
files=list(folder.glob('campus-auto-*.json'));assert len(files)==2 and (folder/'keep-me.txt').exists()
for path in files:
 assert path.stat().st_mode&0o777==0o600
 archive=json.loads(path.read_text());assert archive['format']=='smart-campus-backup-v1' and 'IssuedDocuments' in archive['queue_data']
 db=sqlite3.connect(':memory:');db.deserialize(base64.b64decode(archive['academic_database']));assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok';assert db.execute('SELECT COUNT(*) FROM sqlite_master WHERE name="campus_requests"').fetchone()[0]==1
status=json.loads((folder/'status.json').read_text());assert status['retained']==2 and status['bytes']>0 and status['last_success']
# Use the existing test transport without running its feature suite.
namespace={};transport=(repo/'scripts/campus-test.py').read_text().split("def database(owner):")[0];exec(compile(transport,'test-transport','exec'),namespace)
owner=namespace['login']('ADMIN','ADMIN');original=namespace['backup'](owner)
try:namespace['restore'](owner,json.loads(files[0].read_text()));owner=namespace['login']('ADMIN','ADMIN');assert namespace['backup'](owner)['queue_data']['Tickets']==archive['queue_data']['Tickets']
finally:namespace['restore'](owner,original)
print('PASS worker execution, full backup integrity, private permissions, retention, unrelated-file preservation, and automated-snapshot restore')

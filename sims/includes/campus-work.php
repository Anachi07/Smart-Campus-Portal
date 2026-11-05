<?php
// School workflows use the existing private SQLite database and paired backups.
function init_campus_work(PDO $pdo): void {
    foreach([
        'CREATE TABLE IF NOT EXISTS campus_offices(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT UNIQUE NOT NULL,reviewer_id INTEGER REFERENCES users(id),active INTEGER NOT NULL DEFAULT 1)',
        'CREATE TABLE IF NOT EXISTS campus_form_types(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT UNIQUE NOT NULL,route TEXT NOT NULL,active INTEGER NOT NULL DEFAULT 1)',
        'CREATE TABLE IF NOT EXISTS campus_requests(id INTEGER PRIMARY KEY AUTOINCREMENT,reference TEXT UNIQUE NOT NULL,kind TEXT NOT NULL,owner_id INTEGER NOT NULL REFERENCES users(id),title TEXT NOT NULL,details TEXT NOT NULL,cycle TEXT NOT NULL DEFAULT "",status TEXT NOT NULL DEFAULT "In review",revision INTEGER NOT NULL DEFAULT 0,request_key TEXT UNIQUE NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
        'CREATE UNIQUE INDEX IF NOT EXISTS clearance_cycle ON campus_requests(owner_id,cycle) WHERE kind="clearance" AND status<>"Cancelled"',
        'CREATE TABLE IF NOT EXISTS campus_steps(id INTEGER PRIMARY KEY AUTOINCREMENT,request_id INTEGER NOT NULL REFERENCES campus_requests(id),office_id INTEGER NOT NULL REFERENCES campus_offices(id),office_name TEXT NOT NULL,position INTEGER NOT NULL,status TEXT NOT NULL DEFAULT "Pending",note TEXT NOT NULL DEFAULT "",reviewed_by INTEGER REFERENCES users(id),reviewed_at TEXT,UNIQUE(request_id,position))',
        'CREATE TABLE IF NOT EXISTS campus_work_events(id INTEGER PRIMARY KEY AUTOINCREMENT,request_id INTEGER NOT NULL REFERENCES campus_requests(id),actor_id INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,internal INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
        'CREATE TABLE IF NOT EXISTS attendance_windows(id INTEGER PRIMARY KEY AUTOINCREMENT,schedule_id INTEGER NOT NULL REFERENCES schedules(id),opened_by INTEGER NOT NULL REFERENCES users(id),token TEXT NOT NULL,token_expires INTEGER NOT NULL,expires_at INTEGER NOT NULL,closed INTEGER NOT NULL DEFAULT 0)',
        'CREATE TABLE IF NOT EXISTS attendance_window_entries(window_id INTEGER NOT NULL REFERENCES attendance_windows(id),student_id INTEGER NOT NULL REFERENCES students(id),created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,PRIMARY KEY(window_id,student_id))',
        'CREATE TABLE IF NOT EXISTS account_mfa(user_id INTEGER PRIMARY KEY REFERENCES users(id),secret TEXT NOT NULL,recovery_hashes TEXT NOT NULL,last_step INTEGER NOT NULL DEFAULT -1,enabled_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
        'CREATE INDEX IF NOT EXISTS campus_steps_office ON campus_steps(office_id,status)',
        'CREATE INDEX IF NOT EXISTS campus_requests_owner ON campus_requests(owner_id,kind,status)'
    ] as $sql)$pdo->exec($sql);
    require_once __DIR__.'/enrollment.php';
    init_enrollment($pdo);
    require_once __DIR__.'/school-courses.php';
    init_school_courses($pdo);
    require_once __DIR__.'/facilities.php';
    init_facilities($pdo);
    require_once __DIR__.'/lost-found.php';
    init_lost_found($pdo);
    $columns=array_column($pdo->query('PRAGMA table_info(announcements)')->fetchAll(),'name');
    foreach(['target_course_id','target_section_id'] as $column)if(!in_array($column,$columns,true))$pdo->exec("ALTER TABLE announcements ADD COLUMN $column INTEGER");
    if(getenv('CAMPUS_MODE')!=='production' && !$pdo->query('SELECT COUNT(*) FROM campus_offices')->fetchColumn() && !$pdo->query("SELECT value FROM settings WHERE key='campus_work_initialized'")->fetchColumn()) {
        foreach(['Registrar','Cashier','Library'] as $office)$pdo->prepare('INSERT INTO campus_offices(name) VALUES(?)')->execute([$office]);
        $registrar=(int)$pdo->query('SELECT id FROM campus_offices WHERE name="Registrar"')->fetchColumn();
        foreach(['Leave request','Schedule change','School permit'] as $name)$pdo->prepare('INSERT INTO campus_form_types(name,route) VALUES(?,?)')->execute([$name,json_encode([$registrar])]);
        $pdo->exec("INSERT OR IGNORE INTO settings(key,value) VALUES('campus_work_initialized','1')");
    }
}
function campus_time(string $utc): string {return (new DateTimeImmutable($utc,new DateTimeZone('UTC')))->setTimezone(new DateTimeZone('Asia/Manila'))->format('M j, Y g:i a').' · Manila';}
function campus_staff(): bool {return in_array(current_user()['role']??'',['Admin','Super Admin'],true);}
function work_scope(string $alias='r'): string {
    $u=current_user();$id=(int)$u['id'];if($u['role']==='Super Admin')return '1=1';if(!in_array($u['role'],['Admin','Teacher'],true))return "$alias.owner_id=$id";
    return "($alias.owner_id=$id OR EXISTS(SELECT 1 FROM campus_steps ws JOIN campus_offices wo ON wo.id=ws.office_id WHERE ws.request_id=$alias.id AND wo.reviewer_id=$id))";
}
function work_request(int $id): ?array {return row('SELECT r.* FROM campus_requests r WHERE r.id=? AND '.work_scope(),[$id]);}
function work_steps(int $id): array {return rows('SELECT s.*,o.reviewer_id FROM campus_steps s JOIN campus_offices o ON o.id=s.office_id WHERE s.request_id=? ORDER BY position',[$id]);}
function can_review_step(array $step): bool {return current_user()['role']==='Super Admin'||in_array(current_user()['role'],['Admin','Teacher'],true)&&(int)$step['reviewer_id']===(int)current_user()['id'];}
function work_event(int $id,string $body,bool $internal=false): void {db()->prepare('INSERT INTO campus_work_events(request_id,actor_id,body,internal) VALUES(?,?,?,?)')->execute([$id,current_user()['id'],$body,$internal?1:0]);}
function campus_notify(int $id,string $title,string $body): void {db()->prepare('INSERT INTO notifications(user_id,title,body) VALUES(?,?,?)')->execute([$id,$title,$body]);}
function notify_work_reviewers(int $id,string $title): void {
    $request=row('SELECT * FROM campus_requests WHERE id=?',[$id]);
    foreach(rows('SELECT DISTINCT u.id FROM users u JOIN roles ro ON ro.id=u.role_id WHERE u.status="Active" AND u.id<>? AND (ro.name="Super Admin" OR (ro.name IN("Admin","Teacher") AND u.id IN(SELECT o.reviewer_id FROM campus_steps s JOIN campus_offices o ON o.id=s.office_id WHERE s.request_id=?)))',[$request['owner_id'],$id]) as $reviewer)campus_notify((int)$reviewer['id'],$title,'Request '.$request['reference'].' has a new update in your office workspace.');
}
function work_create(string $kind,array $input): int {
    $user=current_user();$uid=(int)$user['id'];
    if(!in_array($kind,['clearance','helpdesk','form'],true)||($kind!=='helpdesk'&&$user['role']!=='Student'))throw new InvalidArgumentException('Student access is required for clearance and school forms.');
    $key=(string)($input['request_key']??'');if(!preg_match('/^[a-f0-9]{32}$/',$key))throw new InvalidArgumentException('Reload this form and try again.');
    $prior=row('SELECT id,owner_id FROM campus_requests WHERE request_key=?',[$key]);if($prior){if((int)$prior['owner_id']!==$uid)throw new InvalidArgumentException('Reload this form.');return(int)$prior['id'];}
    $details=trim((string)($input['details']??''));$title=trim((string)($input['title']??''));$cycle='';
    if(strlen($details)<5||strlen($details)>3000)throw new InvalidArgumentException('Provide details of 5–3000 characters. Do not include passwords or payment credentials.');
    if((int)scalar('SELECT COUNT(*) FROM campus_requests WHERE owner_id=? AND created_at>=datetime("now","-1 day")',[$uid])>=50)throw new InvalidArgumentException('Daily request limit reached. Contact the school for assistance.');
    if($kind==='clearance') {
        $cycle=(string)(scalar('SELECT name FROM school_years WHERE is_active=1 ORDER BY id DESC LIMIT 1')?:'');
        if(!$cycle)throw new InvalidArgumentException('The school must configure an active school year first.');
        $title='School clearance · '.$cycle;$offices=rows('SELECT * FROM campus_offices WHERE active=1 ORDER BY id');
    } elseif($kind==='form') {
        $type=row('SELECT * FROM campus_form_types WHERE id=? AND active=1',[(int)($input['form_type']??0)]);
        if(!$type)throw new InvalidArgumentException('Choose a published school form.');$title=$type['name'];$offices=[];
        foreach(json_decode($type['route'],true,512,JSON_THROW_ON_ERROR) as $oid){$office=row('SELECT * FROM campus_offices WHERE id=? AND active=1',[$oid]);if(!$office)throw new InvalidArgumentException('This approval route is unavailable. Contact the school.');$offices[]=$office;}
    } else {
        if(strlen($title)<3||strlen($title)>120)throw new InvalidArgumentException('Use a concern title of 3–120 characters.');
        $office=row('SELECT * FROM campus_offices WHERE id=? AND active=1',[(int)($input['office_id']??0)]);$offices=$office?[$office]:[];
    }
    if(!$offices)throw new InvalidArgumentException('The Super Admin must configure the required offices first.');
    db()->beginTransaction();
    try {
        // All clearance offices review independently; forms follow their configured sequence.
        $reference=['clearance'=>'CLR','helpdesk'=>'SUP','form'=>'FRM'][$kind].'-'.strtoupper(bin2hex(random_bytes(6)));
        db()->prepare('INSERT INTO campus_requests(reference,kind,owner_id,title,details,cycle,request_key) VALUES(?,?,?,?,?,?,?)')->execute([$reference,$kind,$uid,$title,$details,$cycle,$key]);$id=(int)db()->lastInsertId();
        foreach($offices as $i=>$office)db()->prepare('INSERT INTO campus_steps(request_id,office_id,office_name,position) VALUES(?,?,?,?)')->execute([$id,$office['id'],$office['name'],$i]);
        work_event($id,'Request submitted.');notify_work_reviewers($id,'New campus request');log_activity($uid,'Campus request submitted',$reference);db()->commit();return$id;
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();if($e instanceof PDOException)throw new InvalidArgumentException('An active clearance for this school year may already exist. Check your requests.');throw$e;}
}
function work_update(int $id,array $input): void {
    $request=work_request($id);if(!$request){http_response_code(404);exit('Request not found.');}
    $action=(string)($input['action']??'');$note=trim((string)($input['note']??''));$uid=(int)current_user()['id'];$owner=(int)$request['owner_id']===$uid;
    if(strlen($note)>2000)throw new InvalidArgumentException('Keep messages within 2000 characters.');
    db()->beginTransaction();
    try {
        $lock=db()->prepare('UPDATE campus_requests SET revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=? AND revision=?');$lock->execute([$id,(int)($input['revision']??-1)]);
        if(!$lock->rowCount())throw new InvalidArgumentException('This request changed. Reload before submitting another action.');
        if($action==='message') {
            if(strlen($note)<2)throw new InvalidArgumentException('Write a message of at least 2 characters.');
            $internal=($input['internal']??'')==='1';if($internal&&$owner)throw new InvalidArgumentException('Only assigned office staff can write internal notes.');work_event($id,$note,$internal);if(!$internal){if($owner)notify_work_reviewers($id,'Requester replied');else campus_notify((int)$request['owner_id'],'School office replied','Open request '.$request['reference'].' to read the office reply.');}
        } elseif($action==='cancel') {
            if(!$owner||in_array($request['status'],['Approved','Resolved','Rejected','Cancelled'],true))throw new InvalidArgumentException('This request cannot be cancelled.');
            db()->prepare('UPDATE campus_requests SET status="Cancelled" WHERE id=?')->execute([$id]);work_event($id,'Requester cancelled this request.');
        } elseif($action==='resubmit') {
            if(!$owner||$request['status']!=='Returned'||strlen($note)<5)throw new InvalidArgumentException('Only a returned request can be resubmitted, with an explanation of the correction.');
            if(isset($input['details'])){$details=trim((string)$input['details']);if(strlen($details)<5||strlen($details)>3000)throw new InvalidArgumentException('Corrected details must have 5–3000 characters.');if($details!==$request['details']){work_event($id,'Previous submission details: '.$request['details']);db()->prepare('UPDATE campus_requests SET details=? WHERE id=?')->execute([$details,$id]);}}
            db()->prepare('UPDATE campus_steps SET status="Pending",note="",reviewed_at=NULL,reviewed_by=NULL WHERE request_id=? AND status="Returned"')->execute([$id]);
            db()->prepare('UPDATE campus_requests SET status="In review" WHERE id=?')->execute([$id]);work_event($id,'Resubmitted: '.$note);notify_work_reviewers($id,'Campus request resubmitted');
        } elseif($action==='review') {
            $steps=work_steps($id);$step=null;foreach($steps as $s)if((int)$s['id']===(int)($input['step_id']??0))$step=$s;
            if(!$step||!can_review_step($step)||$owner||$step['status']!=='Pending'||in_array($request['status'],['Approved','Resolved','Rejected','Cancelled'],true))throw new InvalidArgumentException('Only the assigned office can review a pending step.');
            if($request['kind']==='form')foreach($steps as $earlier)if((int)$earlier['position']<(int)$step['position']&&$earlier['status']!=='Approved')throw new InvalidArgumentException('Earlier offices must approve this form first.');
            $decision=(string)($input['decision']??'');if(!in_array($decision,['Approved','Returned','Rejected'],true)||($decision!=='Approved'&&strlen($note)<5))throw new InvalidArgumentException('Choose a decision. Returns and rejections need a reason of at least 5 characters.');
            db()->prepare('UPDATE campus_steps SET status=?,note=?,reviewed_by=?,reviewed_at=CURRENT_TIMESTAMP WHERE id=?')->execute([$decision,$note,$uid,$step['id']]);
            $states=array_column(work_steps($id),'status');$status=in_array('Rejected',$states,true)?'Rejected':(in_array('Returned',$states,true)?'Returned':(count(array_filter($states,fn($s)=>$s==='Approved'))===count($states)?($request['kind']==='helpdesk'?'Resolved':'Approved'):'In review'));
            db()->prepare('UPDATE campus_requests SET status=? WHERE id=?')->execute([$status,$id]);work_event($id,$step['office_name'].': '.$decision.($note!==''?' · '.$note:''));
            campus_notify((int)$request['owner_id'],'Campus request updated','Your request '.$request['reference'].' has an office update. Open the request to review it.');
        } else throw new InvalidArgumentException('Choose a valid action.');
        log_activity($uid,'Campus workflow update',$request['reference'].' · '.$action);db()->commit();
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function announcement_scope(string $alias='a'): array {
    $u=current_user();if(campus_staff())return['1=1',[]];
    $student=row('SELECT s.* FROM students s WHERE '.student_scope_sql().' ORDER BY s.id LIMIT 1');
    $course=(int)($student['course_id']??0);$section=(int)($student['section_id']??0);
    if($u['role']==='Teacher')return["($alias.target_role IN ('All',?) AND ($alias.target_course_id IS NULL OR EXISTS(SELECT 1 FROM subjects su WHERE su.teacher_id=".teacher_id()." AND su.course_id=$alias.target_course_id) OR EXISTS(SELECT 1 FROM schedules sc JOIN sections se ON se.id=sc.section_id WHERE sc.teacher_id=".teacher_id()." AND se.course_id=$alias.target_course_id)) AND ($alias.target_section_id IS NULL OR $alias.target_section_id IN(SELECT section_id FROM schedules WHERE teacher_id=".teacher_id().")))",[$u['role']]];
    return["($alias.target_role IN ('All',?) AND ($alias.target_course_id IS NULL OR $alias.target_course_id=?) AND ($alias.target_section_id IS NULL OR $alias.target_section_id=?))",[$u['role'],$course,$section]];
}
function publish_announcement(array $input): void {
    $u=current_user();if(!campus_staff()&&$u['role']!=='Teacher'){http_response_code(403);exit('Forbidden');}
    $title=trim((string)($input['title']??''));$body=trim((string)($input['body']??''));$role=(string)($input['target_role']??'All');$course=(int)($input['target_course_id']??0)?:null;$section=(int)($input['target_section_id']??0)?:null;
    if(strlen($title)<3||strlen($title)>160||strlen($body)<3||strlen($body)>5000||!in_array($role,['All','Student','Teacher','Parent','Admin'],true))throw new InvalidArgumentException('Provide a title, message, and valid audience.');
    if($course&&!scalar('SELECT COUNT(*) FROM courses WHERE id=?',[$course]))throw new InvalidArgumentException('Choose a valid course.');
    if($section){$s=row('SELECT * FROM sections WHERE id=?',[$section]);if(!$s||($course&&(int)$s['course_id']!==$course))throw new InvalidArgumentException('Choose a section from the selected course.');$course=(int)$s['course_id'];}
    if($u['role']==='Teacher'&&(!$section||!scalar('SELECT COUNT(*) FROM schedules WHERE teacher_id=? AND section_id=?',[teacher_id(),$section])||!in_array($role,['Student','Parent'],true)))throw new InvalidArgumentException('Teachers may post to students or parents in their assigned sections only.');
    db()->prepare('INSERT INTO announcements(title,body,target_role,target_course_id,target_section_id,created_by) VALUES(?,?,?,?,?,?)')->execute([$title,$body,$role,$course,$section,$u['id']]);log_activity($u['id'],'Targeted announcement published','Audience: '.$role.'; course '.($course??'all').'; section '.($section??'all'));
}

<?php
function init_enrollment(PDO $pdo): void {
    foreach([
        'CREATE TABLE IF NOT EXISTS enrollment_requirements(id INTEGER PRIMARY KEY AUTOINCREMENT,year_id INTEGER NOT NULL REFERENCES school_years(id),course_id INTEGER REFERENCES courses(id),office_id INTEGER NOT NULL REFERENCES campus_offices(id),title TEXT NOT NULL,instructions TEXT NOT NULL DEFAULT "",required INTEGER NOT NULL DEFAULT 1,active INTEGER NOT NULL DEFAULT 1)',
        'CREATE TABLE IF NOT EXISTS enrollment_checklists(id INTEGER PRIMARY KEY AUTOINCREMENT,owner_id INTEGER NOT NULL REFERENCES users(id),year_id INTEGER NOT NULL REFERENCES school_years(id),year_name TEXT NOT NULL,course_name TEXT NOT NULL,revision INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,UNIQUE(owner_id,year_id))',
        'CREATE TABLE IF NOT EXISTS enrollment_items(id INTEGER PRIMARY KEY AUTOINCREMENT,checklist_id INTEGER NOT NULL REFERENCES enrollment_checklists(id),requirement_id INTEGER NOT NULL REFERENCES enrollment_requirements(id),office_id INTEGER NOT NULL REFERENCES campus_offices(id),office_name TEXT NOT NULL,title TEXT NOT NULL,instructions TEXT NOT NULL,required INTEGER NOT NULL,prepared INTEGER NOT NULL DEFAULT 0,status TEXT NOT NULL DEFAULT "Pending",note TEXT NOT NULL DEFAULT "",reviewed_by INTEGER REFERENCES users(id),reviewed_at TEXT,UNIQUE(checklist_id,requirement_id))',
        'CREATE TABLE IF NOT EXISTS enrollment_history(id INTEGER PRIMARY KEY AUTOINCREMENT,checklist_id INTEGER NOT NULL REFERENCES enrollment_checklists(id),actor_id INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
        'CREATE INDEX IF NOT EXISTS enrollment_items_office ON enrollment_items(office_id,status)'
    ] as $sql)$pdo->exec($sql);
}
function enrollment_scope(string $alias='c'): string {
    $u=current_user();$id=(int)$u['id'];if($u['role']==='Super Admin')return '1=1';
    $own="$alias.owner_id=$id";
    if($u['role']==='Parent')return "($own OR $alias.owner_id IN(SELECT s.user_id FROM students s JOIN parents p ON p.student_id=s.id WHERE p.user_id=$id))";
    if(in_array($u['role'],['Admin','Teacher'],true))return "($own OR EXISTS(SELECT 1 FROM enrollment_items i JOIN campus_offices o ON o.id=i.office_id WHERE i.checklist_id=$alias.id AND o.reviewer_id=$id))";
    return$own;
}
function enrollment_get(int $id): ?array {return row('SELECT c.*,u.full_name,s.student_no FROM enrollment_checklists c JOIN users u ON u.id=c.owner_id LEFT JOIN students s ON s.user_id=c.owner_id WHERE c.id=? AND '.enrollment_scope(),[$id]);}
function enrollment_items(int $id): array {return rows('SELECT i.*,o.reviewer_id FROM enrollment_items i JOIN campus_offices o ON o.id=i.office_id WHERE i.checklist_id=? ORDER BY i.id',[$id]);}
function enrollment_summary(array $items): array {
    $required=array_filter($items,fn($i)=>(bool)$i['required']);$verified=count(array_filter($required,fn($i)=>$i['status']==='Verified'));$returned=count(array_filter($items,fn($i)=>$i['status']==='Returned'));
    return['required'=>count($required),'verified'=>$verified,'returned'=>$returned,'label'=>$returned?'Corrections needed':(count($required)>0&&$verified===count($required)?'Required checklist verified':'Requirements in progress')];
}
function enrollment_history(int $id,string $body): void {db()->prepare('INSERT INTO enrollment_history(checklist_id,actor_id,body) VALUES(?,?,?)')->execute([$id,current_user()['id'],$body]);}
function enrollment_start(): int {
    $u=current_user();if($u['role']!=='Student')throw new InvalidArgumentException('Only students can start their enrollment checklist.');
    $student=row('SELECT * FROM students WHERE user_id=?',[$u['id']]);$year=row('SELECT * FROM school_years WHERE is_active=1 ORDER BY id DESC LIMIT 1');
    if(!$student||!$student['course_id']||!scalar('SELECT COUNT(*) FROM courses WHERE id=? AND is_offered=1',[$student['course_id']])||!$year)throw new InvalidArgumentException('Ask the school to configure your student profile and active school year first.');
    $prior=scalar('SELECT id FROM enrollment_checklists WHERE owner_id=? AND year_id=?',[$u['id'],$year['id']]);if($prior)return(int)$prior;
    $requirements=rows('SELECT r.*,o.name AS office_name FROM enrollment_requirements r JOIN campus_offices o ON o.id=r.office_id WHERE r.year_id=? AND r.active=1 AND o.active=1 AND (r.course_id IS NULL OR r.course_id=?) ORDER BY r.id',[$year['id'],$student['course_id']]);
    if(!$requirements)throw new InvalidArgumentException('The school has not published enrollment requirements for your course and active school year yet.');
    db()->beginTransaction();try{
        $prior=scalar('SELECT id FROM enrollment_checklists WHERE owner_id=? AND year_id=?',[$u['id'],$year['id']]);if($prior){db()->commit();return(int)$prior;}
        $course=(string)(scalar('SELECT name FROM courses WHERE id=?',[$student['course_id']])?:'Unassigned course');
        db()->prepare('INSERT INTO enrollment_checklists(owner_id,year_id,year_name,course_name) VALUES(?,?,?,?)')->execute([$u['id'],$year['id'],$year['name'],$course]);$id=(int)db()->lastInsertId();
        foreach($requirements as $r)db()->prepare('INSERT INTO enrollment_items(checklist_id,requirement_id,office_id,office_name,title,instructions,required) VALUES(?,?,?,?,?,?,?)')->execute([$id,$r['id'],$r['office_id'],$r['office_name'],$r['title'],$r['instructions'],$r['required']]);
        enrollment_history($id,'Checklist started from the school-published requirements.');log_activity($u['id'],'Enrollment checklist started','Checklist #'.$id);db()->commit();return$id;
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function enrollment_update(int $id,array $input): void {
    $checklist=enrollment_get($id);if(!$checklist){http_response_code(404);exit('Checklist not found.');}
    $u=current_user();$owner=$u['role']==='Student'&&(int)$checklist['owner_id']===(int)$u['id'];$action=(string)($input['action']??'');
    db()->beginTransaction();try{
        $change=db()->prepare('UPDATE enrollment_checklists SET revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=? AND revision=?');$change->execute([$id,(int)($input['revision']??-1)]);if(!$change->rowCount())throw new InvalidArgumentException('This checklist changed. Reload before saving another update.');
        if($action==='prepare') {
            if(!$owner)throw new InvalidArgumentException('Only the student can update preparation flags.');
            $prepared=array_map('intval',is_array($input['prepared']??null)?$input['prepared']:[]);$items=enrollment_items($id);
            foreach($prepared as $iid)if(!in_array($iid,array_column($items,'id')))throw new InvalidArgumentException('Select items from this checklist only.');
            foreach($items as $item){if($item['status']==='Verified')continue;$value=in_array((int)$item['id'],$prepared,true)?1:0;
                if($value!==(int)$item['prepared'])db()->prepare('UPDATE enrollment_items SET prepared=?,status=? WHERE id=?')->execute([$value,$item['status']==='Returned'&&$value===1?'Pending':$item['status'],$item['id']]);
            }
            enrollment_history($id,'Student updated preparation. Office verification is still required.');
        }elseif($action==='review') {
            $item=row('SELECT i.*,o.reviewer_id FROM enrollment_items i JOIN campus_offices o ON o.id=i.office_id WHERE i.id=? AND i.checklist_id=?',[(int)($input['item_id']??0),$id]);
            if(!$item||!can_review_step($item)||(int)$checklist['owner_id']===(int)$u['id'])throw new InvalidArgumentException('Only the assigned office can review this requirement.');
            $status=(string)($input['status']??'');$note=trim((string)($input['note']??''));
            if(!in_array($status,['Verified','Returned'],true)||strlen($note)>600||($status==='Returned'&&strlen($note)<5))throw new InvalidArgumentException('Choose Verified or Return for correction. Returns need a reason of 5–600 characters.');
            db()->prepare('UPDATE enrollment_items SET status=?,note=?,prepared=?,reviewed_by=?,reviewed_at=CURRENT_TIMESTAMP WHERE id=?')->execute([$status,$note,$status==='Verified'?1:0,$u['id'],$item['id']]);
            enrollment_history($id,$item['office_name'].' · '.$item['title'].': '.$status.($note!==''?' · '.$note:''));campus_notify((int)$checklist['owner_id'],'Enrollment requirement reviewed','Open your enrollment checklist for office feedback and your next step.');
        }else throw new InvalidArgumentException('Choose a valid checklist action.');
        log_activity($u['id'],'Enrollment checklist updated','Checklist #'.$id.' · '.$action);db()->commit();
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}

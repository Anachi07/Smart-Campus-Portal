<?php
function init_facilities(PDO $pdo): void {
    foreach([
        'CREATE TABLE IF NOT EXISTS campus_resources(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,kind TEXT NOT NULL,location TEXT NOT NULL,capacity INTEGER NOT NULL,office_id INTEGER NOT NULL REFERENCES campus_offices(id),approval_required INTEGER NOT NULL DEFAULT 1,active INTEGER NOT NULL DEFAULT 1,revision INTEGER NOT NULL DEFAULT 0)',
        'CREATE TABLE IF NOT EXISTS campus_bookings(id INTEGER PRIMARY KEY AUTOINCREMENT,resource_id INTEGER NOT NULL REFERENCES campus_resources(id),owner_id INTEGER NOT NULL REFERENCES users(id),starts_at TEXT NOT NULL,ends_at TEXT NOT NULL,purpose TEXT NOT NULL,status TEXT NOT NULL,revision INTEGER NOT NULL DEFAULT 0,request_key TEXT UNIQUE NOT NULL,staff_note TEXT NOT NULL DEFAULT "",created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
        'CREATE TABLE IF NOT EXISTS campus_booking_events(id INTEGER PRIMARY KEY AUTOINCREMENT,booking_id INTEGER NOT NULL REFERENCES campus_bookings(id),actor_id INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
        'CREATE INDEX IF NOT EXISTS resource_booking_times ON campus_bookings(resource_id,status,starts_at,ends_at)'
    ] as $sql)$pdo->exec($sql);
}
function facility_booking_scope(string $b='b',string $r='r',string $o='o'): string {
    $u=current_user();$id=(int)$u['id'];if($u['role']==='Super Admin')return'1=1';return "$b.owner_id=$id".(in_array($u['role'],['Admin','Teacher'],true)?" OR $o.reviewer_id=$id":'');
}
function facility_booking(int $id): ?array {return row('SELECT b.*,r.name AS resource_name,r.office_id,o.reviewer_id FROM campus_bookings b JOIN campus_resources r ON r.id=b.resource_id JOIN campus_offices o ON o.id=r.office_id WHERE b.id=? AND ('.facility_booking_scope().')',[$id]);}
function facility_event(int $id,string $body): void {db()->prepare('INSERT INTO campus_booking_events(booking_id,actor_id,body) VALUES(?,?,?)')->execute([$id,current_user()['id'],$body]);}
function facility_time(string $value): DateTimeImmutable {
    $date=DateTimeImmutable::createFromFormat('!Y-m-d\TH:i',$value,new DateTimeZone('Asia/Manila'));if(!$date||$date->format('Y-m-d\TH:i')!==$value)throw new InvalidArgumentException('Choose valid dates and times in Manila time.');return$date;
}
function facility_create(array $input): int {
    $u=current_user();if(!in_array($u['role'],['Student','Teacher','Admin','Super Admin'],true))throw new InvalidArgumentException('Student or school staff access is required.');
    $key=(string)($input['request_key']??'');if(!preg_match('/^[a-f0-9]{32}$/',$key))throw new InvalidArgumentException('Reload the booking form.');
    $start=facility_time((string)($input['starts_at']??''));$end=facility_time((string)($input['ends_at']??''));$now=new DateTimeImmutable();
    if($start<=$now||$start>$now->modify('+90 days')||$end<=$start||$end->getTimestamp()-$start->getTimestamp()>8*3600)throw new InvalidArgumentException('Book a future start within 90 days and a duration of up to eight hours.');
    $purpose=trim((string)($input['purpose']??''));if(strlen($purpose)<5||strlen($purpose)>1000)throw new InvalidArgumentException('Give a purpose of 5–1000 characters.');
    $start=$start->setTimezone(new DateTimeZone('UTC'))->format('Y-m-d H:i:s');$end=$end->setTimezone(new DateTimeZone('UTC'))->format('Y-m-d H:i:s');$rid=(int)($input['resource_id']??0);
    db()->beginTransaction();try{
        // First statement acquires a write lock; overlapping requests cannot both pass the conflict check.
        $lock=db()->prepare('UPDATE campus_resources SET revision=revision+1 WHERE id=?');$lock->execute([$rid]);if(!$lock->rowCount())throw new InvalidArgumentException('Choose an available facility.');
        $prior=row('SELECT * FROM campus_bookings WHERE request_key=?',[$key]);if($prior){if((int)$prior['owner_id']!==(int)$u['id'])throw new InvalidArgumentException('Reload this booking form.');db()->commit();return(int)$prior['id'];}
        $resource=row('SELECT r.*,o.active AS office_active FROM campus_resources r JOIN campus_offices o ON o.id=r.office_id WHERE r.id=?',[$rid]);if(!$resource['active']||!$resource['office_active'])throw new InvalidArgumentException('This facility is unavailable for new reservations.');
        if(scalar('SELECT COUNT(*) FROM campus_bookings WHERE resource_id=? AND status IN("Pending","Approved") AND starts_at<? AND ends_at>?',[$rid,$end,$start]))throw new InvalidArgumentException('This facility already has a pending or approved reservation during that time.');
        if((int)scalar('SELECT COUNT(*) FROM campus_bookings WHERE owner_id=? AND status IN("Pending","Approved") AND ends_at>datetime("now")',[$u['id']])>=20)throw new InvalidArgumentException('Active reservation limit reached. Cancel unused reservations or contact the office.');
        $status=$resource['approval_required']?'Pending':'Approved';db()->prepare('INSERT INTO campus_bookings(resource_id,owner_id,starts_at,ends_at,purpose,status,request_key) VALUES(?,?,?,?,?,?,?)')->execute([$rid,$u['id'],$start,$end,$purpose,$status,$key]);$id=(int)db()->lastInsertId();facility_event($id,'Reservation created · '.$status);log_activity($u['id'],'Facility reservation created','Reservation #'.$id);db()->commit();return$id;
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function facility_update(int $id,array $input): void {
    $b=facility_booking($id);if(!$b){http_response_code(404);exit('Reservation not found.');}$u=current_user();$action=(string)($input['action']??'');$note=trim((string)($input['note']??''));if(strlen($note)>600)throw new InvalidArgumentException('Keep the note within 600 characters.');
    db()->beginTransaction();try{
        $lock=db()->prepare('UPDATE campus_bookings SET revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=? AND revision=?');$lock->execute([$id,(int)($input['revision']??-1)]);if(!$lock->rowCount())throw new InvalidArgumentException('This reservation changed. Reload before saving.');
        if(!in_array($b['status'],['Pending','Approved'],true)||strtotime($b['starts_at'].' UTC')<=time())throw new InvalidArgumentException('This reservation is closed for changes.');
        if($action==='cancel'&&(int)$b['owner_id']===(int)$u['id'])$status='Cancelled';
        elseif($action==='review'&&can_review_step($b)&&(int)$b['owner_id']!==(int)$u['id']){
            $status=(string)($input['status']??'');if(!in_array($status,['Approved','Rejected','Cancelled'],true)||($status==='Approved'&&$b['status']!=='Pending')||($status!=='Approved'&&strlen($note)<5))throw new InvalidArgumentException('Choose a valid decision. Rejection or cancellation requires a reason.');
            if($status==='Approved'&&scalar('SELECT COUNT(*) FROM campus_bookings WHERE resource_id=? AND id<>? AND status IN("Pending","Approved") AND starts_at<? AND ends_at>?',[$b['resource_id'],$id,$b['ends_at'],$b['starts_at']]))throw new InvalidArgumentException('The reserved time conflicts with another booking.');
        }else throw new InvalidArgumentException('Only the requester can cancel, or the assigned office can review.');
        db()->prepare('UPDATE campus_bookings SET status=?,staff_note=? WHERE id=?')->execute([$status,$note,$id]);facility_event($id,$status.($note!==''?' · '.$note:''));campus_notify((int)$b['owner_id'],'Facility reservation updated','Open your facility reservation for the current office decision.');log_activity($u['id'],'Facility reservation updated','Reservation #'.$id.' · '.$status);db()->commit();
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}

<?php
const LOST_FOUND_CATEGORIES=['Bag','Wallet','Electronics','ID / documents','Clothing','Accessories','Other'];
function init_lost_found(PDO $pdo): void {
 foreach([
  'CREATE TABLE IF NOT EXISTS lost_found_items(id INTEGER PRIMARY KEY AUTOINCREMENT,owner_id INTEGER NOT NULL REFERENCES users(id),kind TEXT NOT NULL,category TEXT NOT NULL,title TEXT NOT NULL,location TEXT NOT NULL,item_date TEXT NOT NULL,private_details TEXT NOT NULL,listing_state TEXT NOT NULL DEFAULT "Pending",resolution TEXT NOT NULL DEFAULT "Open",revision INTEGER NOT NULL DEFAULT 0,submission_key TEXT UNIQUE NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
  'CREATE TABLE IF NOT EXISTS lost_found_claims(id INTEGER PRIMARY KEY AUTOINCREMENT,item_id INTEGER NOT NULL REFERENCES lost_found_items(id),owner_id INTEGER NOT NULL REFERENCES users(id),proof TEXT NOT NULL,state TEXT NOT NULL DEFAULT "Pending",staff_note TEXT NOT NULL DEFAULT "",revision INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,UNIQUE(item_id,owner_id))',
  'CREATE UNIQUE INDEX IF NOT EXISTS lost_found_one_verified ON lost_found_claims(item_id) WHERE state IN("Verified","Released")',
  'CREATE TABLE IF NOT EXISTS lost_found_events(id INTEGER PRIMARY KEY AUTOINCREMENT,item_id INTEGER NOT NULL REFERENCES lost_found_items(id),claim_id INTEGER REFERENCES lost_found_claims(id),actor_id INTEGER NOT NULL REFERENCES users(id),body TEXT NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)',
  'CREATE INDEX IF NOT EXISTS lost_found_listing ON lost_found_items(listing_state,resolution,item_date)'
 ] as $sql)$pdo->exec($sql);
}
function lf_public_item(int $id): ?array {return row('SELECT id,kind,category,title,location,item_date,listing_state,resolution FROM lost_found_items WHERE id=? AND listing_state="Published"',[$id]);}
function lf_private_item(int $id): ?array {return row('SELECT i.*,u.full_name AS reporter FROM lost_found_items i JOIN users u ON u.id=i.owner_id WHERE i.id=?'.(campus_staff()?'':' AND i.owner_id='.(int)current_user()['id']),[$id]);}
function lf_claim(int $id): ?array {return row('SELECT c.*,i.title,i.kind,u.full_name AS claimant FROM lost_found_claims c JOIN lost_found_items i ON i.id=c.item_id JOIN users u ON u.id=c.owner_id WHERE c.id=?'.(campus_staff()?'':' AND c.owner_id='.(int)current_user()['id']),[$id]);}
function lf_event(int $item,?int $claim,string $body): void {db()->prepare('INSERT INTO lost_found_events(item_id,claim_id,actor_id,body) VALUES(?,?,?,?)')->execute([$item,$claim,current_user()['id'],$body]);}
function lf_notify_staff(string $title): void {
 foreach(rows('SELECT u.id FROM users u JOIN roles r ON r.id=u.role_id WHERE u.status="Active" AND r.name IN("Admin","Super Admin") AND u.id<>?',[current_user()['id']]) as $staff)campus_notify((int)$staff['id'],$title,'Open the lost-and-found office workspace to review the update.');
}
function lf_report(array $input): int {
 $u=current_user();$kind=(string)($input['kind']??'');$category=(string)($input['category']??'');$title=trim((string)($input['title']??''));$location=trim((string)($input['location']??''));$date=(string)($input['item_date']??'');$details=trim((string)($input['private_details']??''));$key=(string)($input['submission_key']??'');
 if(!in_array($kind,['Lost','Found'],true)||!in_array($category,LOST_FOUND_CATEGORIES,true)||strlen($title)<3||strlen($title)>100||strlen($location)<2||strlen($location)>120||strlen($details)<5||strlen($details)>1500||!preg_match('/^[a-f0-9]{32}$/',$key))throw new InvalidArgumentException('Provide a type, category, generic title, broad location, and private identifying details.');
 $day=DateTimeImmutable::createFromFormat('!Y-m-d',$date);if(!$day||$day->format('Y-m-d')!==$date||$date>date('Y-m-d'))throw new InvalidArgumentException('Choose a valid date up to today.');
 db()->beginTransaction();try{
  $create=db()->prepare('INSERT OR IGNORE INTO lost_found_items(owner_id,kind,category,title,location,item_date,private_details,submission_key) SELECT ?,?,?,?,?,?,?,? WHERE (SELECT COUNT(*) FROM lost_found_items WHERE owner_id=? AND created_at>=datetime("now","-1 day"))<20');$create->execute([$u['id'],$kind,$category,$title,$location,$date,$details,$key,$u['id']]);
  $item=row('SELECT * FROM lost_found_items WHERE submission_key=?',[$key]);if(!$item||(int)$item['owner_id']!==(int)$u['id'])throw new InvalidArgumentException('Daily report limit reached, or this form expired. Contact the school desk.');
  if($create->rowCount()){lf_event((int)$item['id'],null,'Report submitted for staff moderation.');log_activity($u['id'],'Lost-and-found report created','Report #'.$item['id']);lf_notify_staff('Lost-and-found report awaiting review');}
  db()->commit();return(int)$item['id'];
 }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function lf_moderate(int $id,array $input): void {
 $item=lf_private_item($id);if(!$item){http_response_code(404);exit('Report not found.');}$u=current_user();$action=(string)($input['action']??'');$note=trim((string)($input['note']??''));
 if(strlen($note)>600)throw new InvalidArgumentException('Keep notes within 600 characters.');
 db()->beginTransaction();try{
  $lock=db()->prepare('UPDATE lost_found_items SET revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=? AND revision=?');$lock->execute([$id,(int)($input['revision']??-1)]);if(!$lock->rowCount())throw new InvalidArgumentException('This report changed. Reload before saving.');
  if($action==='publish'&&campus_staff()){
   if($item['resolution']!=='Open'||$item['listing_state']!=='Pending'||($input['safe_publication']??'')!=='1')throw new InvalidArgumentException('Review the public fields and confirm they contain no identifying or contact details.');
   if($item['kind']==='Found'&&($input['confirm_received']??'')!=='1')throw new InvalidArgumentException('Confirm the found item has been received by the school desk before publishing.');
   $title=trim((string)($input['title']??''));$location=trim((string)($input['location']??''));if(strlen($title)<3||strlen($title)>100||strlen($location)<2||strlen($location)>120)throw new InvalidArgumentException('Provide a generic public title and broad location.');
   db()->prepare('UPDATE lost_found_items SET title=?,location=?,listing_state="Published" WHERE id=?')->execute([$title,$location,$id]);lf_event($id,null,$item['kind']==='Found'?'Staff confirmed desk receipt and approved the generic listing.':'Staff approved the generic listing for campus display.');
  }elseif($action==='archive'&&campus_staff()){
   if(strlen($note)<5)throw new InvalidArgumentException('Give a reason for closing this listing.');
   if(scalar('SELECT COUNT(*) FROM lost_found_claims WHERE item_id=? AND state="Verified"',[$id]))throw new InvalidArgumentException('Resolve the verified claim before archiving this listing.');
   db()->prepare('UPDATE lost_found_items SET listing_state="Archived" WHERE id=?')->execute([$id]);
   foreach(rows('SELECT id,owner_id FROM lost_found_claims WHERE item_id=? AND state IN("Pending","Needs information")',[$id]) as $claim){db()->prepare('UPDATE lost_found_claims SET state="Rejected",staff_note="The school closed this listing. Contact the office for assistance.",revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=?')->execute([$claim['id']]);lf_event($id,(int)$claim['id'],'Listing closed by the school.');campus_notify((int)$claim['owner_id'],'Lost-and-found listing closed','Open your private claim for the current status.');}
   lf_event($id,null,'Listing archived: '.$note);
  }elseif($action==='reunited'&&$item['kind']==='Lost'&&$item['resolution']==='Open'&&((int)$item['owner_id']===(int)$u['id']||campus_staff())){
   if(strlen($note)<5)throw new InvalidArgumentException('Add a short explanation after recovering your item.');db()->prepare('UPDATE lost_found_items SET resolution="Returned" WHERE id=?')->execute([$id]);lf_event($id,null,'Lost-item report resolved: '.$note);
  }else throw new InvalidArgumentException('This report action is unavailable.');
  campus_notify((int)$item['owner_id'],'Lost-and-found report updated','Open your report for its current status.');log_activity($u['id'],'Lost-and-found report updated','Report #'.$id.' · '.$action);db()->commit();
 }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function lf_submit_claim(int $itemId,string $proof): int {
 $proof=trim($proof);if(strlen($proof)<10||strlen($proof)>1500)throw new InvalidArgumentException('Describe private ownership evidence in 10–1500 characters. Do not send passwords, PINs, or full ID numbers.');$uid=(int)current_user()['id'];
 db()->beginTransaction();try{
  $lock=db()->prepare('UPDATE lost_found_items SET revision=revision+1 WHERE id=?');$lock->execute([$itemId]);$item=row('SELECT * FROM lost_found_items WHERE id=?',[$itemId]);
  if(!$item||$item['kind']!=='Found'||$item['listing_state']!=='Published'||$item['resolution']!=='Open'||(int)$item['owner_id']===$uid)throw new InvalidArgumentException('This listing is not available for your ownership claim.');
  $prior=scalar('SELECT id FROM lost_found_claims WHERE item_id=? AND owner_id=?',[$itemId,$uid]);if($prior){db()->commit();return(int)$prior;}
  if(scalar('SELECT COUNT(*) FROM lost_found_claims WHERE item_id=? AND state IN("Verified","Released")',[$itemId]))throw new InvalidArgumentException('The office is already processing a verified claim for this item.');
  if((int)scalar('SELECT COUNT(*) FROM lost_found_claims WHERE owner_id=? AND created_at>=datetime("now","-1 day")',[$uid])>=20)throw new InvalidArgumentException('Daily claim limit reached. Contact the school desk.');
  db()->prepare('INSERT INTO lost_found_claims(item_id,owner_id,proof) VALUES(?,?,?)')->execute([$itemId,$uid,$proof]);$id=(int)db()->lastInsertId();lf_event($itemId,$id,'Ownership claim submitted privately.');log_activity($uid,'Lost-and-found claim submitted','Claim #'.$id);lf_notify_staff('New private ownership claim');db()->commit();return$id;
 }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function lf_update_claim(int $id,array $input): void {
 $claim=lf_claim($id);if(!$claim){http_response_code(404);exit('Claim not found.');}$uid=(int)current_user()['id'];$owner=(int)$claim['owner_id']===$uid;$action=(string)($input['action']??'');$note=trim((string)($input['note']??''));if(strlen($note)>600)throw new InvalidArgumentException('Keep review notes within 600 characters.');
 db()->beginTransaction();try{
  $lock=db()->prepare('UPDATE lost_found_claims SET revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=? AND revision=?');$lock->execute([$id,(int)($input['revision']??-1)]);if(!$lock->rowCount())throw new InvalidArgumentException('This claim changed. Reload before saving.');
  $item=row('SELECT * FROM lost_found_items WHERE id=?',[$claim['item_id']]);
  if($action==='withdraw'&&$owner&&in_array($claim['state'],['Pending','Needs information','Verified'],true)){$state='Withdrawn';lf_event((int)$claim['item_id'],$id,'Claim withdrawn by requester.');}
  elseif($action==='resubmit'&&$owner&&$claim['state']==='Needs information'&&$item['listing_state']==='Published'&&$item['resolution']==='Open'){
   $proof=trim((string)($input['proof']??''));if(strlen($proof)<10||strlen($proof)>1500)throw new InvalidArgumentException('Provide corrected ownership evidence of 10–1500 characters.');lf_event((int)$claim['item_id'],$id,'Previous private evidence: '.$claim['proof']);db()->prepare('UPDATE lost_found_claims SET proof=? WHERE id=?')->execute([$proof,$id]);$state='Pending';lf_event((int)$claim['item_id'],$id,'Corrected private ownership evidence resubmitted.');
  }elseif($action==='review'&&campus_staff()&&!$owner&&$item['listing_state']==='Published'&&$item['resolution']==='Open'){
   $state=(string)($input['state']??'');if(!in_array($claim['state'],['Pending','Needs information','Verified'],true)||!in_array($state,['Needs information','Verified','Rejected','Released'],true)||strlen($note)<5)throw new InvalidArgumentException('Choose a valid review decision and provide a note of 5–600 characters.');
   if($state==='Verified'&&scalar('SELECT COUNT(*) FROM lost_found_claims WHERE item_id=? AND id<>? AND state IN("Verified","Released")',[$claim['item_id'],$id]))throw new InvalidArgumentException('Another claim is already verified for this item.');
   if($state==='Released'){
    if($claim['state']!=='Verified'||($input['confirm_handover']??'')!=='1')throw new InvalidArgumentException('Verify the claim first, then confirm the in-person handover.');
    db()->prepare('UPDATE lost_found_items SET resolution="Returned",revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=?')->execute([$claim['item_id']]);
    foreach(rows('SELECT id,owner_id FROM lost_found_claims WHERE item_id=? AND id<>? AND state IN("Pending","Needs information")',[$claim['item_id'],$id]) as $other){db()->prepare('UPDATE lost_found_claims SET state="Rejected",staff_note="The item was released following office verification of another claim.",revision=revision+1,updated_at=CURRENT_TIMESTAMP WHERE id=?')->execute([$other['id']]);lf_event((int)$claim['item_id'],(int)$other['id'],'The item was released after office verification of another claim.');campus_notify((int)$other['owner_id'],'Lost-and-found claim closed','Open your private claim to review the outcome.');}
   }
   lf_event((int)$claim['item_id'],$id,$state.' · '.$note);
  }else throw new InvalidArgumentException('This claim action is unavailable.');
  db()->prepare('UPDATE lost_found_claims SET state=?,staff_note=CASE WHEN ?="review" THEN ? ELSE staff_note END WHERE id=?')->execute([$state,$action,$note,$id]);campus_notify((int)$claim['owner_id'],'Lost-and-found claim updated','Open your private claim for the current decision.');log_activity($uid,'Lost-and-found claim updated','Claim #'.$id.' · '.$state);if($owner)lf_notify_staff('Ownership claim updated');db()->commit();
 }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}

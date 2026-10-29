<?php
require_once __DIR__ . '/../../includes/layout.php'; require_role(['Admin']); check_csrf();
$entities = [
 'users'=>['title'=>'Users','table'=>'users','fields'=>['username'=>'Username','email'=>'Email','full_name'=>'Full Name','role_id'=>'Role ID','status'=>'Status'],'list'=>['id','username','email','full_name','role_id','status']],
 'students'=>['title'=>'Students','table'=>'students','fields'=>['user_id'=>'User ID','student_no'=>'Student No','course_id'=>'Course ID','section_id'=>'Section ID','year_level'=>'Year Level','birthdate'=>'Birthdate','gender'=>'Gender','address'=>'Address','phone'=>'Phone'],'list'=>['id','student_no','course_id','section_id','year_level','gpa','attendance_rate','risk_level']],
 'teachers'=>['title'=>'Teachers','table'=>'teachers','fields'=>['user_id'=>'User ID','employee_no'=>'Employee No','department'=>'Department','specialization'=>'Specialization','phone'=>'Phone'],'list'=>['id','employee_no','department','specialization','phone']],
 'parents'=>['title'=>'Parents','table'=>'parents','fields'=>['user_id'=>'User ID','student_id'=>'Student ID','relationship'=>'Relationship','phone'=>'Phone'],'list'=>['id','user_id','student_id','relationship','phone']],
 'courses'=>['title'=>'Courses','table'=>'courses','fields'=>['code'=>'Code','name'=>'Name','description'=>'Description'],'list'=>['id','code','name','description']],
 'subjects'=>['title'=>'Subjects','table'=>'subjects','fields'=>['code'=>'Code','title'=>'Title','course_id'=>'Course ID','year_level'=>'Year Level','units'=>'Units','teacher_id'=>'Teacher ID'],'list'=>['id','code','title','course_id','year_level','units','teacher_id']],
 'sections'=>['title'=>'Sections','table'=>'sections','fields'=>['name'=>'Name','course_id'=>'Course ID','year_level'=>'Year Level','adviser_teacher_id'=>'Adviser Teacher ID'],'list'=>['id','name','course_id','year_level','adviser_teacher_id']],
 'events'=>['title'=>'Calendar Events','table'=>'events','fields'=>['title'=>'Title','event_date'=>'Event Date','description'=>'Description'],'list'=>['id','title','event_date']],
 'school_years'=>['title'=>'School Years','table'=>'school_years','fields'=>['name'=>'Name','starts_at'=>'Start Date','ends_at'=>'End Date','is_active'=>'Is Active 1/0'],'list'=>['id','name','starts_at','ends_at','is_active']],
 'announcements'=>['title'=>'Announcements','table'=>'announcements','fields'=>['title'=>'Title','body'=>'Body','target_role'=>'Target Role'],'list'=>['id','title','target_role','created_at']],
 'schedules'=>['title'=>'Schedules','table'=>'schedules','fields'=>['subject_id'=>'Subject ID','section_id'=>'Section ID','teacher_id'=>'Teacher ID','day_of_week'=>'Day','start_time'=>'Start','end_time'=>'End','room'=>'Room'],'list'=>['id','subject_id','section_id','teacher_id','day_of_week','start_time','end_time','room']]
];
$entity=(string)($_GET['entity']??'students'); if(!isset($entities[$entity])) $entity='students';
$cfg=$entities[$entity]; $table=$cfg['table']; $error='';
$references=['role_id'=>['roles','name'],'course_id'=>['courses','code'],'section_id'=>['sections','name'],'student_id'=>['students','student_no'],'subject_id'=>['subjects','code'],'teacher_id'=>['teachers','employee_no'],'adviser_teacher_id'=>['teachers','employee_no']];
$choices=[];
foreach($references as $field=>[$ref,$label]) $choices[$field]=option_list($ref,$label);
$choices['user_id']=rows('SELECT id,full_name || " (" || username || ")" AS label FROM users WHERE role_id=? ORDER BY full_name',[role_id(['students'=>'Student','teachers'=>'Teacher','parents'=>'Parent'][$entity]??'Student')]);
$enums=['status'=>['Active','Inactive'],'gender'=>['Female','Male','Other','Prefer not to say'],'year_level'=>['1st Year','2nd Year','3rd Year','4th Year','5th Year'],'risk_level'=>['Low','Medium','High'],'target_role'=>['All','Student','Teacher','Parent'],'day_of_week'=>['Monday','Tuesday','Wednesday','Thursday','Friday','Saturday','Sunday'],'is_active'=>['0','1']];
if($_SERVER['REQUEST_METHOD']==='POST') {
    $action=(string)($_POST['action']??'create'); $id=(int)($_POST['id']??0);
    try {
        if(!in_array($action,['create','update','delete'],true)) throw new InvalidArgumentException('Invalid action.');
        $existing=$id?row("SELECT * FROM $table WHERE id=?",[$id]):null;
        if($action!=='create' && !$existing) throw new InvalidArgumentException('This record no longer exists. Refresh the page and try again.');
        $values=[];
        foreach($cfg['fields'] as $field=>$label) {
            $value=trim((string)($_POST[$field]??''));
            if(strlen($value)>($field==='body'||$field==='description'?5000:200)) throw new InvalidArgumentException("$label is too long.");
            if(isset($choices[$field])) {
                if($value!=='' && !in_array((int)$value,array_map(fn($o)=>(int)$o['id'],$choices[$field]),true)) throw new InvalidArgumentException("Choose a valid $label.");
                $value=$value===''?null:(int)$value;
            }
            if($action!=='delete' && isset($enums[$field]) && !in_array((string)$value,$enums[$field],true)) throw new InvalidArgumentException("Choose a valid $label.");
            $values[$field]=$value;
        }
        if($entity==='users') {
            $isAdmin=$existing && (int)$existing['role_id']===role_id('Admin') && $existing['status']==='Active';
            $removing=$action==='delete'||(int)$values['role_id']!==role_id('Admin')||$values['status']!=='Active';
            if($isAdmin && $removing && (int)scalar('SELECT COUNT(*) FROM users WHERE role_id=? AND status="Active"',[role_id('Admin')])<=1) throw new InvalidArgumentException('Keep at least one active administrator.');
            if($id===(int)$_SESSION['user_id'] && $removing) throw new InvalidArgumentException('You cannot delete, disable, or demote your own account.');
        }
        db()->beginTransaction();
        if($action==='delete') db()->prepare("DELETE FROM $table WHERE id=?")->execute([$id]);
        else {
            foreach(['username','full_name','code','name','student_no','employee_no','title'] as $required) if(array_key_exists($required,$values) && !$values[$required]) throw new InvalidArgumentException(ucwords(str_replace('_',' ',$required)).' is required.');
            if(isset($values['email']) && !filter_var($values['email'],FILTER_VALIDATE_EMAIL)) throw new InvalidArgumentException('Enter a valid email address.');
            foreach(['gpa'=>[1,5],'attendance_rate'=>[0,100],'units'=>[1,12]] as $field=>[$min,$max]) if(isset($values[$field]) && (!is_numeric($values[$field]) || (float)$values[$field]<$min || (float)$values[$field]>$max)) throw new InvalidArgumentException("$field must be between $min and $max.");
            foreach(['birthdate','event_date','starts_at','ends_at'] as $field) if(!empty($values[$field]) && (!preg_match('/^\d{4}-\d{2}-\d{2}$/',$values[$field]) || date('Y-m-d',strtotime($values[$field]))!==$values[$field])) throw new InvalidArgumentException('Choose a valid date.');
            if(!empty($values['starts_at'])&&!empty($values['ends_at'])&&$values['ends_at']<$values['starts_at']) throw new InvalidArgumentException('End date must follow the start date.');
            if($entity==='schedules' && ($values['start_time']>=$values['end_time'] || !preg_match('/^\d{2}:\d{2}$/',$values['start_time']) || !preg_match('/^\d{2}:\d{2}$/',$values['end_time']))) throw new InvalidArgumentException('Choose a valid start and end time.');
            if($entity==='users') {
                if(!preg_match('/^[a-zA-Z0-9._-]{3,60}$/',$values['username'])) throw new InvalidArgumentException('Use 3–60 letters, numbers, dots, dashes, or underscores for the username.');
                if(scalar('SELECT COUNT(*) FROM users WHERE (username=? COLLATE NOCASE OR email=? COLLATE NOCASE) AND id<>?',[$values['username'],$values['email'],$id])) throw new InvalidArgumentException('That username or email is already in use.');
                $password=(string)($_POST['new_password']??'');
                if($action==='create' || $password!=='') {
                    if(strlen($password)<12 || strlen($password)>200) throw new InvalidArgumentException('A new password must have 12–200 characters.');
                    $values['password_hash']=password_hash($password,PASSWORD_DEFAULT);
                    if($existing) $values['auth_version']=(int)$existing['auth_version']+1;
                }
            }
            if(in_array($entity,['students','teachers','parents'],true) && !$values['user_id']) throw new InvalidArgumentException('Choose the linked campus account. Create the account in Users & Roles first.');
            if($entity==='announcements'||$entity==='events') $values['created_by']=$_SESSION['user_id'];
            if($entity==='school_years' && $values['is_active']==='1') db()->exec('UPDATE school_years SET is_active=0');
            if($action==='update') {
                $sets=implode(',',array_map(fn($k)=>"$k=?",array_keys($values)));
                db()->prepare("UPDATE $table SET $sets WHERE id=?")->execute([...array_values($values),$id]);
            } else {
                $cols=implode(',',array_keys($values)); $placeholders=implode(',',array_fill(0,count($values),'?'));
                db()->prepare("INSERT INTO $table($cols) VALUES($placeholders)")->execute(array_values($values));
                $id=(int)db()->lastInsertId();
                if($entity==='users') {
                    $role=row('SELECT name FROM roles WHERE id=?',[$values['role_id']])['name'];
                    if($role==='Student') db()->prepare('INSERT INTO students(user_id,student_no,year_level,gpa,attendance_rate) VALUES(?,?,?,0,0)')->execute([$id,$values['username'],'1st Year']);
                    if($role==='Teacher') db()->prepare('INSERT INTO teachers(user_id,employee_no) VALUES(?,?)')->execute([$id,$values['username']]);
                    if($role==='Parent') db()->prepare('INSERT INTO parents(user_id,relationship) VALUES(?,?)')->execute([$id,'Guardian']);
                }
            }
        }
        if($entity==='users' && $action==='update') {
            $role=row('SELECT name FROM roles WHERE id=?',[$values['role_id']])['name'];
            if($role==='Student') db()->prepare('INSERT OR IGNORE INTO students(user_id,student_no,year_level,gpa,attendance_rate) VALUES(?,?,?,0,0)')->execute([$id,$values['username'],'1st Year']);
            if($role==='Teacher') db()->prepare('INSERT OR IGNORE INTO teachers(user_id,employee_no) VALUES(?,?)')->execute([$id,$values['username']]);
            if($role==='Parent') db()->prepare('INSERT OR IGNORE INTO parents(user_id,relationship) VALUES(?,?)')->execute([$id,'Guardian']);
        }
        log_activity($_SESSION['user_id'],ucfirst($action).' record',"$entity #$id"); db()->commit();
        if($entity==='users' && $id===(int)$_SESSION['user_id'] && isset($values['auth_version'])) $_SESSION['auth_version']=$values['auth_version'];
        set_flash('success',$action==='delete'?'Record deleted.':'Changes saved successfully.'); redirect('manage.php?entity='.$entity);
    } catch(InvalidArgumentException $e) { if(db()->inTransaction()) db()->rollBack(); $error=$e->getMessage(); }
    catch(PDOException $e) { if(db()->inTransaction()) db()->rollBack(); $error='Unable to save. Check required fields, duplicate values, or records linked to this item.'; }
}
$edit=isset($_GET['edit'])?row("SELECT * FROM $table WHERE id=?",[(int)$_GET['edit']]):null;
$form=$error?array_merge($edit??[],$_POST):($edit??[]);
$search=trim((string)($_GET['q']??''));
$data=rows("SELECT * FROM $table ORDER BY id DESC LIMIT 200");
if($search!=='') $data=array_values(array_filter($data,fn($record)=>stripos(implode(' ',array_map('strval',$record)),$search)!==false));
render_header($cfg['title']); ?>
<div class="page-title"><div><span class="eyebrow">CAMPUS MANAGEMENT</span><h1><?= e($cfg['title']) ?></h1><p><?= count($data) ?> records shown · Manage your campus information.</p></div><a href="manage.php?entity=<?= e($entity) ?>" class="btn btn-outline-primary">New record</a></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<div class="row g-4"><div class="col-xl-4"><section class="panel"><h2 class="panel-title"><?= $edit?'Edit record':'Add a record' ?></h2>
<?php if(in_array($entity,['students','teachers','parents'],true)): ?><p class="small text-muted">Create an account in <a href="manage.php?entity=users">Users & Roles</a> first. Its profile is created automatically; edit that profile here.</p><?php endif; ?>
<form method="post"><?= csrf_field() ?><input type="hidden" name="action" value="<?= $edit?'update':'create' ?>"><?php if($edit): ?><input type="hidden" name="id" value="<?= e($edit['id']) ?>"><?php endif; ?>
<?php foreach($cfg['fields'] as $field=>$label): ?><div class="mb-3"><label for="field-<?= e($field) ?>" class="form-label"><?= e(str_replace(' ID','',$label)) ?></label>
<?php if(isset($choices[$field])): ?><select id="field-<?= e($field) ?>" name="<?= e($field) ?>" class="form-select" <?= in_array($field,['role_id','user_id'])?'required':'' ?>><option value="">Select <?= e(str_replace(' ID','',$label)) ?></option><?php foreach($choices[$field] as $option): ?><option value="<?= e($option['id']) ?>" <?= (string)($form[$field]??'')===(string)$option['id']?'selected':'' ?>><?= e($option['label']) ?></option><?php endforeach; ?></select>
<?php elseif(isset($enums[$field])): ?><select id="field-<?= e($field) ?>" name="<?= e($field) ?>" class="form-select"><?php foreach($enums[$field] as $option): ?><option <?= (string)($form[$field]??'')===$option?'selected':'' ?>><?= e($option) ?></option><?php endforeach; ?></select>
<?php elseif(in_array($field,['body','description','address'],true)): ?><textarea id="field-<?= e($field) ?>" name="<?= e($field) ?>" class="form-control" rows="3"><?= e($form[$field]??'') ?></textarea>
<?php else: $type=$field==='email'?'email':(in_array($field,['event_date','birthdate','starts_at','ends_at'])?'date':(in_array($field,['start_time','end_time'])?'time':(in_array($field,['gpa','attendance_rate','units'])?'number':'text'))); ?><input id="field-<?= e($field) ?>" type="<?= $type ?>" name="<?= e($field) ?>" value="<?= e($form[$field]??'') ?>" class="form-control" <?= $type==='number'?'step="0.01"':'' ?> <?= in_array($field,['username','full_name','code','name','student_no','employee_no','title','event_date'],true)?'required':'' ?>><?php endif; ?></div><?php endforeach; ?>
<?php if($entity==='users'): ?><label for="new_password" class="form-label"><?= $edit?'Reset password (optional)':'Initial password' ?></label><input id="new_password" type="password" name="new_password" class="form-control mb-3" minlength="12" maxlength="200" autocomplete="new-password" <?= $edit?'':'required' ?>><small class="text-muted d-block mb-3">At least 12 characters. Share the initial password privately.</small><?php endif; ?>
<button class="btn btn-primary w-100">Save record</button></form></section></div>
<div class="col-xl-8"><section class="panel"><form method="get" class="d-flex gap-2 mb-4"><input type="hidden" name="entity" value="<?= e($entity) ?>"><label class="visually-hidden" for="record-search">Search records</label><input id="record-search" name="q" value="<?= e($search) ?>" class="form-control" placeholder="Search these records…"><button class="btn btn-outline-primary">Search</button></form>
<?php if(!$data): empty_state('No matching records','Add a record or try a different search.'); else: ?><div class="table-responsive"><table class="table table-hover"><thead><tr><?php foreach($cfg['list'] as $col): ?><th><?= e(ucwords(str_replace('_',' ',$col))) ?></th><?php endforeach; ?><th>Actions</th></tr></thead><tbody>
<?php foreach($data as $record): ?><tr><?php foreach($cfg['list'] as $col): $value=$record[$col]??''; if(($col==='gpa'||$col==='attendance_rate')&&(float)$value===0) $value='—'; if(isset($choices[$col])) foreach($choices[$col] as $option) if((string)$option['id']===(string)$value) $value=$option['label']; ?><td><?= e($value) ?></td><?php endforeach; ?><td class="text-nowrap"><a class="btn btn-sm btn-outline-primary" href="manage.php?entity=<?= e($entity) ?>&edit=<?= e($record['id']) ?>" aria-label="Edit record">Edit</a> <form method="post" class="d-inline"><?= csrf_field() ?><input type="hidden" name="action" value="delete"><input type="hidden" name="id" value="<?= e($record['id']) ?>"><button class="btn btn-sm btn-outline-danger confirm-delete" aria-label="Delete record">Delete</button></form></td></tr><?php endforeach; ?></tbody></table></div><?php endif; ?></section></div></div>
<?php render_footer(); ?>

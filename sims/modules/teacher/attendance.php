<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Admin','Teacher']); check_csrf();
$subjects=visible_subjects(); $subject=(int)($_POST['subject_id']??$_GET['subject_id']??($subjects[0]['id']??0));
$students=array_values(array_filter(visible_students(),fn($s)=>has_class_access((int)$s['id'],$subject))); $error='';
if($_SERVER['REQUEST_METHOD']==='POST') {
    $date=(string)($_POST['date']??''); $statuses=$_POST['status']??[];
    if(!is_array($statuses)||!$statuses) $error='Select a class with students before saving attendance.';
    else {
        foreach($statuses as $sid=>$status) { require_class_access((int)$sid,$subject); if(!in_array($status,['Present','Late','Absent','Excused'],true)) $error='Choose a valid attendance status.'; }
        if(!$error) {
            try { foreach($statuses as $sid=>$status) record_attendance((int)$sid,$subject,$date,$status); log_activity($_SESSION['user_id'],'Attendance saved','Class attendance saved for subject #'.$subject); set_flash('success','Attendance saved. Existing records for this class and date were updated.'); redirect('attendance.php?subject_id='.$subject); }
            catch(InvalidArgumentException $e) { $error=$e->getMessage(); }
        }
    }
}
render_header('Attendance desk'); ?>
<div class="page-title"><div><span class="eyebrow">CLASSROOM WORKSPACE</span><h1>Attendance desk</h1><p>Record class attendance and keep student histories up to date.</p></div><a class="btn btn-outline-primary" href="../shared/qr_attendance.php">Student ID check-in</a></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<section class="panel"><form method="get" class="mb-4"><label for="class-subject" class="form-label">Select your subject</label><div class="d-flex gap-2"><select id="class-subject" class="form-select" name="subject_id"><?php foreach($subjects as $sub): ?><option value="<?= e($sub['id']) ?>" <?= (int)$sub['id']===$subject?'selected':'' ?>><?= e($sub['code'].' · '.$sub['title']) ?></option><?php endforeach; ?></select><button class="btn btn-outline-primary">Open class</button></div></form>
<?php if(!$students): empty_state('No students in this class','Ask the administrator to check subject, section, and schedule assignments.'); else: ?>
<form method="post"><?= csrf_field() ?><input type="hidden" name="subject_id" value="<?= $subject ?>"><label for="attendance-date" class="form-label">Attendance date</label><input id="attendance-date" class="form-control mb-4" type="date" name="date" max="<?= date('Y-m-d') ?>" value="<?= e($_POST['date']??date('Y-m-d')) ?>" required>
<div class="table-responsive"><table class="table"><thead><tr><th>Student</th><th>Status</th></tr></thead><tbody><?php foreach($students as $student): $saved=scalar('SELECT status FROM attendance WHERE student_id=? AND subject_id=? AND date=? ORDER BY id DESC LIMIT 1',[$student['id'],$subject,date('Y-m-d')])?:'Present'; ?><tr><td><strong><?= e($student['full_name']) ?></strong><br><small class="text-muted"><?= e($student['student_no']) ?></small></td><td><select class="form-select" name="status[<?= e($student['id']) ?>]" aria-label="Attendance for <?= e($student['full_name']) ?>"><?php foreach(['Present','Late','Absent','Excused'] as $status): ?><option <?= $saved===$status?'selected':'' ?>><?= $status ?></option><?php endforeach; ?></select></td></tr><?php endforeach; ?></tbody></table></div><button class="btn btn-primary mt-3">Save class attendance</button></form><?php endif; ?></section>
<?php render_footer(); ?>

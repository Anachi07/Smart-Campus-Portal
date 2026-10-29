<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Admin','Teacher']); check_csrf(); $error='';
if($_SERVER['REQUEST_METHOD']==='POST') {
    $code=trim((string)($_POST['student_no']??'')); $subject=(int)($_POST['subject_id']??0);
    $student=row('SELECT s.* FROM students s WHERE s.student_no=? AND '.student_scope_sql(),[$code]);
    if(!$student) $error='Student ID not found in your assigned classes.';
    else {
        try { record_attendance((int)$student['id'],$subject,date('Y-m-d'),'Present','Student ID check-in'); set_flash('success',$student['student_no'].' · '.$code.' marked present.'); redirect('qr_attendance.php'); }
        catch(InvalidArgumentException $e) { $error=$e->getMessage(); }
    }
}
$records=rows('SELECT a.*,u.full_name,s.student_no FROM attendance a JOIN students s ON s.id=a.student_id JOIN users u ON u.id=s.user_id LEFT JOIN subjects sub ON sub.id=a.subject_id WHERE '.student_scope_sql().' AND '.subject_scope_sql().' ORDER BY a.id DESC LIMIT 20');
render_header('Student ID check-in'); ?>
<div class="page-title"><div><span class="eyebrow">ATTENDANCE</span><h1>Student ID check-in</h1><p>Enter or scan an assigned student's ID to record today's attendance.</p></div><a class="btn btn-outline-primary" href="../teacher/attendance.php">Class attendance</a></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<div class="row g-4"><div class="col-lg-4"><section class="panel"><h2 class="panel-title">Check in a student</h2><form method="post"><?= csrf_field() ?><label for="check-in-subject" class="form-label">Subject</label><select id="check-in-subject" name="subject_id" class="form-select mb-3" required><?php foreach(visible_subjects() as $sub): ?><option value="<?= e($sub['id']) ?>"><?= e($sub['code'].' · '.$sub['title']) ?></option><?php endforeach; ?></select><label for="student_no" class="form-label">Student ID</label><input id="student_no" name="student_no" class="form-control mb-3" placeholder="Enter student number" autocomplete="off" required><button class="btn btn-primary w-100">Mark present</button></form><button class="btn btn-outline-primary w-100 mt-3" id="startScanner" type="button">Use camera scanner</button><p id="scannerStatus" class="small text-muted mt-3" aria-live="polite">A keyboard barcode scanner or manual ID entry works on any device.</p><video id="scannerVideo" hidden playsinline class="w-100 rounded"></video></section></div><div class="col-lg-8"><section class="panel"><h2 class="panel-title">Recent attendance</h2><?php if(!$records): empty_state('No attendance records'); else: ?><div class="table-responsive"><table class="table"><thead><tr><th>Date</th><th>Student</th><th>Method</th><th>Status</th></tr></thead><tbody><?php foreach($records as $record): ?><tr><td><?= e($record['date']) ?></td><td><?= e($record['student_no'].' · '.$record['full_name']) ?></td><td><?= e($record['method']) ?></td><td><?= e($record['status']) ?></td></tr><?php endforeach; ?></tbody></table></div><?php endif; ?></section></div></div>
<?php render_footer(); ?>

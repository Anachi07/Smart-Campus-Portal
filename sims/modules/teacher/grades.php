<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Admin','Teacher']); check_csrf(); $error='';
if($_SERVER['REQUEST_METHOD']==='POST') {
    $sid=(int)($_POST['student_id']??0); $subject=(int)($_POST['subject_id']??0); require_class_access($sid,$subject);
    $scores=[];
    foreach(['prelim','midterm','final'] as $field) { $value=$_POST[$field]??''; if(!is_numeric($value)||!is_finite((float)$value)||(float)$value<0||(float)$value>100) $error='All grades must be numbers from 0 to 100.'; $scores[]=(float)$value; }
    if(!$error) {
        $average=array_sum($scores)/3;
        db()->beginTransaction();
        try {
            db()->prepare('INSERT INTO grades(student_id,subject_id,prelim,midterm,final,gpa,remarks,encoded_by) VALUES(?,?,?,?,?,?,?,?) ON CONFLICT(student_id,subject_id) DO UPDATE SET prelim=excluded.prelim,midterm=excluded.midterm,final=excluded.final,gpa=excluded.gpa,remarks=excluded.remarks,encoded_by=excluded.encoded_by')->execute([$sid,$subject,...$scores,grade_to_gpa($average),$average>=75?'Passed':'Failed',$_SESSION['user_id']]);
            generate_prediction($sid,false); log_activity($_SESSION['user_id'],'Grades saved','Student #'.$sid.' · Subject #'.$subject); db()->commit(); set_flash('success','Grades saved and academic summary updated.'); redirect('grades.php');
        } catch(Throwable $e) { if(db()->inTransaction()) db()->rollBack(); throw $e; }
    }
}
$records=rows('SELECT g.*,u.full_name,sub.code FROM grades g JOIN students s ON s.id=g.student_id JOIN users u ON u.id=s.user_id JOIN subjects sub ON sub.id=g.subject_id WHERE '.student_scope_sql().' AND '.subject_scope_sql().' ORDER BY g.id DESC');
render_header('Gradebook'); ?>
<div class="page-title"><div><span class="eyebrow">CLASSROOM WORKSPACE</span><h1>Gradebook</h1><p>Encode grades for your assigned classes. Existing entries are updated when saved.</p></div></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<div class="row g-4"><div class="col-xl-4"><section class="panel"><h2 class="panel-title">Encode a grade</h2><form method="post"><?= csrf_field() ?><label class="form-label" for="student">Student</label><select id="student" name="student_id" class="form-select mb-3" required><?php foreach(visible_students() as $student): ?><option value="<?= e($student['id']) ?>"><?= e($student['student_no'].' · '.$student['full_name']) ?></option><?php endforeach; ?></select><label class="form-label" for="subject">Subject</label><select id="subject" name="subject_id" class="form-select mb-3" required><?php foreach(visible_subjects() as $sub): ?><option value="<?= e($sub['id']) ?>"><?= e($sub['code'].' · '.$sub['title']) ?></option><?php endforeach; ?></select><?php foreach(['prelim','midterm','final'] as $field): ?><label class="form-label" for="<?= $field ?>"><?= ucfirst($field) ?></label><input id="<?= $field ?>" class="form-control mb-3" type="number" name="<?= $field ?>" value="<?= e($_POST[$field]??'') ?>" min="0" max="100" step=".01" required><?php endforeach; ?><button class="btn btn-primary w-100">Save grades</button></form></section></div><div class="col-xl-8"><section class="panel"><h2 class="panel-title">Recorded grades</h2><?php if(!$records): empty_state('No grades recorded'); else: ?><div class="table-responsive"><table class="table"><thead><tr><th>Student</th><th>Subject</th><th>Prelim</th><th>Midterm</th><th>Final</th><th>GPA</th><th>Result</th></tr></thead><tbody><?php foreach($records as $grade): ?><tr><?php foreach(['full_name','code','prelim','midterm','final','gpa','remarks'] as $field): ?><td><?= e($grade[$field]) ?></td><?php endforeach; ?></tr><?php endforeach; ?></tbody></table></div><?php endif; ?></section></div></div>
<?php render_footer(); ?>

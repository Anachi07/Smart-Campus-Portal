<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Admin']); check_csrf(); $error='';
$defaults=['school_name'=>'ICAS Philippines','academic_year'=>date('Y').'-'.(date('Y')+1)];
if($_SERVER['REQUEST_METHOD']==='POST') {
    $values=$_POST['settings']??[];
    foreach($defaults as $key=>$default) { $value=trim((string)($values[$key]??'')); if(!$value||strlen($value)>120) $error='Enter a school name and academic year of up to 120 characters.'; $values[$key]=$value; }
    if(!$error) { foreach($defaults as $key=>$default) db()->prepare('INSERT INTO settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value')->execute([$key,$values[$key]]); set_flash('success','School settings saved.'); redirect('settings.php'); }
}
render_header('School settings'); ?>
<div class="page-title"><div><span class="eyebrow">ADMINISTRATION</span><h1>School settings</h1><p>Keep your campus information current.</p></div></div><?php if($error): ?><div class="alert alert-danger"><?= e($error) ?></div><?php endif; ?>
<section class="panel"><form method="post"><?= csrf_field() ?><div class="row g-4"><?php foreach($defaults as $key=>$default): ?><div class="col-md-6"><label for="<?= $key ?>" class="form-label"><?= ucwords(str_replace('_',' ',$key)) ?></label><input id="<?= $key ?>" class="form-control" name="settings[<?= $key ?>]" value="<?= e($_POST['settings'][$key]??setting($key,$default)) ?>" maxlength="120" required></div><?php endforeach; ?></div><button class="btn btn-primary mt-4">Save settings</button></form></section><section class="panel"><h2 class="panel-title">Workspace details</h2><p class="small">Mode: <strong><?= e(campus_mode()) ?></strong>. Attendance and grade thresholds are documented in Academic Insights. Only account notifications are available without an SMS provider.</p><a href="backup.php" class="btn btn-outline-primary">Back up academic records</a></section>
<?php render_footer(); ?>

<?php
require_once __DIR__.'/../../includes/layout.php';require_once __DIR__.'/../../includes/public-school.php';require_role(['Admin']);check_csrf();$error='';$fields=public_school_fields();$values=[];
foreach($fields as $key=>$field)$values[$key]=setting($key,$field[3]);
if($_SERVER['REQUEST_METHOD']==='POST'){
    $input=is_array($_POST['settings']??null)?$_POST['settings']:[];
    foreach($fields as $key=>[$label,$limit,$type,$default]){
        // Preserve public fields when older settings clients submit only the basic fields.
        if(!array_key_exists($key,$input))continue;
        if(!is_string($input[$key])){$error='Enter plain text for '.$label.'.';continue;}
        $values[$key]=trim($input[$key]);
        if(strlen($values[$key])>$limit||($values[$key]===''&&in_array($key,['school_name','academic_year','school_public_name'],true)))$error=$label.' is required where marked and must be within '.$limit.' characters.';
        if($values[$key]!==''&&$type==='email'&&!filter_var($values[$key],FILTER_VALIDATE_EMAIL))$error='Enter a valid public email address.';
        if($values[$key]!==''&&$type==='url'&&!public_school_url($values[$key]))$error='Enter a complete HTTPS URL without embedded login credentials.';
    }
    if(!in_array($values['school_admissions_status'],['Draft','Published'],true))$error='Choose Draft or Published for enrollment information.';
    foreach(['school_admissions_new','school_admissions_transferee','school_admissions_steps'] as $key){$lines=public_school_lines($values[$key]);if(count($lines)>30)$error='Use at most 30 lines per enrollment list.';foreach($lines as $line)if(strlen($line)>500)$error='Keep each requirement or step within 500 characters.';}
    if($values['school_admissions_status']==='Published'&&!public_admissions_ready($values))$error='Before publishing, enter enrollment steps and requirements for at least one applicant type.';
    if(!$error){$pdo=db();$pdo->beginTransaction();try{foreach($values as $key=>$value)$pdo->prepare('INSERT INTO settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value')->execute([$key,$value]);$pdo->commit();}catch(Throwable $e){$pdo->rollBack();throw $e;}set_flash('success','School settings saved.');redirect('settings.php');}
}
render_header('School settings'); ?>
<div class="page-title"><div><span class="eyebrow">ADMINISTRATION</span><h1>School settings</h1><p>Update the school information shown on the public homepage.</p></div><a class="btn btn-outline-primary" href="/">View homepage</a></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<section class="panel"><h2>School information</h2><p>Publish only school-approved information and public contact details. Blank optional fields are hidden on the homepage. HRS and IT remain separate programs; enter their official full names only when confirmed.</p><form method="post"><?= csrf_field() ?><div class="row g-4">
<?php foreach($fields as $key=>[$label,$limit,$type,$default]): $required=in_array($key,['school_name','academic_year','school_public_name'],true); ?><?php if($key==='school_admissions_notice'): ?><div class="col-12" id="public-enrollment-information"><hr><h3>Public enrollment information</h3><p>The admission announcement is public when nonblank; clear it to hide it. The initial notice quotes the supplied Sucat poster for 2026–2027 and does not confirm current availability. Enter school-approved requirements and steps, one item per line. Draft content stays off the homepage. Choose Published only after review. Leave an applicant list blank if its requirements are not yet confirmed.</p></div><?php endif; ?><div class="<?= $type==='textarea'?'col-12':'col-md-6' ?>"><label for="<?= e($key) ?>" class="form-label"><?= e($label) ?><?= $required?' (required)':'' ?></label>
<?php if($type==='select'): ?><select id="<?= e($key) ?>" class="form-select" name="settings[<?= e($key) ?>]"><?php foreach(['Draft','Published'] as $state): ?><option value="<?= e($state) ?>" <?= $values[$key]===$state?'selected':'' ?>><?= e($state) ?></option><?php endforeach; ?></select><?php elseif($type==='textarea'): ?><textarea id="<?= e($key) ?>" class="form-control" name="settings[<?= e($key) ?>]" maxlength="<?= $limit ?>" rows="3"><?= e($values[$key]) ?></textarea><?php else: ?><input id="<?= e($key) ?>" class="form-control" type="<?= e($type) ?>" name="settings[<?= e($key) ?>]" value="<?= e($values[$key]) ?>" maxlength="<?= $limit ?>" <?= $required?'required':'' ?>><?php endif; ?></div><?php endforeach; ?></div><button class="btn btn-primary mt-4">Save settings</button></form></section>
<section class="panel"><h2 class="panel-title">Workspace details</h2><p class="small">Mode: <strong><?= e(campus_mode()) ?></strong>. Attendance and grade thresholds are documented in Academic Insights. Only account notifications are available without an SMS provider.</p><?php if(current_user()['role']==='Super Admin'): ?><a href="backup.php" class="btn btn-outline-primary">Back up academic records</a><?php endif; ?></section>
<?php render_footer(); ?>

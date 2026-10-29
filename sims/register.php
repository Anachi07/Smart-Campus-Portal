<?php
require_once 'includes/auth-layout.php';
if(current_user()) redirect('/portal');
$error=''; $values=['full_name'=>'','username'=>'','email'=>''];
if($_SERVER['REQUEST_METHOD']==='POST') {
    check_csrf(); foreach($values as $key=>$_) $values[$key]=trim((string)($_POST[$key]??''));
    $password=(string)($_POST['password']??'');
    if(isset($_POST['role']) && $_POST['role']!=='Student') { http_response_code(403); exit('Only student accounts can be self-registered.'); }
    if(!$values['full_name'] || strlen($values['full_name'])>120 || !preg_match('/^[a-zA-Z0-9._-]{3,60}$/',$values['username'])) $error='Enter your name and a username of 3–60 letters, numbers, dots, dashes, or underscores.';
    elseif(!filter_var($values['email'],FILTER_VALIDATE_EMAIL)) $error='Enter a valid email address.';
    elseif(strlen($password)<12 || strlen($password)>200) $error='Use a password with 12–200 characters.';
    elseif($password!==($_POST['confirm_password']??'')) $error='Passwords do not match.';
    elseif(scalar('SELECT COUNT(*) FROM users WHERE username=? COLLATE NOCASE OR email=? COLLATE NOCASE',[$values['username'],$values['email']])) $error='That username or email is already registered.';
    else {
        try {
            db()->beginTransaction();
            db()->prepare("INSERT INTO users(username,email,password_hash,role_id,full_name,status) VALUES(?,?,?,?,?,'Pending')")->execute([$values['username'],$values['email'],password_hash($password,PASSWORD_DEFAULT),role_id('Student'),$values['full_name']]);
            $uid=(int)db()->lastInsertId();
            db()->prepare('INSERT INTO students(user_id,student_no,year_level,gpa,attendance_rate) VALUES(?,?,?,0,0)')->execute([$uid,$values['username'],'1st Year']);
            log_activity($uid,'Registration requested','Student account pending Super Admin approval.');
            db()->prepare("INSERT INTO notifications(user_id,title,body) SELECT u.id,?,? FROM users u JOIN roles r ON r.id=u.role_id WHERE r.name='Super Admin' AND u.status='Active'")->execute(['Account approval needed','A new student account is awaiting approval in Accounts & approvals.']); db()->commit();
            set_flash('success','Registration submitted. Wait for Super Admin approval before signing in.'); redirect('login.php');
        } catch(Throwable $e) { if(db()->inTransaction()) db()->rollBack(); $error='Unable to create this account. Check your details and try again.'; }
    }
}
auth_header('Create your account'); ?>
<p class="text-muted">Request a student account. A Super Admin must approve it before you can sign in. Staff accounts are created by the Super Admin.</p>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<form method="post" class="auth-form"><?= csrf_field() ?>
<?php foreach(['full_name'=>'Full name','username'=>'Username / student number','email'=>'Email address'] as $key=>$label): ?><label for="<?= e($key) ?>"><?= e($label) ?></label><input id="<?= e($key) ?>" name="<?= e($key) ?>" type="<?= $key==='email'?'email':'text' ?>" class="form-control" value="<?= e($values[$key]) ?>" maxlength="120" required><?php endforeach; ?>
<label for="password">Password <small>(at least 12 characters)</small></label><input id="password" name="password" type="password" class="form-control" autocomplete="new-password" minlength="12" maxlength="200" required>
<label for="confirm_password">Confirm password</label><input id="confirm_password" name="confirm_password" type="password" class="form-control" autocomplete="new-password" minlength="12" required>
<button class="btn btn-primary w-100 mt-4">Submit for approval</button></form><p class="mt-3 small">Already registered? <a href="login.php">Sign in</a></p>
<?php auth_footer(); ?>

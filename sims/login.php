<?php
require_once 'includes/auth-layout.php';
if (current_user()) redirect('/portal');
$error=''; $username='';
if ($_SERVER['REQUEST_METHOD']==='POST') {
    check_csrf(); $username=trim((string)($_POST['username']??'')); $password=(string)($_POST['password']??'');
    $identity=hash('sha256',strtolower($username));
    $attempt=row('SELECT * FROM login_attempts WHERE identity=?',[$identity]);
    if ($attempt && (int)$attempt['blocked_until']>time()) { $error='Too many attempts. Please try again in 15 minutes.'; http_response_code(429); }
    else {
        $user=row('SELECT * FROM users WHERE username=? COLLATE NOCASE AND status="Active"',[$username]);
        if ($user && password_verify($password,$user['password_hash'])) {
            db()->prepare('DELETE FROM login_attempts WHERE identity=?')->execute([$identity]);
            session_regenerate_id(true);
            $_SESSION=['user_id'=>$user['id'],'role_id'=>$user['role_id'],'auth_version'=>(int)$user['auth_version'],'last_activity'=>time(),'workspace_epoch'=>setting('session_epoch','initial')];
            db()->prepare('UPDATE users SET last_login=CURRENT_TIMESTAMP WHERE id=?')->execute([$user['id']]);
            log_activity($user['id'],'Login','Signed in to campus portal.'); redirect('/portal');
        }
        $failures=$attempt && (int)$attempt['blocked_until']>time()-900 ? (int)$attempt['failures']+1 : 1;
        db()->prepare('INSERT INTO login_attempts(identity,failures,blocked_until) VALUES(?,?,?) ON CONFLICT(identity) DO UPDATE SET failures=excluded.failures,blocked_until=excluded.blocked_until')->execute([$identity,$failures,$failures>=5?time()+900:time()]);
        $error='Invalid username or password.';
    }
}
auth_header('Welcome back'); ?>
<p class="text-muted mb-4">Sign in to your campus account to continue.</p>
<?= flash() ?>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<?php if(isset($_GET['restored'])): ?><div class="alert alert-success">Workspace restored. Sign in with an account from your backup.</div><?php endif; ?>
<?php if(isset($_GET['timeout'])): ?><div class="alert alert-warning">Your session expired. Please sign in again.</div><?php endif; ?>
<form method="post" class="auth-form"><?= csrf_field() ?>
<label for="username">Username / student number</label><input id="username" name="username" class="form-control" value="<?= e($username) ?>" maxlength="80" autocomplete="username" required autofocus>
<label for="password">Password</label><div class="password-field"><input id="password" name="password" type="password" class="form-control" autocomplete="current-password" maxlength="200" required></div>
<p class="small text-muted mt-3">Forgot your password? Ask your Super Admin to reset it.</p>
<button class="btn btn-primary w-100" type="submit">Sign in <span aria-hidden="true">→</span></button></form>
<p class="small text-muted mt-3">Need a student account? <a href="register.php">Request an account</a></p><?php auth_footer(); ?>

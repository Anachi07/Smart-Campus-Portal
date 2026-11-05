<?php
require_once __DIR__.'/includes/auth-layout.php';$challenge=$_SESSION['mfa_challenge']??null;
if(!$challenge||$challenge['expires']<time()){unset($_SESSION['mfa_challenge']);redirect('/sims/login.php');}
$error='';if($_SERVER['REQUEST_METHOD']==='POST') {
    check_csrf();$identity='mfa:'.(int)$challenge['id'];$attempt=row('SELECT * FROM login_attempts WHERE identity=?',[$identity]);
    if($attempt&&(int)$attempt['blocked_until']>time()&&(int)$attempt['failures']>=5){http_response_code(429);$error='Too many attempts. Try again in 15 minutes.';}
    else {$user=row('SELECT * FROM users WHERE id=? AND status="Active"',[$challenge['id']]);
        if($user&&(int)$user['auth_version']===$challenge['version']&&hash_equals(setting('session_epoch','initial'),$challenge['epoch'])&&mfa_verify((int)$user['id'],(string)($_POST['code']??''))) {
            db()->prepare('DELETE FROM login_attempts WHERE identity=?')->execute([$identity]);establish_campus_login($user);redirect('/portal');
        }
        $failures=$attempt&&(int)$attempt['blocked_until']>time()-900?(int)$attempt['failures']+1:1;db()->prepare('INSERT INTO login_attempts(identity,failures,blocked_until) VALUES(?,?,?) ON CONFLICT(identity) DO UPDATE SET failures=excluded.failures,blocked_until=excluded.blocked_until')->execute([$identity,$failures,$failures>=5?time()+900:time()]);$error='Invalid, expired, or already-used code.';
    }
}
auth_header('Verify your sign-in'); ?>
<p>Enter the current code from your authenticator app, or one unused recovery code.</p><?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?><form method="post" class="auth-form"><?= csrf_field() ?><label for="code">Authenticator / recovery code</label><input id="code" name="code" class="form-control" autocomplete="one-time-code" maxlength="40" required autofocus><button class="btn btn-primary">Verify and sign in</button></form><a href="/sims/login.php">Start a new sign-in</a><?php auth_footer(); ?>

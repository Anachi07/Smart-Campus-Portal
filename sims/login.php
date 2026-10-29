<?php
require_once 'bootstrap.php';
if (current_user()) redirect('/portal');
$error = '';
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    check_csrf();
    $username = trim($_POST['username'] ?? '');
    $password = $_POST['password'] ?? '';
    $stmt = db()->prepare('SELECT * FROM users WHERE username=? AND status="Active"');
    $stmt->execute([$username]);
    $user = $stmt->fetch();
    if ($user && password_verify($password, $user['password_hash'])) {
        session_regenerate_id(true);
        $_SESSION['user_id'] = $user['id'];
        $_SESSION['role_id'] = $user['role_id'];
        $_SESSION['last_activity'] = time();
        db()->prepare('UPDATE users SET last_login=CURRENT_TIMESTAMP WHERE id=?')->execute([$user['id']]);
        log_activity($user['id'], 'Login', 'User logged in successfully.');
        redirect('/portal');
    } else { $error = 'Invalid username or password.'; }
}
?>
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Login - Smart Campus Portal</title><link href="/sims/assets/css/bootstrap.min.css" rel="stylesheet"><link href="/sims/assets/css/fontawesome.min.css" rel="stylesheet"><link href="assets/css/style.css" rel="stylesheet"></head><body><section class="landing"><div class="landing-card row g-0"><div class="col-lg-6 landing-left"><img src="assets/images/logo.svg" width="82" class="mb-4"><h1 class="fw-bold">Smart Campus Portal</h1><p><?= APP_LONG_NAME ?></p><p class="mt-5">Student Information, Attendance & Campus Queue Services</p></div><div class="col-lg-6 p-5"><div class="login-box"><h3 class="fw-bold">Welcome Back!</h3><p class="text-muted">Sign in to continue to your account.</p><?php if($error): ?><div class="alert alert-danger"><?= e($error) ?></div><?php endif; ?><?php if(isset($_GET['timeout'])): ?><div class="alert alert-warning">Session expired. Please sign in again.</div><?php endif; ?><form method="post"><?= csrf_field() ?><div class="mb-3"><label class="form-label">Username</label><input name="username" class="form-control"  required autofocus></div><div class="mb-3"><label class="form-label">Password</label><input type="password" name="password" class="form-control"  required></div><div class="d-flex justify-content-between small mb-3"><label><input type="checkbox"> Remember me</label><a href="#">Forgot password?</a></div><button class="btn btn-primary w-100 py-2">Sign In</button></form><p class="text-center mt-3 small">Don't have an account? <a href="register.php">Register here</a></p></div></div></div></section></body></html>

<?php
require_once 'bootstrap.php';
if (current_user()) redirect('dashboard.php');
$error='';
if ($_SERVER['REQUEST_METHOD']==='POST') {
    check_csrf();
    $full = trim($_POST['full_name'] ?? ''); $username = trim($_POST['username'] ?? ''); $email = trim($_POST['email'] ?? ''); $password = $_POST['password'] ?? ''; $role = $_POST['role'] ?? 'Student';
    if (!in_array($role, ['Teacher','Student','Parent'], true)) $error = 'Invalid role selected.';
    elseif (strlen($password) < 6) $error = 'Password must have at least 6 characters.';
    else {
        try {
            $rid = role_id($role);
            $stmt = db()->prepare('INSERT INTO users(username,email,password_hash,role_id,full_name) VALUES(?,?,?,?,?)');
            $stmt->execute([$username,$email,password_hash($password,PASSWORD_DEFAULT),$rid,$full]);
            $uid = (int)db()->lastInsertId();
            if ($role==='Student') db()->prepare('INSERT INTO students(user_id,student_no,year_level) VALUES(?,?,?)')->execute([$uid,$username,'1st Year']);
            if ($role==='Teacher') db()->prepare('INSERT INTO teachers(user_id,employee_no,department) VALUES(?,?,?)')->execute([$uid,$username,'General Education']);
            if ($role==='Parent') db()->prepare('INSERT INTO parents(user_id,relationship) VALUES(?,?)')->execute([$uid,'Guardian']);
            log_activity($uid, 'Registered', 'New '.$role.' account registered.');
            set_flash('success','Registration successful. You can now log in.'); redirect('login.php');
        } catch (Throwable $e) { $error = 'Registration failed: username or email may already exist.'; }
    }
}
?>
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Register - Smart SIMS</title><link href="/sims/assets/css/bootstrap.min.css" rel="stylesheet"><link href="/sims/assets/css/fontawesome.min.css" rel="stylesheet"><link href="assets/css/style.css" rel="stylesheet"></head><body><section class="landing"><div class="landing-card p-5" style="max-width:650px"><div class="text-center mb-4"><img src="assets/images/logo.svg" width="72"><h3 class="fw-bold mt-2">Create Smart SIMS Account</h3><p class="text-muted">Admin accounts are only created by the school.</p></div><?php if($error): ?><div class="alert alert-danger"><?= e($error) ?></div><?php endif; ?><form method="post" class="row g-3"><?= csrf_field() ?><div class="col-md-6"><label class="form-label">Full Name</label><input name="full_name" class="form-control" required></div><div class="col-md-6"><label class="form-label">Username / ID Number</label><input name="username" class="form-control" required></div><div class="col-md-6"><label class="form-label">Email</label><input type="email" name="email" class="form-control" required></div><div class="col-md-6"><label class="form-label">Role</label><select name="role" class="form-select"><option>Student</option><option>Teacher</option><option>Parent</option></select></div><div class="col-12"><label class="form-label">Password</label><input type="password" name="password" class="form-control" required></div><div class="col-12"><button class="btn btn-primary w-100">Register</button></div></form><p class="text-center mt-3 small">Already have an account? <a href="login.php">Login</a></p></div></section></body></html>

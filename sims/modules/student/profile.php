<?php
require_once __DIR__.'/../../includes/layout.php'; require_login(); check_csrf(); $user=current_user(); $error='';
if($_SERVER['REQUEST_METHOD']==='POST') {
    if(($_POST['action']??'profile')==='password') {
        $password=(string)($_POST['new_password']??'');
        if(!password_verify($_POST['current_password']??'',$user['password_hash'])) $error='Your current password is incorrect.';
        elseif(strlen($password)<12 || strlen($password)>200) $error='Use a new password with 12–200 characters.';
        elseif($password!==($_POST['confirm_password']??'')) $error='New passwords do not match.';
        else {
            db()->prepare('UPDATE users SET password_hash=?,auth_version=auth_version+1 WHERE id=?')->execute([password_hash($password,PASSWORD_DEFAULT),$user['id']]);
            $_SESSION['auth_version']=(int)$user['auth_version']+1; session_regenerate_id(true);
            log_activity($user['id'],'Password changed','Other sessions were invalidated.'); set_flash('success','Password changed. Your other sessions have been signed out.'); redirect('profile.php');
        }
    } else {
        $name=trim((string)($_POST['full_name']??'')); $email=trim((string)($_POST['email']??''));
        if(!$name || strlen($name)>120 || !filter_var($email,FILTER_VALIDATE_EMAIL)) $error='Enter a name and a valid email address.';
        else {
            try { db()->prepare('UPDATE users SET full_name=?,email=? WHERE id=?')->execute([$name,$email,$user['id']]); set_flash('success','Profile updated.'); redirect('profile.php'); }
            catch(PDOException $e) { $error='That email is already in use. Choose another email address.'; }
        }
    }
}
render_header('My profile'); ?>
<div class="page-title"><div><span class="eyebrow">YOUR ACCOUNT</span><h1>My profile</h1><p>Manage your personal details and account security.</p></div></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<div class="row g-4"><div class="col-lg-4"><div class="panel text-center"><img src="<?= asset('images/default-avatar.svg') ?>" class="avatar-big" alt=""><h3 class="mt-3"><?= e($user['full_name']) ?></h3><span class="badge-soft risk-Low"><?= e($user['role']) ?></span><p class="small text-muted mt-3"><?= e($user['username']) ?></p></div></div><div class="col-lg-8"><div class="panel"><h2 class="panel-title">Personal details</h2><form method="post"><?= csrf_field() ?><input type="hidden" name="action" value="profile"><label for="full_name">Full name</label><input id="full_name" name="full_name" class="form-control mb-3" value="<?= e($user['full_name']) ?>" maxlength="120" required><label for="email">Email address</label><input id="email" type="email" name="email" class="form-control mb-3" value="<?= e($user['email']) ?>" required><button class="btn btn-primary">Save changes</button></form></div><div class="panel"><h2 class="panel-title">Change password</h2><p class="text-muted small">Use at least 12 characters. Changing your password signs out other sessions.</p><form method="post"><?= csrf_field() ?><input type="hidden" name="action" value="password"><label for="current_password">Current password</label><input id="current_password" name="current_password" type="password" class="form-control mb-3" autocomplete="current-password" required><?php foreach(['new_password'=>'New password','confirm_password'=>'Confirm new password'] as $field=>$label): ?><label for="<?= $field ?>"><?= $label ?></label><input id="<?= $field ?>" name="<?= $field ?>" type="password" class="form-control mb-3" autocomplete="new-password" minlength="12" maxlength="200" required><?php endforeach; ?><button class="btn btn-outline-primary">Update password</button></form></div></div></div>
<?php render_footer(); ?>

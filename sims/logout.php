<?php
require_once 'includes/auth-layout.php';
if(!current_user()) redirect('login.php');
if($_SERVER['REQUEST_METHOD']==='POST') { check_csrf(); log_activity($_SESSION['user_id'],'Logout','Signed out of campus portal.'); destroy_campus_session(); redirect('login.php'); }
auth_header('Sign out?'); ?><p>You'll be signed out of student information and queue services.</p><form method="post"><?= csrf_field() ?><button class="btn btn-primary w-100">Sign out</button></form><a href="/portal" class="btn btn-link w-100 mt-3">Stay signed in</a><?php auth_footer(); ?>

<?php require_once 'bootstrap.php'; if(current_user()) log_activity($_SESSION['user_id'], 'Logout', 'User logged out.'); session_destroy(); redirect('login.php'); ?>

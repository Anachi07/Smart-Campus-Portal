<?php
require_once __DIR__ . '/../../includes/layout.php'; require_role(['Admin']); check_csrf();
if($_SERVER['REQUEST_METHOD']==='POST' && isset($_FILES['dbfile'])){
    if($_FILES['dbfile']['error']===UPLOAD_ERR_OK && str_ends_with($_FILES['dbfile']['name'], '.db')){
        copy(DB_PATH, DB_PATH.'.before-restore-'.date('YmdHis').'.bak');
        move_uploaded_file($_FILES['dbfile']['tmp_name'], DB_PATH);
        set_flash('success','Database restored. Please log in again.');
        session_destroy(); redirect_root('login.php');
    } else { set_flash('danger','Please upload a valid .db SQLite backup file.'); }
}
render_header('Restore Database');
?>
<div class="page-title"><div><h1>Restore Database</h1><p>Upload a SQLite .db backup file to restore the system database.</p></div></div><div class="panel"><div class="alert alert-warning"><strong>Warning:</strong> Restoring replaces the active database. A safety backup is created first.</div><form method="post" enctype="multipart/form-data"><?= csrf_field() ?><div class="mb-3"><label class="form-label">SQLite Backup File (.db)</label><input type="file" name="dbfile" class="form-control" accept=".db" required></div><button class="btn btn-danger"><i class="fa-solid fa-upload me-2"></i>Restore Database</button></form></div><?php render_footer(); ?>

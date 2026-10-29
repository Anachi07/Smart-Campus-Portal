<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Super Admin']); check_csrf(); require_once __DIR__.'/../../includes/backup.php'; $error='';
if($_SERVER['REQUEST_METHOD']==='POST') {
    $stageDb=null; $stageQueue=null;
    try {
        if(($_POST['confirm_restore']??'')!=='RESTORE') throw new InvalidArgumentException('Type RESTORE to confirm replacement.');
        $file=$_FILES['backup']??null;
        if(!$file || $file['error']!==UPLOAD_ERR_OK || $file['size']>10*1024*1024) throw new InvalidArgumentException('Upload a workspace JSON backup smaller than 10 MB.');
        $backup=json_decode(file_get_contents($file['tmp_name']),true,512,JSON_THROW_ON_ERROR);
        if(($backup['format']??'')!=='smart-campus-backup-v1' || !is_array($backup['queue_data']??null)) throw new InvalidArgumentException('This is not a Smart Campus workspace backup.');
        $bytes=base64_decode($backup['academic_database']??'',true);
        if(!$bytes || !str_starts_with($bytes,"SQLite format 3\0")) throw new InvalidArgumentException('The backup does not contain a valid academic database.');
        $stageDb=tempnam(dirname(DB_PATH),'.restore-'); file_put_contents($stageDb,$bytes); validate_campus_database($stageDb);
        $probe=new PDO('sqlite:'.$stageDb); $probe->prepare('INSERT INTO settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value')->execute(['session_epoch',bin2hex(random_bytes(16))]); $probe=null;
        $queue=json_encode($backup['queue_data'],JSON_THROW_ON_ERROR);
        if(!queue_backup_request($queue)) throw new InvalidArgumentException('The queue backup is invalid or the queue service is unavailable. No records were changed.');
        $stageQueue=tempnam(dirname(QUEUE_DATA_PATH),'.restore-'); file_put_contents($stageQueue,$queue);
        $suffix='.before-restore-'.date('YmdHis').'-'.bin2hex(random_bytes(3)).'.bak';
        if(!copy(DB_PATH,DB_PATH.$suffix)||!copy(QUEUE_DATA_PATH,QUEUE_DATA_PATH.$suffix)) throw new RuntimeException('Unable to create the safety backup.');
        try {
            if(!rename($stageDb,DB_PATH)||!rename($stageQueue,QUEUE_DATA_PATH)) throw new RuntimeException('Unable to replace workspace files.');
            $stageDb=null; $stageQueue=null;
            if(!queue_backup_request()) throw new RuntimeException('Queue service could not reload restored records.');
        } catch(Throwable $failure) { copy(DB_PATH.$suffix,DB_PATH); copy(QUEUE_DATA_PATH.$suffix,QUEUE_DATA_PATH); queue_backup_request(); throw $failure; }
        destroy_campus_session(); redirect_root('login.php?restored=1');
    } catch(InvalidArgumentException $e) { $error=$e->getMessage(); }
    catch(Throwable $e) { $error='Unable to restore this backup. No replacement was completed; check that the backup is valid and both services are running.'; error_log('Workspace restore failed: '.$e->getMessage()); }
    finally { if($stageDb && file_exists($stageDb)) unlink($stageDb); if($stageQueue && file_exists($stageQueue)) unlink($stageQueue); }
}
render_header('Restore workspace'); ?>
<div class="page-title"><div><span class="eyebrow">ADMINISTRATION</span><h1>Restore workspace</h1><p>Restore academic records and queues from a complete workspace backup.</p></div></div>
<?php if($error): ?><div class="alert alert-danger" role="alert"><?= e($error) ?></div><?php endif; ?>
<section class="panel"><div class="alert alert-warning">Restoring replaces current academic and queue records. Safety copies are created first. All users should sign out before restoring. You will sign in with an account from the restored backup.</div><form method="post" enctype="multipart/form-data"><?= csrf_field() ?><label for="backup" class="form-label">Workspace backup (.json, up to 10 MB)</label><input id="backup" type="file" name="backup" accept=".json,application/json" class="form-control mb-3" required><label for="confirm_restore" class="form-label">Type RESTORE to confirm</label><input id="confirm_restore" name="confirm_restore" class="form-control mb-3" autocomplete="off" pattern="RESTORE" required><button class="btn btn-danger">Restore workspace</button><a class="btn btn-outline-primary ms-2" href="backup.php">Download a backup first</a></form></section>
<?php render_footer(); ?>

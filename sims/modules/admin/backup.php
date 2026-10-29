<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Admin']);
if(isset($_GET['download'])) {
    $temporary=tempnam(dirname(DB_PATH),'.backup-'); unlink($temporary);
    try {
        db()->exec('VACUUM INTO '.db()->quote($temporary));
        $queue=file_get_contents(QUEUE_DATA_PATH); json_decode($queue,true,512,JSON_THROW_ON_ERROR);
        $backup=['format'=>'smart-campus-backup-v1','created_at'=>date(DATE_ATOM),'academic_database'=>base64_encode(file_get_contents($temporary)),'queue_data'=>json_decode($queue,true,512,JSON_THROW_ON_ERROR)];
        header('Content-Type: application/json'); header('Content-Disposition: attachment; filename="smart-campus-backup-'.date('Ymd-His').'.json"'); header('Cache-Control: no-store');
        echo json_encode($backup,JSON_THROW_ON_ERROR); unlink($temporary); exit;
    } finally { if(file_exists($temporary)) unlink($temporary); }
}
render_header('Backup workspace'); ?>
<div class="page-title"><div><span class="eyebrow">ADMINISTRATION</span><h1>Backup workspace</h1><p>Keep a copy of academic records and queue data together.</p></div></div><section class="panel"><h2 class="panel-title">Download a complete backup</h2><p class="small text-muted">Includes the academic SQLite database and queue JSON data. Keep backups private: they contain account information and academic records. On free hosting, download a backup before redeploying if you need to retain demo changes.</p><a href="backup.php?download=1" class="btn btn-primary">Download workspace backup</a><a href="restore.php" class="btn btn-outline-primary ms-2">Restore a backup</a></section>
<?php render_footer(); ?>

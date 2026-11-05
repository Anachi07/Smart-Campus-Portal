<?php
require_once __DIR__.'/../../includes/layout.php'; require_role(['Super Admin']);
if(isset($_GET['download'])) {
    require_once __DIR__.'/../../includes/backup.php';
    header('Content-Type: application/json');header('Content-Disposition: attachment; filename="smart-campus-backup-'.date('Ymd-His').'.json"');header('Cache-Control: no-store');echo json_encode(complete_workspace_backup(),JSON_THROW_ON_ERROR);exit;
}
require_once __DIR__.'/../../includes/backup.php';
$backupStatus=null;$statusPath=campus_backup_dir().'/status.json';if(is_file($statusPath))$backupStatus=json_decode(file_get_contents($statusPath),true);
render_header('Backup workspace'); ?>
<div class="page-title"><div><span class="eyebrow">ADMINISTRATION</span><h1>Backup workspace</h1><p>Keep a copy of academic records, queue data, and private requirement uploads together.</p></div></div><section class="panel"><h2 class="panel-title">Download a complete backup</h2><p class="small text-muted">Includes the academic SQLite database, queue JSON data, private requirement uploads, and their review history. Keep backups private: they contain account information and academic records. On free hosting, download a backup before redeploying if you need to retain demo changes.</p><a href="backup.php?download=1" class="btn btn-primary">Download workspace backup</a><a href="restore.php" class="btn btn-outline-primary ms-2">Restore a backup</a></section>
<section class="panel"><h2>Scheduled local backups</h2><p>While the portal launcher is running, a complete backup starts after one minute, then daily by default. The newest seven are retained. A sleeping free service cannot run its backup schedule.</p><?php if(!empty($backupStatus['error'])): ?><div class="alert alert-danger"><?= e($backupStatus['error']) ?></div><?php endif; ?><?php if(!empty($backupStatus['last_success'])): ?><p>Last successful snapshot: <?= e($backupStatus['last_success']) ?> · <?= (int)$backupStatus['retained'] ?> retained</p><p><?= e($backupStatus['storage']) ?></p><?php else: ?><p>No successful scheduled backup has been recorded yet. Use Download workspace backup to keep a private copy now.</p><?php endif; ?><p>Free Render disks are ephemeral. Local snapshots on the same disk are not off-site backups and may disappear on redeploy.</p></section>
<?php render_footer(); ?>

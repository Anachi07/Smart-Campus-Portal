<?php
require_once __DIR__ . '/../bootstrap.php';
function render_header(string $title='Dashboard'): void {
    $user = current_user();
    $role = $user['role'] ?? 'Guest';
    $unread = $user ? scalar('SELECT COUNT(*) FROM notifications WHERE (user_id=? OR user_id IS NULL) AND is_read=0', [$user['id']]) : 0;
?>
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title><?= e($title) ?> - <?= APP_NAME ?></title>
    <link href="/sims/assets/css/bootstrap.min.css" rel="stylesheet">
    <link href="/sims/assets/css/fontawesome.min.css" rel="stylesheet">
    <link href="<?= asset('css/style.css') ?>" rel="stylesheet">
</head>
<body>
<div class="app-shell">
<?php render_sidebar($role); ?>
<main class="main-content">
    <nav class="topbar shadow-sm">
        <button class="btn btn-light d-lg-none" id="sidebarToggle"><i class="fa-solid fa-bars"></i></button>
        <div class="search-box d-none d-md-flex"><i class="fa-solid fa-search"></i><input id="globalSearch" placeholder="Search anything..."></div>
        <div class="ms-auto d-flex align-items-center gap-3">
            <button class="btn btn-sm btn-light rounded-pill" id="darkModeToggle"><i class="fa-solid fa-moon"></i></button>
            <div class="dropdown">
                <button class="btn btn-light position-relative" data-bs-toggle="dropdown"><i class="fa-solid fa-bell"></i><?php if($unread): ?><span class="badge rounded-pill bg-danger bell-badge"><?= e($unread) ?></span><?php endif; ?></button>
                <div class="dropdown-menu dropdown-menu-end notification-menu p-2">
                    <?php foreach(rows('SELECT * FROM notifications WHERE user_id=? OR user_id IS NULL ORDER BY created_at DESC LIMIT 5', [$user['id'] ?? 0]) as $n): ?>
                    <div class="small p-2 border-bottom"><strong><?= e($n['title']) ?></strong><br><span class="text-muted"><?= e($n['body']) ?></span></div>
                    <?php endforeach; ?>
                </div>
            </div>
            <div class="dropdown">
                <button class="btn user-chip" data-bs-toggle="dropdown"><img src="<?= asset('images/' . ($user['profile_pic'] ?? 'default-avatar.svg')) ?>" onerror="this.src='<?= asset('images/default-avatar.svg') ?>'"> <span><?= e($user['full_name'] ?? 'Guest') ?></span></button>
                <ul class="dropdown-menu dropdown-menu-end">
                    <li><span class="dropdown-item-text small text-muted"><?= e($role) ?></span></li>
                    <li><a class="dropdown-item" href="<?= root_href('modules/student/profile.php') ?>"><i class="fa-solid fa-user me-2"></i>Profile</a></li>
                    <li><a class="dropdown-item" href="<?= root_href('logout.php') ?>"><i class="fa-solid fa-right-from-bracket me-2"></i>Logout</a></li>
                </ul>
            </div>
        </div>
    </nav>
    <div class="page-body">
        <?= flash() ?>
<?php }

function render_sidebar(string $role): void { 
    $baseLinks = [ ['dashboard.php','fa-house','Dashboard'], ['modules/shared/calendar.php','fa-calendar','Calendar'], ['modules/shared/notifications.php','fa-bell','Notifications'] ];
    $links = $baseLinks;
    if ($role === 'Admin') {
        $links = array_merge($links, [
            ['modules/admin/manage.php?entity=students','fa-user-graduate','Students'],
            ['modules/admin/manage.php?entity=teachers','fa-chalkboard-user','Teachers'],
            ['modules/admin/manage.php?entity=parents','fa-users','Parents'],
            ['modules/admin/manage.php?entity=courses','fa-layer-group','Courses'],
            ['modules/admin/manage.php?entity=subjects','fa-book','Subjects'],
            ['modules/admin/manage.php?entity=sections','fa-diagram-project','Sections'],
            ['modules/admin/manage.php?entity=school_years','fa-school','School Years'],
            ['modules/admin/manage.php?entity=users','fa-user-shield','Users & Roles'],
            ['modules/admin/manage.php?entity=announcements','fa-bullhorn','Announcements'],
            ['modules/shared/attendance_ai.php','fa-camera','AI Attendance'],
            ['modules/shared/qr_attendance.php','fa-qrcode','QR Attendance'],
            ['modules/shared/predictive.php','fa-chart-line','Predictive Analytics'],
            ['modules/shared/reports.php','fa-file-export','Reports'],
            ['modules/admin/backup.php','fa-database','Backup Database'],
            ['modules/admin/restore.php','fa-upload','Restore Database'],
            ['modules/admin/logs.php','fa-clock-rotate-left','Activity Logs'],
            ['modules/admin/settings.php','fa-gear','Settings']
        ]);
    } elseif ($role === 'Teacher') {
        $links = array_merge($links, [
            ['modules/teacher/students.php','fa-user-graduate','My Students'],
            ['modules/teacher/attendance.php','fa-clipboard-check','Manage Attendance'],
            ['modules/teacher/grades.php','fa-pen-to-square','Encode Grades'],
            ['modules/teacher/announcements.php','fa-bullhorn','Announcements'],
            ['modules/teacher/at_risk.php','fa-triangle-exclamation','At-Risk Students'],
            ['modules/shared/attendance_ai.php','fa-camera','AI Attendance'],
            ['modules/shared/qr_attendance.php','fa-qrcode','QR Attendance'],
            ['modules/shared/reports.php','fa-file-export','Reports']
        ]);
    } elseif ($role === 'Student') {
        $links = array_merge($links, [
            ['modules/student/grades.php','fa-ranking-star','My Grades'],
            ['modules/student/attendance.php','fa-calendar-check','My Attendance'],
            ['modules/student/subjects.php','fa-book-open','My Subjects'],
            ['modules/student/schedule.php','fa-clock','My Schedule'],
            ['modules/student/profile.php','fa-id-card','Profile'],
            ['modules/student/reports.php','fa-download','Download Reports']
        ]);
    } elseif ($role === 'Parent') {
        $links = array_merge($links, [
            ['modules/parent/monitor.php','fa-child','Student Monitoring'],
            ['modules/shared/reports.php','fa-file-export','Reports']
        ]);
    }
?>
<aside class="sidebar" id="sidebar">
    <a class="brand" href="<?= root_href('dashboard.php') ?>"><img src="<?= asset('images/logo.svg') ?>" alt="Smart SIMS"><div><strong>Smart SIMS</strong><span>AI Portal System</span></div></a>
    <div class="side-section">Campus Portal</div>
    <a class="nav-link" href="/portal"><i class="fa-solid fa-house"></i> Portal Home</a>
    <a class="nav-link" href="<?= $role === 'Admin' ? '/Admin/Dashboard' : '/Queue/GetTicket' ?>"><i class="fa-solid fa-ticket"></i> SmartQueue</a>
    <div class="side-section">Student Information</div>
    <ul class="nav flex-column">
        <?php foreach($links as $l): ?>
        <li class="nav-item"><a class="nav-link" href="<?= e(root_href($l[0])) ?>"><i class="fa-solid <?= e($l[1]) ?>"></i><span><?= e($l[2]) ?></span></a></li>
        <?php endforeach; ?>
    </ul>
</aside>
<?php }

function render_footer(): void { ?>
    </div>
</main>
</div>
<script src="/sims/assets/js/bootstrap.bundle.min.js"></script>
<script src="/sims/assets/js/chart.umd.min.js"></script>
<script src="<?= asset('js/app.js') ?>"></script>
</body>
</html>
<?php }

function card_stat(string $title, string $value, string $icon, string $class='primary', string $sub=''): void { ?>
<div class="col-sm-6 col-xl-3"><div class="stat-card"><div class="stat-icon bg-<?= e($class) ?>"><i class="fa-solid <?= e($icon) ?>"></i></div><div><span><?= e($title) ?></span><h3><?= e($value) ?></h3><small><?= e($sub) ?></small></div></div></div>
<?php }
?>

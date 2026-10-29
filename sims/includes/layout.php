<?php
require_once __DIR__ . '/../bootstrap.php';
function render_header(string $title='Dashboard'): void {
    $user = current_user();
    $role = $user['role'] ?? 'Guest';
    $school=setting('school_name','ICAS Philippines');
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
    <link href="<?= asset('css/portal.css') ?>" rel="stylesheet">
    <link href="<?= asset('css/icas.css') ?>" rel="stylesheet">
    <link rel="icon" href="/sims/brand-logo.php">
    <meta name="theme-color" content="#104b2b">
</head>
<body>
<a class="skip-link" href="#main-content">Skip to content</a><div class="app-shell"><button class="sidebar-overlay" id="sidebarOverlay" aria-label="Close navigation" hidden></button>
<?php render_sidebar($role); ?>
<main class="main-content" id="main-content">
    <nav class="topbar shadow-sm">
        <button class="btn btn-light d-lg-none" id="sidebarToggle" aria-label="Open navigation" aria-controls="sidebar" aria-expanded="false"><i class="fa-solid fa-bars"></i></button>
        <a class="mobile-school-name d-md-none" href="/portal">ICAS <span>PHILIPPINES</span></a><div class="search-box d-none d-md-flex"><i class="fa-solid fa-search"></i><input id="globalSearch" placeholder="Filter this page…" aria-label="Filter records on this page"></div>
        <div class="ms-auto d-flex align-items-center gap-3">
            <a class="help-link" href="/sims/modules/shared/help.php" aria-label="Help and guidance"><i class="fa-regular fa-circle-question"></i></a>
            <div class="dropdown">
                <button class="btn btn-light position-relative" data-bs-toggle="dropdown" aria-label="Notifications"><i class="fa-solid fa-bell"></i><?php if($unread): ?><span class="badge rounded-pill bg-danger bell-badge"><?= e($unread) ?></span><?php endif; ?></button>
                <div class="dropdown-menu dropdown-menu-end notification-menu p-2">
                    <?php foreach(rows('SELECT * FROM notifications WHERE user_id=? OR user_id IS NULL ORDER BY created_at DESC LIMIT 5', [$user['id'] ?? 0]) as $n): ?>
                    <div class="small p-2 border-bottom"><strong><?= e($n['title']) ?></strong><br><span class="text-muted"><?= e($n['body']) ?></span></div>
                    <?php endforeach; ?>
                </div>
            </div>
            <div class="dropdown">
                <button class="btn user-chip" data-bs-toggle="dropdown" aria-label="Open account menu"><img src="<?= asset('images/' . ($user['profile_pic'] ?? 'default-avatar.svg')) ?>" onerror="this.src='<?= asset('images/default-avatar.svg') ?>'"> <span><?= e($user['full_name'] ?? 'Guest') ?></span></button>
                <ul class="dropdown-menu dropdown-menu-end">
                    <li><span class="dropdown-item-text small text-muted"><?= e($role) ?></span></li>
                    <li><a class="dropdown-item" href="<?= root_href('modules/student/profile.php') ?>"><i class="fa-solid fa-user me-2"></i>Profile</a></li>
                    <li><form method="post" action="/sims/logout.php"><?= csrf_field() ?><button class="dropdown-item" type="submit"><i class="fa-solid fa-right-from-bracket me-2"></i>Sign out</button></form></li>
                </ul>
            </div>
        </div>
    </nav>
    <div class="page-body">

        <?= flash() ?>
<?php }

function render_sidebar(string $role): void {
    $uri=parse_url($_SERVER['REQUEST_URI'],PHP_URL_PATH);
    $active=function(string $path) use($uri): bool { $url=str_starts_with($path,'/')?$path:root_href($path);parse_str(parse_url($url,PHP_URL_QUERY)??'', $query);return $uri===parse_url($url,PHP_URL_PATH) && (!isset($query['entity']) || ($_GET['entity']??'')===$query['entity']); };
    $link=function(array $item) use($active): void { [$path,$icon,$label]=$item; echo '<a class="nav-link '.($active($path)?'active':'').'" href="'.e(str_starts_with($path,'/')?$path:root_href($path)).'"><i class="fa-solid '.e($icon).'" aria-hidden="true"></i><span>'.e($label).'</span></a>'; };
    $groups=[];
    if(in_array($role,['Admin','Super Admin'],true)) {
        $groups=[
            'People & access'=>[['modules/admin/manage.php?entity=students','fa-user-graduate','Students'],['modules/admin/manage.php?entity=teachers','fa-chalkboard-user','Teachers'],['modules/admin/manage.php?entity=parents','fa-users','Parents']],
            'Office services'=>[['/Services/Requests','fa-file-lines','Document requests'],['/Services/Requirements','fa-list-check','Requirements'],['/Services/OfficeCalendar','fa-calendar-days','Office calendar'],['/Services/Report','fa-chart-simple','Service reports']],
            'Academic setup'=>[['modules/admin/manage.php?entity=courses','fa-layer-group','Courses'],['modules/admin/manage.php?entity=subjects','fa-book','Subjects'],['modules/admin/manage.php?entity=sections','fa-diagram-project','Sections'],['modules/admin/manage.php?entity=schedules','fa-clock','Class schedules'],['modules/admin/manage.php?entity=school_years','fa-school','School years']],
            'Classroom tools'=>[['modules/teacher/attendance.php','fa-calendar-check','Attendance desk'],['modules/teacher/grades.php','fa-pen','Gradebook'],['modules/shared/qr_attendance.php','fa-qrcode','ID check-in'],['modules/shared/predictive.php','fa-chart-line','Academic insights'],['modules/shared/reports.php','fa-file-export','Reports']],
            'School administration'=>[['modules/admin/knowledge.php','fa-comments','Assistant knowledge'],['modules/admin/manage.php?entity=announcements','fa-bullhorn','Announcements'],['modules/admin/manage.php?entity=events','fa-calendar-days','School events'],['modules/admin/settings.php','fa-gear','School settings'],['modules/admin/logs.php','fa-clock-rotate-left','Activity log']]
        ];
        if($role==='Super Admin') $groups=['Account governance'=>[['modules/admin/manage.php?entity=users','fa-user-shield','Accounts & approvals'],['modules/admin/backup.php','fa-database','Backup workspace'],['modules/admin/restore.php','fa-upload','Restore workspace']]]+$groups;
    } elseif($role==='Teacher') $groups=['My classroom'=>[['modules/teacher/students.php','fa-user-graduate','My students'],['modules/teacher/attendance.php','fa-calendar-check','Attendance'],['modules/teacher/grades.php','fa-pen','Gradebook'],['modules/shared/qr_attendance.php','fa-qrcode','ID check-in'],['modules/teacher/announcements.php','fa-bullhorn','Announcements'],['modules/shared/reports.php','fa-file-export','Reports']]];
    elseif($role==='Student') $groups=['My academics'=>[['modules/student/grades.php','fa-ranking-star','My grades'],['modules/student/attendance.php','fa-calendar-check','My attendance'],['modules/student/schedule.php','fa-clock','My schedule'],['modules/student/subjects.php','fa-book-open','My subjects'],['modules/student/reports.php','fa-file-export','My reports']]];
    elseif($role==='Parent') $groups=['Student progress'=>[['modules/parent/monitor.php','fa-child','Linked student'],['modules/shared/reports.php','fa-file-export','Reports']]];
?>
<aside class="sidebar" id="sidebar"><a class="brand" href="/portal"><img src="/sims/brand-logo.php" alt="ICAS Philippines"><div><strong><?= e(setting('school_name','ICAS Philippines')) ?></strong><span>CAMPUS PORTAL</span></div></a><div class="side-section">My workspace</div><a class="nav-link <?= $uri==='/portal'?'active':'' ?>" href="/portal"><i class="fa-solid fa-house"></i><span>Overview</span></a>
<?php $link(['dashboard.php','fa-chart-simple','Academic dashboard']); ?>
<a class="nav-link" href="<?= in_array($role,['Admin','Super Admin'],true)?'/Admin/Dashboard':'/Queue/MyTicket' ?>"><i class="fa-solid fa-ticket"></i><span>Registrar & Cashier</span></a><button class="nav-link sidebar-assistant" type="button" data-assistant-action="open"><i class="fa-regular fa-comment-dots"></i><span>ICAS Assistant</span></button>
<?php foreach($groups as $title=>$items): $open=!in_array($role,['Admin','Super Admin'],true)||array_filter($items,fn($item)=>$active($item[0])); ?><details class="nav-group" <?= $open?'open':'' ?>><summary><?= e($title) ?><i class="fa-solid fa-chevron-down"></i></summary><div><?php foreach($items as $item)$link($item); ?></div></details><?php endforeach; ?>
<a class="nav-link" href="/Services/Requests"><i class="fa-solid fa-file-lines"></i><span>My service requests</span></a><div class="side-section">Stay connected</div><?php $link(['modules/shared/calendar.php','fa-calendar','Calendar']);$link(['modules/shared/notifications.php','fa-bell','Notifications']);$link(['modules/student/profile.php','fa-user','My profile']); ?>
<div class="sidebar-school-note"><span class="school-note-mark"></span><div>ICAS Philippines<small>Learning, connected.</small></div></div></aside>
<?php }

function render_footer(): void { ?>
    </div>
</main>
</div>
<script src="/sims/assets/js/bootstrap.bundle.min.js"></script>
<script src="/sims/assets/js/chart.umd.min.js"></script>
<script src="<?= asset('js/app.js') ?>"></script>
<link rel="stylesheet" href="/sims/assets/css/assistant.css"><script src="/sims/assets/js/assistant.js" defer></script>
</body>
</html>
<?php }

function card_stat(string $title, string $value, string $icon, string $class='primary', string $sub=''): void { ?>
<div class="col-sm-6 col-xl-3"><div class="stat-card"><div class="stat-icon bg-<?= e($class) ?>"><i class="fa-solid <?= e($icon) ?>"></i></div><div><span><?= e($title) ?></span><h3><?= e($value) ?></h3><small><?= e($sub) ?></small></div></div></div>
<?php }
?>

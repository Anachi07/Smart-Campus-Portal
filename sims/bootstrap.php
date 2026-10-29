<?php
require_once __DIR__ . '/config/config.php';

if (session_status() === PHP_SESSION_NONE) {
    session_set_cookie_params([
        'lifetime' => 0,
        'path' => '/',
        'httponly' => true,
        'samesite' => 'Lax'
    ]);
    session_start();
}

ini_set('display_errors', '1');
error_reporting(E_ALL);

function db(): PDO {
    static $pdo = null;
    if ($pdo === null) {
        if (!is_dir(dirname(DB_PATH))) {
            mkdir(dirname(DB_PATH), 0775, true);
        }
        $pdo = new PDO('sqlite:' . DB_PATH);
        $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
        $pdo->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
        $pdo->exec('PRAGMA foreign_keys = ON');
        init_database($pdo);
    }
    return $pdo;
}

function init_database(PDO $pdo): void {
    $schema = [
        "CREATE TABLE IF NOT EXISTS roles (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT UNIQUE NOT NULL)",
        "CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT UNIQUE NOT NULL, email TEXT UNIQUE, password_hash TEXT NOT NULL, role_id INTEGER NOT NULL, full_name TEXT NOT NULL, status TEXT DEFAULT 'Active', profile_pic TEXT DEFAULT 'default-avatar.svg', last_login TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(role_id) REFERENCES roles(id))",
        "CREATE TABLE IF NOT EXISTS courses (id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT UNIQUE NOT NULL, name TEXT NOT NULL, description TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP)",
        "CREATE TABLE IF NOT EXISTS school_years (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT UNIQUE NOT NULL, starts_at TEXT, ends_at TEXT, is_active INTEGER DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS sections (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, course_id INTEGER, year_level TEXT, adviser_teacher_id INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(course_id) REFERENCES courses(id))",
        "CREATE TABLE IF NOT EXISTS subjects (id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT UNIQUE NOT NULL, title TEXT NOT NULL, course_id INTEGER, year_level TEXT, units INTEGER DEFAULT 3, teacher_id INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(course_id) REFERENCES courses(id))",
        "CREATE TABLE IF NOT EXISTS students (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id INTEGER UNIQUE, student_no TEXT UNIQUE NOT NULL, course_id INTEGER, section_id INTEGER, year_level TEXT, birthdate TEXT, gender TEXT, address TEXT, phone TEXT, gpa REAL DEFAULT 1.75, attendance_rate REAL DEFAULT 92.4, risk_level TEXT DEFAULT 'Low', created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(user_id) REFERENCES users(id), FOREIGN KEY(course_id) REFERENCES courses(id), FOREIGN KEY(section_id) REFERENCES sections(id))",
        "CREATE TABLE IF NOT EXISTS teachers (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id INTEGER UNIQUE, employee_no TEXT UNIQUE NOT NULL, department TEXT, specialization TEXT, phone TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(user_id) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS parents (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id INTEGER UNIQUE, student_id INTEGER, relationship TEXT, phone TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(user_id) REFERENCES users(id), FOREIGN KEY(student_id) REFERENCES students(id))",
        "CREATE TABLE IF NOT EXISTS schedules (id INTEGER PRIMARY KEY AUTOINCREMENT, subject_id INTEGER, section_id INTEGER, teacher_id INTEGER, day_of_week TEXT, start_time TEXT, end_time TEXT, room TEXT, FOREIGN KEY(subject_id) REFERENCES subjects(id), FOREIGN KEY(section_id) REFERENCES sections(id), FOREIGN KEY(teacher_id) REFERENCES teachers(id))",
        "CREATE TABLE IF NOT EXISTS attendance (id INTEGER PRIMARY KEY AUTOINCREMENT, student_id INTEGER NOT NULL, subject_id INTEGER, date TEXT NOT NULL, status TEXT NOT NULL, method TEXT DEFAULT 'Manual', confidence REAL DEFAULT 99.0, marked_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(student_id) REFERENCES students(id), FOREIGN KEY(subject_id) REFERENCES subjects(id), FOREIGN KEY(marked_by) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS grades (id INTEGER PRIMARY KEY AUTOINCREMENT, student_id INTEGER NOT NULL, subject_id INTEGER NOT NULL, prelim REAL DEFAULT 0, midterm REAL DEFAULT 0, final REAL DEFAULT 0, gpa REAL DEFAULT 0, remarks TEXT, encoded_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, UNIQUE(student_id, subject_id), FOREIGN KEY(student_id) REFERENCES students(id), FOREIGN KEY(subject_id) REFERENCES subjects(id), FOREIGN KEY(encoded_by) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS announcements (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, body TEXT NOT NULL, target_role TEXT DEFAULT 'All', created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(created_by) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS reports (id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, type TEXT NOT NULL, content TEXT, created_by INTEGER, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(created_by) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS predictions (id INTEGER PRIMARY KEY AUTOINCREMENT, student_id INTEGER NOT NULL, attendance_rate REAL, gpa REAL, risk_score REAL, grade_prediction REAL, dropout_probability REAL, risk_level TEXT, generated_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(student_id) REFERENCES students(id))",
        "CREATE TABLE IF NOT EXISTS notifications (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id INTEGER, title TEXT NOT NULL, body TEXT NOT NULL, is_read INTEGER DEFAULT 0, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(user_id) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS activity_logs (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id INTEGER, action TEXT NOT NULL, details TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(user_id) REFERENCES users(id))",
        "CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT)"
    ];
    foreach ($schema as $sql) { $pdo->exec($sql); }
    seed_database($pdo);
}

function seed_database(PDO $pdo): void {
    $roles = ['Admin','Teacher','Student','Parent'];
    $st = $pdo->prepare('INSERT OR IGNORE INTO roles(name) VALUES(?)');
    foreach ($roles as $role) { $st->execute([$role]); }

    $roleId = function($name) use ($pdo) {
        $s = $pdo->prepare('SELECT id FROM roles WHERE name=?');
        $s->execute([$name]);
        return (int)$s->fetchColumn();
    };
    $count = (int)$pdo->query('SELECT COUNT(*) FROM users')->fetchColumn();
    if ($count > 0) { return; }

    $addUser = function($username, $password, $role, $full, $email) use ($pdo, $roleId) {
        $stmt = $pdo->prepare('INSERT INTO users(username,email,password_hash,role_id,full_name) VALUES(?,?,?,?,?)');
        $stmt->execute([$username, $email, password_hash($password, PASSWORD_DEFAULT), $roleId($role), $full]);
        return (int)$pdo->lastInsertId();
    };

    $adminId = $addUser('ADMIN', 'ADMIN', 'Admin', 'Admin User', 'admin@smartsims.local');
    $teacher1 = $addUser('TCH001', 'teacher123', 'Teacher', 'Prof. Maria Santos', 'maria.santos@smartsims.local');
    $teacher2 = $addUser('TCH002', 'teacher123', 'Teacher', 'Prof. Robert Cruz', 'robert.cruz@smartsims.local');
    $student1 = $addUser('2024-0001', 'student123', 'Student', 'Juan Dela Cruz', 'juan.delacruz@smartsims.local');
    $student2 = $addUser('2024-0002', 'student123', 'Student', 'Maria Santos', 'student.maria@smartsims.local');
    $student3 = $addUser('2024-0003', 'student123', 'Student', 'Pedro Reyes', 'pedro.reyes@smartsims.local');
    $student4 = $addUser('2024-0004', 'student123', 'Student', 'Anna Lopez', 'anna.lopez@smartsims.local');
    $student5 = $addUser('2024-0005', 'student123', 'Student', 'Mark Garcia', 'mark.garcia@smartsims.local');
    $parent1 = $addUser('PARENT001', 'parent123', 'Parent', 'Mr. Antonio Dela Cruz', 'parent.delacruz@smartsims.local');

    $pdo->exec("INSERT INTO courses(code,name,description) VALUES
        ('BSIT','Bachelor of Science in Information Technology','Technology and software development program'),
        ('BSCS','Bachelor of Science in Computer Science','Computing and algorithms program'),
        ('BSA','Bachelor of Science in Accountancy','Accounting and business program'),
        ('BSCE','Bachelor of Science in Civil Engineering','Engineering program')");
    $pdo->exec("INSERT INTO school_years(name,starts_at,ends_at,is_active) VALUES('2024-2025','2024-08-01','2025-05-31',1)");
    $pdo->exec("INSERT INTO teachers(user_id,employee_no,department,specialization,phone) VALUES
        ($teacher1,'T-1001','College of Computing','Machine Learning','0917 111 2222'),
        ($teacher2,'T-1002','College of Computing','Database Systems','0917 333 4444')");
    $pdo->exec("INSERT INTO sections(name,course_id,year_level,adviser_teacher_id) VALUES
        ('3A',1,'3rd Year',1),('2B',2,'2nd Year',2),('4A',3,'4th Year',1)");
    $pdo->exec("INSERT INTO subjects(code,title,course_id,year_level,units,teacher_id) VALUES
        ('IT301','Machine Learning',1,'3rd Year',3,1),
        ('IT302','Database Systems',1,'3rd Year',3,2),
        ('IT303','Data Structures',1,'2nd Year',3,2),
        ('CS201','Algorithms',2,'2nd Year',3,1),
        ('GE101','Purposive Communication',1,'1st Year',3,1)");
    $pdo->exec("INSERT INTO students(user_id,student_no,course_id,section_id,year_level,birthdate,gender,address,phone,gpa,attendance_rate,risk_level) VALUES
        ($student1,'2024-0001',1,1,'3rd Year','2004-03-15','Male','123 Rizal St., Manila','0912 967 6543',2.25,68.0,'High'),
        ($student2,'2024-0002',2,2,'2nd Year','2005-01-11','Female','Quezon City','0912 111 0002',1.50,96.0,'Low'),
        ($student3,'2024-0003',1,1,'3rd Year','2004-09-20','Male','Pasig City','0912 111 0003',2.95,73.0,'High'),
        ($student4,'2024-0004',3,3,'4th Year','2003-11-25','Female','Makati City','0912 111 0004',1.80,88.0,'Medium'),
        ($student5,'2024-0005',4,1,'2nd Year','2005-07-06','Male','Taguig City','0912 111 0005',2.10,91.0,'Low')");
    $pdo->exec("INSERT INTO parents(user_id,student_id,relationship,phone) VALUES($parent1,1,'Father','0917 999 8888')");
    $pdo->exec("INSERT INTO schedules(subject_id,section_id,teacher_id,day_of_week,start_time,end_time,room) VALUES
        (1,1,1,'Monday','08:00','10:00','Lab 1'),(2,1,2,'Tuesday','10:00','12:00','Room 204'),(3,2,2,'Wednesday','13:00','15:00','Room 301'),(4,2,1,'Thursday','09:00','11:00','Room 110')");

    $today = date('Y-m-d');
    $dates = [];
    for ($i=6; $i>=0; $i--) { $dates[] = date('Y-m-d', strtotime("-$i days")); }
    foreach ([1,2,3,4,5] as $sid) {
        foreach ($dates as $idx => $d) {
            $status = ($sid === 1 && $idx % 3 === 0) ? 'Absent' : (($sid === 3 && $idx % 4 === 0) ? 'Late' : 'Present');
            $method = $idx % 2 === 0 ? 'AI Facial Recognition' : 'QR Code';
            $conf = 92 + ($sid * 1.1) + $idx;
            $stmt = $pdo->prepare('INSERT INTO attendance(student_id,subject_id,date,status,method,confidence,marked_by) VALUES(?,?,?,?,?,?,?)');
            $stmt->execute([$sid, 1, $d, $status, $method, min($conf,99.8), $adminId]);
        }
    }
    foreach ([1,2,3,4,5] as $sid) {
        foreach ([1,2] as $sub) {
            $pre = 78 + ($sid*2) + $sub;
            $mid = 80 + ($sid*1.5) + $sub;
            $fin = 82 + ($sid*1.2) + $sub;
            if ($sid === 1) { $pre=70; $mid=73; $fin=75; }
            if ($sid === 3) { $pre=74; $mid=76; $fin=77; }
            $gpa = round((100 - (($pre+$mid+$fin)/3)) / 20 + 1, 2);
            $remarks = (($pre+$mid+$fin)/3) >= 75 ? 'Passed' : 'Failed';
            $stmt = $pdo->prepare('INSERT INTO grades(student_id,subject_id,prelim,midterm,final,gpa,remarks,encoded_by) VALUES(?,?,?,?,?,?,?,?)');
            $stmt->execute([$sid,$sub,$pre,$mid,$fin,$gpa,$remarks,$teacher1]);
        }
    }
    $pdo->exec("INSERT INTO announcements(title,body,target_role,created_by) VALUES
        ('Midterm Examination Schedule','Please be informed that the midterm examination will be on May 27-31.','All',$adminId),
        ('Library Maintenance','The library will be closed on May 25 for system maintenance.','Student',$adminId),
        ('Grade Encoding Deadline','Teachers must encode grades before Friday 5 PM.','Teacher',$adminId),
        ('Parent Consultation Week','Parents may check attendance and academic progress in the parent portal.','Parent',$adminId)");
    $pdo->exec("INSERT INTO reports(title,type,content,created_by) VALUES
        ('Weekly Attendance Report','Attendance','Generated attendance summary for current week.',$adminId),
        ('Risk Analytics Report','Risk','Generated risk detection and predictive analytics.',$adminId)");
    $pdo->exec("INSERT INTO notifications(user_id,title,body) VALUES
        ($student1,'Attendance Alert','Your attendance rate is below the recommended threshold.'),
        ($parent1,'Student Risk Alert','Juan Dela Cruz has been flagged as high risk.'),
        ($teacher1,'Grade Reminder','Please encode missing grades for your handled subjects.')");
    foreach ([1,2,3,4,5] as $sid) { generate_prediction($sid, false); }
    log_activity($adminId, 'System Seeded', 'Initial SmartSIMS sample data was created.');
}

function e($value): string { return htmlspecialchars((string)$value, ENT_QUOTES, 'UTF-8'); }
function url(string $path=''): string { return $path; }

function rel_root(): string {
    $scriptFile = $_SERVER['SCRIPT_FILENAME'] ?? BASE_PATH . '/index.php';
    $scriptDir = realpath(dirname($scriptFile)) ?: BASE_PATH;
    $base = str_replace('\\', '/', realpath(BASE_PATH) ?: BASE_PATH);
    $dir = str_replace('\\', '/', $scriptDir);
    $rel = trim(str_replace($base, '', $dir), '/');
    $depth = $rel === '' ? 0 : substr_count($rel, '/') + 1;
    return str_repeat('../', $depth);
}
function root_href(string $path=''): string { return '/sims/' . ltrim($path, '/'); }
function redirect(string $path): void { header('Location: ' . $path); exit; }
function redirect_root(string $path): void { header('Location: ' . root_href($path)); exit; }
function asset(string $path): string { return root_href('assets/' . ltrim($path, '/')); }

function role_id(string $name): int { $stmt = db()->prepare('SELECT id FROM roles WHERE name=?'); $stmt->execute([$name]); return (int)$stmt->fetchColumn(); }
function role_name(?int $id=null): string { $id = $id ?? ($_SESSION['role_id'] ?? 0); $stmt = db()->prepare('SELECT name FROM roles WHERE id=?'); $stmt->execute([$id]); return (string)$stmt->fetchColumn(); }
function current_user(): ?array { if (empty($_SESSION['user_id'])) return null; $stmt = db()->prepare('SELECT u.*, r.name AS role FROM users u JOIN roles r ON r.id=u.role_id WHERE u.id=?'); $stmt->execute([$_SESSION['user_id']]); return $stmt->fetch() ?: null; }
function require_login(): void {
    if (empty($_SESSION['user_id'])) { redirect_root('login.php'); }
    $activeUser = current_user();
    if (!$activeUser || $activeUser['status'] !== 'Active') { session_destroy(); redirect_root('login.php'); }
    if (isset($_SESSION['last_activity']) && (time() - $_SESSION['last_activity']) > SESSION_TIMEOUT) { session_destroy(); redirect_root('login.php?timeout=1'); }
    $_SESSION['last_activity'] = time();
}
function require_role(array $roles): void { require_login(); if (!in_array(role_name(), $roles, true)) { http_response_code(403); exit('403 Forbidden'); } }
function csrf_token(): string { if (empty($_SESSION['csrf'])) { $_SESSION['csrf'] = bin2hex(random_bytes(32)); } return $_SESSION['csrf']; }
function csrf_field(): string { return '<input type="hidden" name="csrf" value="' . e(csrf_token()) . '">'; }
function check_csrf(): void { if ($_SERVER['REQUEST_METHOD'] === 'POST' && (!isset($_POST['csrf']) || !hash_equals($_SESSION['csrf'] ?? '', $_POST['csrf']))) { http_response_code(419); exit('Invalid CSRF token.'); } }
function set_flash(string $type, string $message): void { $_SESSION['flash'] = ['type'=>$type,'message'=>$message]; }
function flash(): string { if (empty($_SESSION['flash'])) return ''; $f = $_SESSION['flash']; unset($_SESSION['flash']); return '<div class="alert alert-'.e($f['type']).' alert-dismissible fade show">'.e($f['message']).'<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>'; }
function log_activity(?int $userId, string $action, string $details=''): void { $stmt = db()->prepare('INSERT INTO activity_logs(user_id,action,details) VALUES(?,?,?)'); $stmt->execute([$userId, $action, $details]); }
function table_count(string $table): int { return (int)db()->query('SELECT COUNT(*) FROM ' . preg_replace('/[^a-z_]/','',$table))->fetchColumn(); }
function rows(string $sql, array $params=[]): array { $st=db()->prepare($sql); $st->execute($params); return $st->fetchAll(); }
function row(string $sql, array $params=[]): ?array { $st=db()->prepare($sql); $st->execute($params); $r=$st->fetch(); return $r ?: null; }
function scalar(string $sql, array $params=[]): mixed { $st=db()->prepare($sql); $st->execute($params); return $st->fetchColumn(); }

function risk_from(float $attendanceRate, float $gpa): array {
    $riskScore = min(100, max(0, round(((100 - $attendanceRate) * 0.75) + ($gpa * 18), 2)));
    $dropout = min(95, max(2, round($riskScore * 0.62, 2)));
    $gradePrediction = max(1.00, min(5.00, round($gpa + ((75 - min($attendanceRate,75)) / 80), 2)));
    if ($attendanceRate < 75 && $gpa < 2.5) { $level = 'High'; }
    elseif ($attendanceRate < 85 || $gpa < 2.75) { $level = 'Medium'; }
    else { $level = 'Low'; }
    return [$riskScore, $gradePrediction, $dropout, $level];
}

function generate_prediction(int $studentId, bool $log=true): void {
    $student = row('SELECT * FROM students WHERE id=?', [$studentId]);
    if (!$student) return;
    $total = (int)scalar('SELECT COUNT(*) FROM attendance WHERE student_id=?', [$studentId]);
    $present = (int)scalar("SELECT COUNT(*) FROM attendance WHERE student_id=? AND status='Present'", [$studentId]);
    $rate = $total > 0 ? round(($present/$total)*100, 2) : (float)$student['attendance_rate'];
    $gpa = (float)(scalar('SELECT AVG(gpa) FROM grades WHERE student_id=?', [$studentId]) ?: $student['gpa']);
    [$score,$gradePrediction,$dropout,$level] = risk_from($rate, $gpa);
    $stmt = db()->prepare('INSERT INTO predictions(student_id,attendance_rate,gpa,risk_score,grade_prediction,dropout_probability,risk_level) VALUES(?,?,?,?,?,?,?)');
    $stmt->execute([$studentId,$rate,$gpa,$score,$gradePrediction,$dropout,$level]);
    db()->prepare('UPDATE students SET attendance_rate=?, gpa=?, risk_level=? WHERE id=?')->execute([$rate,$gpa,$level,$studentId]);
    if ($log) log_activity($_SESSION['user_id'] ?? null, 'Prediction Generated', 'Prediction generated for student ID ' . $studentId);
}

function grade_to_gpa(float $grade): float {
    if ($grade >= 97) return 1.00; if ($grade >= 94) return 1.25; if ($grade >= 91) return 1.50;
    if ($grade >= 88) return 1.75; if ($grade >= 85) return 2.00; if ($grade >= 82) return 2.25;
    if ($grade >= 79) return 2.50; if ($grade >= 75) return 3.00; return 5.00;
}

function option_list(string $table, string $label='name'): array {
    $safeTable = preg_replace('/[^a-z_]/','',$table);
    $safeLabel = preg_replace('/[^a-z_]/','',$label);
    return rows("SELECT id, $safeLabel AS label FROM $safeTable ORDER BY $safeLabel");
}

db();
?>

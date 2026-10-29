<?php
function campus_mode(): string { return getenv('CAMPUS_MODE') === 'production' ? 'production' : 'demo'; }
function teacher_id(): int { return (int)(scalar('SELECT id FROM teachers WHERE user_id=?', [$_SESSION['user_id'] ?? 0]) ?: 0); }
function student_scope_sql(string $alias='s'): string {
    $user = current_user(); if (!$user) return '0=1';
    $uid = (int)$user['id'];
    if ($user['role'] === 'Admin') return '1=1';
    if ($user['role'] === 'Teacher') {
        $tid = teacher_id();
        return "($alias.section_id IN (SELECT section_id FROM schedules WHERE teacher_id=$tid) OR EXISTS (SELECT 1 FROM subjects own WHERE own.teacher_id=$tid AND own.course_id=$alias.course_id AND own.year_level=$alias.year_level))";
    }
    if ($user['role'] === 'Student') return "$alias.user_id=$uid";
    if ($user['role'] === 'Parent') return "$alias.id IN (SELECT student_id FROM parents WHERE user_id=$uid)";
    return '0=1';
}
function subject_scope_sql(string $alias='sub'): string {
    if ((current_user()['role'] ?? '') !== 'Teacher') return '1=1';
    $tid=teacher_id(); return "($alias.teacher_id=$tid OR $alias.id IN (SELECT subject_id FROM schedules WHERE teacher_id=$tid))";
}
function visible_students(): array { return rows('SELECT s.*,u.full_name,(SELECT COUNT(*) FROM attendance a WHERE a.student_id=s.id) AS attendance_count,(SELECT COUNT(*) FROM grades g WHERE g.student_id=s.id) AS grade_count FROM students s JOIN users u ON u.id=s.user_id WHERE '.student_scope_sql().' ORDER BY u.full_name'); }
function visible_subjects(): array { return rows('SELECT sub.* FROM subjects sub WHERE '.subject_scope_sql().' ORDER BY sub.code'); }
function has_class_access(int $sid, int $subject): bool {
    $student=row('SELECT s.* FROM students s WHERE s.id=? AND '.student_scope_sql(),[$sid]);
    $sub=row('SELECT sub.* FROM subjects sub WHERE sub.id=? AND '.subject_scope_sql(),[$subject]);
    if (!$student || !$sub) return false;
    if (current_user()['role'] === 'Teacher') {
        $assigned=(int)scalar('SELECT COUNT(*) FROM schedules WHERE subject_id=? AND section_id=? AND teacher_id=?',[$subject,$student['section_id'],teacher_id()]);
        if (!$assigned && !($sub['course_id']===$student['course_id'] && $sub['year_level']===$student['year_level'])) { return false; }
    }
    return true;
}
function require_class_access(int $sid,int $subject): void { if(!has_class_access($sid,$subject)) { http_response_code(403); exit('You do not have access to this class.'); } }
function record_attendance(int $sid, int $subject, string $date, string $status, string $method='Manual'): void {
    require_class_access($sid,$subject);
    if (!in_array($status,['Present','Late','Absent','Excused'],true) || !preg_match('/^\d{4}-\d{2}-\d{2}$/',$date) || date('Y-m-d',strtotime($date))!==$date || $date>date('Y-m-d')) throw new InvalidArgumentException('Choose a valid attendance status and a date up to today.');
    db()->beginTransaction();
    try {
        db()->prepare('DELETE FROM attendance WHERE student_id=? AND subject_id=? AND date=?')->execute([$sid,$subject,$date]);
        db()->prepare('INSERT INTO attendance(student_id,subject_id,date,status,method,confidence,marked_by) VALUES(?,?,?,?,?,?,?)')->execute([$sid,$subject,$date,$status,$method,0,$_SESSION['user_id']]);
        generate_prediction($sid,false); db()->commit();
    } catch (Throwable $e) { if(db()->inTransaction()) db()->rollBack(); throw $e; }
}
function destroy_campus_session(): void {
    $_SESSION=[];
    setcookie(session_name(),'', ['expires'=>time()-3600,'path'=>'/','httponly'=>true,'secure'=>getenv('CAMPUS_PUBLIC_HTTPS')==='1','samesite'=>'Lax']);
    session_destroy();
}
function setting(string $key,string $default=''): string { $value=scalar('SELECT value FROM settings WHERE key=?',[$key]); return $value===false?$default:(string)$value; }
function empty_state(string $title='Nothing here yet', string $message='New records will appear here once they are added.'): void { echo '<div class="empty-state"><i class="fa-regular fa-folder-open"></i><h3>'.e($title).'</h3><p>'.e($message).'</p></div>'; }

<?php
// Internal only: authoritative active status and class times, without names or contact details.
$secret=getenv('CAMPUS_INTERNAL_SECRET');
if(!$secret || !hash_equals($secret,$_SERVER['HTTP_X_CAMPUS_SECRET']??'') || $_SERVER['REQUEST_METHOD']!=='POST') { http_response_code(404); exit; }
require_once __DIR__.'/bootstrap.php';
header('Content-Type: application/json');header('Cache-Control: no-store');
$ids=json_decode(file_get_contents('php://input'),true);
if(!is_array($ids)||count($ids)>20) { http_response_code(400);echo '[]';exit; }
$result=[];
foreach(array_unique(array_map('intval',$ids)) as $id) {
    $user=row('SELECT u.status,r.name AS role FROM users u JOIN roles r ON r.id=u.role_id WHERE u.id=?',[$id]);
    $classes=[];
    if(($user['role']??'')==='Student') $classes=rows('SELECT sc.day_of_week AS day,sc.start_time AS start,sc.end_time AS end FROM schedules sc JOIN students s ON s.section_id=sc.section_id WHERE s.user_id=?',[$id]);
    if(($user['role']??'')==='Teacher') $classes=rows('SELECT sc.day_of_week AS day,sc.start_time AS start,sc.end_time AS end FROM schedules sc JOIN teachers t ON t.id=sc.teacher_id WHERE t.user_id=?',[$id]);
    $result[]=['campusId'=>$id,'active'=>($user['status']??'')==='Active','classes'=>$classes];
}
echo json_encode($result,JSON_THROW_ON_ERROR);

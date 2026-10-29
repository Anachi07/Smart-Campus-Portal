<?php
function validate_campus_database(string $path): void {
    $probe=new PDO('sqlite:'.$path); $probe->setAttribute(PDO::ATTR_ERRMODE,PDO::ERRMODE_EXCEPTION);
    if($probe->query('PRAGMA integrity_check')->fetchColumn()!=='ok') throw new InvalidArgumentException('The academic database failed its integrity check.');
    foreach(['users'=>'id,username,password_hash,role_id,status,full_name','roles'=>'id,name','students'=>'id,user_id,student_no','teachers'=>'id,user_id','parents'=>'id,user_id,student_id','courses'=>'id,code','subjects'=>'id,code','sections'=>'id,name','school_years'=>'id,name','schedules'=>'id,subject_id,section_id','attendance'=>'id,student_id,subject_id,date,status','grades'=>'id,student_id,subject_id','announcements'=>'id,title','notifications'=>'id,user_id','predictions'=>'id,student_id','activity_logs'=>'id','settings'=>'key,value'] as $table=>$columns) $probe->query("SELECT $columns FROM $table LIMIT 0");
    if(!(int)$probe->query('SELECT COUNT(*) FROM users u JOIN roles r ON r.id=u.role_id WHERE r.name="Admin" AND u.status="Active"')->fetchColumn()) throw new InvalidArgumentException('A backup must contain an active administrator.');
}
function queue_backup_request(?string $json=null): bool {
    $secret=getenv('CAMPUS_INTERNAL_SECRET'); if(!$secret) return false;
    $context=stream_context_create(['http'=>['method'=>'POST','header'=>"X-Campus-Secret: $secret\r\nContent-Type: application/json\r\n",'content'=>$json??'','timeout'=>10,'ignore_errors'=>true]]);
    $result=@file_get_contents('http://127.0.0.1:8081/internal/queue-reload'.($json!==null?'?validate=1':''),false,$context);
    return isset($http_response_header[0]) && str_contains($http_response_header[0],' 204 ');
}

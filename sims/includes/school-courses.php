<?php
const SCHOOL_COURSE_CODES=['HRS','IT','BSHM','BSIT'];
function init_school_courses(PDO $pdo): void {
    $columns=array_column($pdo->query('PRAGMA table_info(courses)')->fetchAll(),'name');
    if(!in_array('is_offered',$columns,true))$pdo->exec('ALTER TABLE courses ADD COLUMN is_offered INTEGER NOT NULL DEFAULT 0');
    $version=$pdo->query("SELECT value FROM settings WHERE key='school_course_catalog_version'")->fetchColumn();
    if($version==='hrs-it-bshm-bsit-1')return;
    $pdo->beginTransaction();try{
        foreach(SCHOOL_COURSE_CODES as $code)$pdo->prepare('INSERT OR IGNORE INTO courses(code,name,description,is_offered) VALUES(?,?,?,1)')->execute([$code,$code,'School-offered program']);
        $pdo->exec("UPDATE courses SET is_offered=CASE WHEN code IN('HRS','IT','BSHM','BSIT') THEN 1 ELSE 0 END");
        $pdo->exec("INSERT INTO settings(key,value) VALUES('school_course_catalog_version','hrs-it-bshm-bsit-1') ON CONFLICT(key) DO UPDATE SET value=excluded.value");
        $pdo->commit();
    }catch(Throwable $e){if($pdo->inTransaction())$pdo->rollBack();throw$e;}
}

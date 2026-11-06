<?php
require_once __DIR__.'/../sims/includes/public-school.php';
$pdo=new PDO('sqlite::memory:');$pdo->setAttribute(PDO::ATTR_ERRMODE,PDO::ERRMODE_EXCEPTION);
$pdo->exec("CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT);INSERT INTO settings VALUES('school_address',''),('school_phone','Existing school number'),('school_facebook','https://example.test/school')");
init_public_school_contacts($pdo);
$get=function($key)use($pdo){$q=$pdo->prepare('SELECT value FROM settings WHERE key=?');$q->execute([$key]);return $q->fetchColumn();};
if(!str_contains($get('school_address'),'8347 CAP Building')||$get('school_mobile')!=='0995-922-2204'||$get('school_phone')!=='Existing school number'||$get('school_facebook')!=='https://example.test/school')throw new RuntimeException('Contact backfill must preserve existing nonblank settings.');
$pdo->exec("UPDATE settings SET value='' WHERE key='school_mobile'");init_public_school_contacts($pdo);
if($get('school_mobile')!=='')throw new RuntimeException('Later admin clears must remain cleared.');
echo "PASS contact profile fills missing/blank values once, preserves custom contacts and respects later admin clearing\n";

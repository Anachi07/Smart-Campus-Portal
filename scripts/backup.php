<?php
if(PHP_SAPI!=='cli'){http_response_code(404);exit;}
require_once __DIR__.'/../sims/bootstrap.php';require_once __DIR__.'/../sims/includes/backup.php';
$dir=campus_backup_dir();if(!is_dir($dir)&&!mkdir($dir,0700,true))throw new RuntimeException('Unable to create backup directory.');
$real=realpath($dir);foreach([__DIR__.'/../sims',__DIR__.'/../queue/wwwroot',__DIR__.'/../public'] as $public){$root=realpath($public);if($root&&($real===$root||str_starts_with($real,$root.DIRECTORY_SEPARATOR)))throw new RuntimeException('Backups must remain outside public roots.');}
$lock=fopen($dir.'/.backup.lock','c');if(!flock($lock,LOCK_EX|LOCK_NB))exit(0);
$failed=false;
try{
    $archive=json_encode(complete_workspace_backup(),JSON_THROW_ON_ERROR);$name=$dir.'/campus-auto-'.gmdate('Ymd-His').'-'.bin2hex(random_bytes(4)).'.json';$tmp=$name.'.tmp';
    $handle=fopen($tmp,'x');chmod($tmp,0600);try{if(fwrite($handle,$archive)!==strlen($archive))throw new RuntimeException('Incomplete backup write.');fflush($handle);if(function_exists('fsync'))fsync($handle);}finally{fclose($handle);}
    if(!rename($tmp,$name))throw new RuntimeException('Unable to finalize backup.');
    $files=array_values(array_filter(glob($dir.'/campus-auto-*.json'),fn($p)=>preg_match('/^campus-auto-\d{8}-\d{6}-[a-f0-9]{8}\.json$/',basename($p))&&!is_link($p)));rsort($files);$keep=max(2,min(30,(int)(getenv('CAMPUS_BACKUP_KEEP')?:7)));foreach(array_slice($files,$keep) as $old)if(!unlink($old))throw new RuntimeException("Unable to prune an old backup.");
    $status=['last_success'=>gmdate(DATE_ATOM),'bytes'=>strlen($archive),'retained'=>min(count($files),$keep),'storage'=>'Local storage; download a private copy before redeploying free hosting.'];file_put_contents($dir.'/.status.tmp',json_encode($status,JSON_THROW_ON_ERROR));chmod($dir.'/.status.tmp',0600);rename($dir.'/.status.tmp',$dir.'/status.json');echo "Complete local backup saved.\n";
}catch(Throwable $e){
    $previous=is_file($dir.'/status.json')?json_decode(file_get_contents($dir.'/status.json'),true):[];$previous=is_array($previous)?$previous:[];
    $previous['last_failure']=gmdate(DATE_ATOM);$previous['error']='The last local backup failed. Check storage availability and download a manual backup.';
    file_put_contents($dir.'/.status.tmp',json_encode($previous,JSON_THROW_ON_ERROR));chmod($dir.'/.status.tmp',0600);rename($dir.'/.status.tmp',$dir.'/status.json');fwrite(STDERR,"Complete local backup failed. Check private storage and database access.\n");$failed=true;
}finally{if(isset($tmp)&&file_exists($tmp))unlink($tmp);flock($lock,LOCK_UN);fclose($lock);}


if($failed)exit(1);

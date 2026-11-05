<?php
function campus_key_path(): string {return dirname(DB_PATH).'/.campus-security.key';}
function campus_security_key(): string {
    $path=campus_key_path();$handle=fopen($path,'c+b');if(!$handle)throw new RuntimeException('Account security key is unavailable.');
    try{flock($handle,LOCK_EX);$key=stream_get_contents($handle);if($key===''){chmod($path,0600);$key=random_bytes(32);if(fwrite($handle,$key)!==32)throw new RuntimeException('Unable to save account security key.');fflush($handle);}if(strlen($key)!==32)throw new RuntimeException('Account security key is invalid.');return$key;}finally{flock($handle,LOCK_UN);fclose($handle);}
}
function mfa_encrypt(string $secret): string {$nonce=random_bytes(SODIUM_CRYPTO_SECRETBOX_NONCEBYTES);return base64_encode($nonce.sodium_crypto_secretbox($secret,$nonce,campus_security_key()));}
function mfa_decrypt(string $encrypted): string {$bytes=base64_decode($encrypted,true);if(!$bytes||strlen($bytes)<40)throw new RuntimeException('MFA data is unavailable.');$secret=sodium_crypto_secretbox_open(substr($bytes,24),substr($bytes,0,24),campus_security_key());if($secret===false)throw new RuntimeException('MFA key does not match this workspace.');return$secret;}
function base32_encode_secret(string $bytes): string {$bits='';foreach(unpack('C*',$bytes) as $b)$bits.=str_pad(decbin($b),8,'0',STR_PAD_LEFT);$output='';foreach(str_split($bits,5) as $chunk)$output.='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'[bindec(str_pad($chunk,5,'0'))];return$output;}
function base32_decode_secret(string $secret): string {$bits='';foreach(str_split($secret) as $c){$i=strpos('ABCDEFGHIJKLMNOPQRSTUVWXYZ234567',$c);if($i===false)throw new RuntimeException('Invalid MFA secret.');$bits.=str_pad(decbin($i),5,'0',STR_PAD_LEFT);}$output='';foreach(str_split($bits,8) as $chunk)if(strlen($chunk)===8)$output.=chr(bindec($chunk));return$output;}
function totp_code(string $secret,int $step): string {$hash=hash_hmac('sha1',pack('N2',0,$step),base32_decode_secret($secret),true);$offset=ord($hash[19])&15;$number=unpack('N',substr($hash,$offset,4))[1]&0x7fffffff;return str_pad((string)($number%1000000),6,'0',STR_PAD_LEFT);}
function mfa_step(string $secret,string $code,int $last=-1): ?int {if(!preg_match('/^\d{6}$/',$code))return null;$now=intdiv(time(),30);foreach([$now,$now-1,$now+1] as $step)if($step>$last&&hash_equals(totp_code($secret,$step),$code))return$step;return null;}
function mfa_verify(int $uid,string $code): bool {
    db()->beginTransaction();try{$mfa=row('SELECT * FROM account_mfa WHERE user_id=?',[$uid]);if(!$mfa){db()->rollBack();return false;}
        $step=mfa_step(mfa_decrypt($mfa['secret']),trim($code),(int)$mfa['last_step']);
        if($step!==null){$update=db()->prepare('UPDATE account_mfa SET last_step=? WHERE user_id=? AND last_step<?');$update->execute([$step,$uid,$step]);$ok=$update->rowCount()===1;}
        else {$hash=hash('sha256',strtoupper(str_replace('-','',trim($code))));$codes=json_decode($mfa['recovery_hashes'],true);$ok=false;foreach($codes as $i=>$stored)if(hash_equals($stored,$hash)){unset($codes[$i]);$ok=true;break;}if($ok)db()->prepare('UPDATE account_mfa SET recovery_hashes=? WHERE user_id=?')->execute([json_encode(array_values($codes)),$uid]);}
        db()->commit();return$ok;
    }catch(Throwable $e){if(db()->inTransaction())db()->rollBack();throw$e;}
}
function establish_campus_login(array $user): void {
    session_regenerate_id(true);$_SESSION=['user_id'=>$user['id'],'role_id'=>$user['role_id'],'auth_version'=>(int)$user['auth_version'],'last_activity'=>time(),'workspace_epoch'=>setting('session_epoch','initial')];
    db()->prepare('UPDATE users SET last_login=CURRENT_TIMESTAMP WHERE id=?')->execute([$user['id']]);log_activity($user['id'],'Login','Signed in to campus portal.');
}

<?php
// Serve the prepared school logo asset; otherwise render a text wordmark.
require_once __DIR__.'/config/config.php';
foreach(['png'=>'image/png','jpg'=>'image/jpeg','jpeg'=>'image/jpeg','webp'=>'image/webp','svg'=>'image/svg+xml'] as $extension=>$mime) {
    $file=__DIR__.'/assets/images/icas-logo.'.$extension;
    if(is_file($file)) { $etag='"'.hash_file('sha256',$file).'"'; header('ETag: '.$etag); header('Cache-Control: public, max-age=0, must-revalidate'); if(($_SERVER['HTTP_IF_NONE_MATCH']??'')===$etag) { http_response_code(304); exit; } header('Content-Type: '.$mime); header('X-Content-Type-Options: nosniff'); readfile($file); exit; }
}
header('Content-Type: image/svg+xml');
echo '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 260 112"><rect width="260" height="112" rx="8" fill="white"/><text x="130" y="73" text-anchor="middle" font-family="Arial,Helvetica,sans-serif" font-weight="900" font-size="82" letter-spacing="-5" fill="#0b4625">ICAS</text><path d="M20 84h220" stroke="#ddbc35" stroke-width="3"/><text x="130" y="104" text-anchor="middle" font-family="Arial,Helvetica,sans-serif" font-weight="700" font-size="15" letter-spacing="5" fill="#0b4625">PHILIPPINES</text></svg>';

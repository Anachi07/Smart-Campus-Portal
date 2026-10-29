<?php
$path = rawurldecode(parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH));
$root = realpath(__DIR__ . '/../sims');
$file = realpath($root . $path);
if (!$file || !str_starts_with($file, $root . DIRECTORY_SEPARATOR)) {
    if ($path === '/') { header('Location: /sims/index.php'); exit; }
    http_response_code(404); exit;
}
$relative = substr($file, strlen($root) + 1);
$publicPhp = preg_match('~^(index|login|logout|register|dashboard|portal|campus-session|brand-logo|assistant-data)\.php$~', $relative)
    || preg_match('~^modules/(admin|parent|shared|student|teacher)/[a-z_]+\.php$~', $relative);
$asset = preg_match('~^assets/(css|js|images|webfonts)/[a-zA-Z0-9_./-]+\.(css|js|svg|png|jpg|woff2?)$~', $relative);
if ($publicPhp || $asset) return false;
http_response_code(404);

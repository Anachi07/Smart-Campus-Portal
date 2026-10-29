<?php
$expected = getenv('CAMPUS_INTERNAL_SECRET');
if (!$expected || !hash_equals($expected, $_SERVER['HTTP_X_CAMPUS_SECRET'] ?? '')) {
    http_response_code(404); exit;
}
require_once __DIR__ . '/bootstrap.php';
header('Content-Type: application/json');
header('Cache-Control: no-store');
$user = current_user();
if (!$user || $user['status'] !== 'Active' || time() - ($_SESSION['last_activity'] ?? 0) > SESSION_TIMEOUT) {
    http_response_code(401); echo '{}'; exit;
}
$_SESSION['last_activity'] = time();
$phone = '';
foreach (['students', 'teachers', 'parents'] as $table) {
    $record = row("SELECT phone FROM $table WHERE user_id=?", [$user['id']]);
    if ($record && !empty($record['phone'])) { $phone = $record['phone']; break; }
}
echo json_encode(['id'=>(int)$user['id'], 'fullName'=>$user['full_name'], 'role'=>$user['role'], 'phone'=>$phone]);

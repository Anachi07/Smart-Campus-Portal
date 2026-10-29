<?php
$secret=getenv('CAMPUS_INTERNAL_SECRET');
if(!$secret || !hash_equals($secret,$_SERVER['HTTP_X_CAMPUS_SECRET']??'')) { http_response_code(404); exit; }
require_once __DIR__.'/bootstrap.php';
header('Content-Type: application/json'); header('Cache-Control: no-store');
$defaults=[
 ['id'=>-1,'question'=>'What can the ICAS campus portal do?','answer'=>'The portal provides academic records, grades, attendance, class schedules, school announcements, and Registrar/Cashier queue services. Access depends on your signed-in role.','sourceLabel'=>'Campus portal services'],
 ['id'=>-2,'question'=>'How do I request school documents? Paano kumuha ng dokumento?','answer'=>'Choose Document Request in Registrar & Cashier services, select the document type, provide the student/reference number, and confirm your request. The Registrar verifies eligibility, fees, requirements, and release. A queue ticket does not issue a document.','sourceLabel'=>'Registrar queue workflow'],
 ['id'=>-3,'question'=>'How do tuition and school fee payments work? Paano magbayad?','answer'=>'Choose Tuition Payment or School Fees to join the Cashier queue. Confirm the amount, accepted payment method, and official receipt with the school Cashier. This portal does not charge money or issue official receipts.','sourceLabel'=>'Cashier queue workflow'],
 ['id'=>-4,'question'=>'How can I change or recover my password? Account login tulong','answer'=>'Change your password in My profile after signing in. If you cannot sign in, ask the school administrator to reset access. Never share passwords in the assistant or a queue request.','sourceLabel'=>'Account help']
];
$published=rows('SELECT id,question,answer,source_label AS sourceLabel FROM school_faqs WHERE published=1 ORDER BY id LIMIT 200');
echo json_encode(['schoolName'=>setting('school_name','ICAS Philippines'),'faqs'=>array_merge($defaults,$published)],JSON_THROW_ON_ERROR);

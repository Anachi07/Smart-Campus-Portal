<?php
define('APP_NAME', 'Smart SIMS');
define('APP_LONG_NAME', 'Design and Development of a Smart Student Information Management System with AI-Based Attendance and Predictive Analytics');
define('BASE_PATH', dirname(__DIR__));
define('DB_PATH', getenv('CAMPUS_SIMS_DB') ?: BASE_PATH . '/database/SmartSIMS.db');
define('SESSION_TIMEOUT', 1800);
define('APP_VERSION', '1.1.0');
define('QUEUE_DATA_PATH', (getenv('CAMPUS_QUEUE_DATA_DIR') ?: dirname(BASE_PATH).'/queue/Data').'/smartqueue-data.json');
?>

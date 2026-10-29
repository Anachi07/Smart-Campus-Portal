<?php
define('APP_NAME', 'ICAS Philippines Campus Portal');
define('APP_LONG_NAME', 'ICAS Philippines · Student information and campus services');
define('BASE_PATH', dirname(__DIR__));
define('DB_PATH', getenv('CAMPUS_SIMS_DB') ?: BASE_PATH . '/database/SmartSIMS.db');
define('SESSION_TIMEOUT', 1800);
define('APP_VERSION', '1.1.0');
define('QUEUE_DATA_PATH', (getenv('CAMPUS_QUEUE_DATA_DIR') ?: dirname(BASE_PATH).'/queue/Data').'/smartqueue-data.json');
?>

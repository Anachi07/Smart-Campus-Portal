<?php
require_once __DIR__.'/../../bootstrap.php'; require_role(['Admin','Teacher']); redirect('/sims/modules/teacher/attendance.php');

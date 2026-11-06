<?php
// Only these explicitly public fields are rendered on the signed-out school page.
function public_school_fields(): array {
    return [
        'school_public_name'=>['Official school name (homepage)',160,'text','Infotech College of Arts and Sciences (ICAS)'],
        'school_branch'=>['Branch name (homepage)',120,'text','Sucat branch'],
        'school_name'=>['School name (portal)',120,'text','ICAS Philippines'],
        'academic_year'=>['Academic year',120,'text',date('Y').'-'.(date('Y')+1)],
        'school_about'=>['About the school',3000,'textarea',''],
        'school_mission'=>['Mission',2000,'textarea',''],
        'school_vision'=>['Vision',2000,'textarea',''],
        'school_address'=>['Campus address',500,'textarea','8347 CAP Building, Dr. A. Santos Ave. (Sucat), Brgy. San Antonio, Parañaque City (in front of Nissan Sucat)'],
        'school_phone'=>['Public contact number',100,'text','(02) 7-717-2982, 825-6531 or 473-4349'],
        'school_mobile'=>['Mobile / inquiries',100,'text','0995-922-2204'],
        'school_email'=>['Public email address',254,'email',''],
        'school_facebook'=>['Official Facebook page URL',300,'url','https://www.facebook.com/icasparanaque'],
        'school_website'=>['Official website URL',300,'url',''],
        'school_office_hours'=>['Office hours',300,'textarea',''],
        'school_hrs_name'=>['Full program name for HRS',120,'text',''],
        'school_it_name'=>['Full program name for IT',120,'text',''],
        'school_admissions_notice'=>['Public admission announcement',1500,'textarea','The school’s poster announces “Admission Open for 2026–2027” for Sucat campus. Contact the school to confirm current enrollment schedules, requirements and steps.'],
        'school_admissions_status'=>['Enrollment information status',9,'select','Draft'],
        'school_admissions_new'=>['New student requirements (one per line)',3000,'textarea',''],
        'school_admissions_transferee'=>['Transferee requirements (one per line)',3000,'textarea',''],
        'school_admissions_steps'=>['Enrollment steps (one per line, in order)',4000,'textarea',''],
        'school_admissions_notes'=>['Enrollment notes',1500,'textarea',''],
    ];
}
function public_school_url(string $url): string {
    return filter_var($url,FILTER_VALIDATE_URL)&&strtolower((string)parse_url($url,PHP_URL_SCHEME))==='https'&&!parse_url($url,PHP_URL_USER)&&!parse_url($url,PHP_URL_PASS)?$url:'';
}

function public_school_lines(string $text): array {
    return array_values(array_filter(array_map('trim',preg_split('/\r\n|\r|\n/',$text)),fn($line)=>$line!==''));
}
function public_admissions_ready(array $school): bool {
    return ($school['school_admissions_status']??'Draft')==='Published'
        && public_school_lines($school['school_admissions_steps']??'')
        && (public_school_lines($school['school_admissions_new']??'')||public_school_lines($school['school_admissions_transferee']??''));
}

// Apply the owner-supplied Sucat contact profile once, preserving existing nonblank edits.
function init_public_school_contacts(PDO $pdo): void {
    $key='sucat_contact_profile_2026_11';
    $check=$pdo->prepare('SELECT value FROM settings WHERE key=?');$check->execute([$key]);if($check->fetchColumn())return;
    $pdo->beginTransaction();
    try {
        $marker=$pdo->prepare('INSERT OR IGNORE INTO settings(key,value) VALUES(?,?)');$marker->execute([$key,'1']);
        if($marker->rowCount()){
            $fields=public_school_fields();
            foreach(['school_address','school_phone','school_mobile','school_facebook'] as $field){
                $update=$pdo->prepare("INSERT INTO settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value WHERE trim(COALESCE(settings.value,''))=''");
                $update->execute([$field,$fields[$field][3]]);
            }
        }
        $pdo->commit();
    }catch(Throwable $e){$pdo->rollBack();throw $e;}
}

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
        'school_address'=>['Campus address',500,'textarea',''],
        'school_phone'=>['Public contact number',100,'text',''],
        'school_email'=>['Public email address',254,'email',''],
        'school_facebook'=>['Official Facebook page URL',300,'url',''],
        'school_website'=>['Official website URL',300,'url',''],
        'school_office_hours'=>['Office hours',300,'textarea',''],
        'school_hrs_name'=>['Full program name for HRS',120,'text',''],
        'school_it_name'=>['Full program name for IT',120,'text',''],
    ];
}
function public_school_url(string $url): string {
    return filter_var($url,FILTER_VALIDATE_URL)&&strtolower((string)parse_url($url,PHP_URL_SCHEME))==='https'&&!parse_url($url,PHP_URL_USER)&&!parse_url($url,PHP_URL_PASS)?$url:'';
}

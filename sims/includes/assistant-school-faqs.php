<?php
require_once __DIR__.'/public-school.php';
// Export only explicit public school facts. Unpublished admission text never leaves PHP.
function assistant_school_faqs(): array {
    $school=[];foreach(public_school_fields() as $key=>$field)$school[$key]=setting($key,$field[3]);
    $identity=$school['school_public_name'].($school['school_branch']?' · '.$school['school_branch']:'');
    $contact=[];foreach(['school_phone'=>'Telephone','school_mobile'=>'Mobile / inquiries','school_email'=>'Email'] as $key=>$label)if($school[$key]!=='')$contact[]=$label.': '.$school[$key];
    foreach(['school_facebook'=>'Facebook','school_website'=>'Website'] as $key=>$label)if($url=public_school_url($school[$key]))$contact[]=$label.': '.$url;
    $contactText=$contact?implode("\n",$contact):'Makipag-ugnayan sa school office para sa enrollment inquiries.';
    $programs=['HRS'.($school['school_hrs_name']?' — '.$school['school_hrs_name']:''),'IT'.($school['school_it_name']?' — '.$school['school_it_name']:''),'BSHM — Bachelor of Science in Hospitality Management','BSIT — Bachelor of Science in Information Technology'];
    $published=public_admissions_ready($school);
    $steps=$published?public_school_lines($school['school_admissions_steps']):[];
    $stepAnswer=$steps?'Published enrollment steps:'."\n".implode("\n",array_map(fn($step,$i)=>($i+1).'. '.$step,$steps,array_keys($steps))):'Wala pang published school-approved enrollment steps. Kumpirmahin ang kasalukuyang proseso at schedule sa school.';
    $faqs=[
        ['id'=>-101,'question'=>'What courses programs are available offered at ICAS Sucat? Anong kurso inaalok HRS IT BSHM BSIT?','answer'=>"Mga programang inaalok:\n".implode("\n",$programs)."\n".'Kumpirmahin sa school ang available classes at admission requirements.','sourceLabel'=>'School-confirmed course catalog and public settings'],
        ['id'=>-102,'question'=>'Where is ICAS Sucat located? Address location directions landmark Saan nasaan ang campus?','answer'=>$school['school_address']?$identity."\n".$school['school_address']:'Wala pang published campus address. Makipag-ugnayan sa school para sa tamang lokasyon.','sourceLabel'=>'School settings · Campus address'],
        ['id'=>-103,'question'=>'How can I contact ICAS Sucat? Telephone landline mobile number Facebook email office hours Contact details Paano tumawag?','answer'=>$identity."\n".$contactText."\nOffice hours: ".($school['school_office_hours']?:'Wala pang published office hours; kumpirmahin muna sa school.'),'sourceLabel'=>'School settings · Public contact information'],
        ['id'=>-104,'question'=>'How do I enroll? Enrollment steps admission application process Paano mag enroll magenroll?','answer'=>$stepAnswer."\n\n".$contactText,'sourceLabel'=>$published?'Published enrollment steps':'Public enrollment inquiry guidance'],
        ['id'=>-107,'question'=>'How do I get a portal account register sign up? Paano gumawa account magregister?','answer'=>'Gamitin ang student registration sa sign-in page kapag available. Pending ang bagong student registration hanggang ma-approve ng Super Admin. Para sa ibang role o tulong sa activation, makipag-ugnayan sa school Super Admin. Hindi kayang mag-approve ng account ang chatbot. Huwag magpadala ng password dito.','sourceLabel'=>'Existing student registration and account approval workflow']
    ];
    foreach(['school_admissions_new'=>[-105,'New student freshman enrollment requirements Ano kailangan bagong estudyante?','New students'],'school_admissions_transferee'=>[-106,'Transferee transfer student enrollment requirements Ano kailangan lilipat?','Transferees']] as $key=>[$id,$question,$label]){
        $items=$published?public_school_lines($school[$key]):[];
        $answer=$items?$label." — published requirements:\n• ".implode("\n• ",$items):'Wala pang published school-approved requirements para sa '.$label.'. Kumpirmahin muna ang listahan sa school.';
        $faqs[]=['id'=>$id,'question'=>$question,'answer'=>$answer."\n\n".$contactText,'sourceLabel'=>$items?'Published enrollment requirements · '.$label:'Public enrollment inquiry guidance · '.$label];
    }
    return $faqs;
}

<?php
function init_surveys(PDO $pdo): void {
    $pdo->exec("CREATE TABLE IF NOT EXISTS campus_surveys(id INTEGER PRIMARY KEY AUTOINCREMENT,title TEXT NOT NULL,description TEXT NOT NULL,privacy TEXT NOT NULL CHECK(privacy IN('Anonymous','Identified')),audience TEXT NOT NULL,course_id INTEGER REFERENCES courses(id),section_id INTEGER REFERENCES sections(id),questions TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'Draft' CHECK(state IN('Draft','Published','Closed')),owner_id INTEGER NOT NULL REFERENCES users(id))");
    $pdo->exec('CREATE TABLE IF NOT EXISTS campus_survey_participation(survey_id INTEGER NOT NULL REFERENCES campus_surveys(id),user_id INTEGER NOT NULL REFERENCES users(id),PRIMARY KEY(survey_id,user_id))');
    $pdo->exec('CREATE TABLE IF NOT EXISTS campus_survey_responses(id TEXT PRIMARY KEY,survey_id INTEGER NOT NULL REFERENCES campus_surveys(id),user_id INTEGER REFERENCES users(id),answers TEXT NOT NULL)');
    $pdo->exec('CREATE UNIQUE INDEX IF NOT EXISTS survey_identified_account ON campus_survey_responses(survey_id,user_id) WHERE user_id IS NOT NULL');
}
function survey_questions(string $source): array {
    $questions=[];
    foreach(preg_split('/\r?\n/',trim($source)) as $line){
        if(trim($line)==='')continue;
        $parts=explode('|',$line);$type=trim(array_shift($parts));$label=trim(array_shift($parts)??'');$options=array_map('trim',$parts);
        if(!in_array($type,['rating','choice','text'],true)||strlen($label)<3||strlen($label)>200||($type==='choice'&&(count($options)<2||count($options)>8||count(array_unique($options))!==count($options)))||($type!=='choice'&&$options))throw new InvalidArgumentException('Use rating|Question, text|Question, or choice|Question|Option A|Option B.');
        foreach($options as $option)if(strlen($option)<1||strlen($option)>100)throw new InvalidArgumentException('Choices must contain 1–100 characters.');
        $questions[]=['type'=>$type,'label'=>$label,'options'=>$options];
    }
    if(count($questions)<1||count($questions)>15)throw new InvalidArgumentException('Provide 1–15 questions.');
    return $questions;
}
function survey_eligible(array $survey,array $user): bool {
    $role=$user['role']==='Super Admin'?'Admin':$user['role'];
    if($survey['audience']!=='All'&&$survey['audience']!==$role)return false;
    if(!$survey['course_id']&&!$survey['section_id'])return true;
    $student=$role==='Student'?row('SELECT course_id,section_id FROM students WHERE user_id=?',[$user['id']]):($role==='Parent'?row('SELECT s.course_id,s.section_id FROM students s JOIN parents p ON p.student_id=s.id WHERE p.user_id=?',[$user['id']]):null);
    return $student&&(!$survey['course_id']||(int)$student['course_id']===(int)$survey['course_id'])&&(!$survey['section_id']||(int)$student['section_id']===(int)$survey['section_id']);
}
function survey_create(array $input): int {
    if(!campus_staff())throw new InvalidArgumentException('Administrator access required.');
    $title=trim((string)($input['title']??''));$description=trim((string)($input['description']??''));$privacy=(string)($input['privacy']??'');$audience=(string)($input['audience']??'');$course=(int)($input['course_id']??0);$section=(int)($input['section_id']??0);
    if(strlen($title)<3||strlen($title)>120||strlen($description)>1500||!in_array($privacy,['Anonymous','Identified'],true)||!in_array($audience,['All','Student','Parent','Teacher','Admin'],true))throw new InvalidArgumentException('Check the title, description, privacy, and audience.');
    if(($course||$section)&&!in_array($audience,['Student','Parent'],true))throw new InvalidArgumentException('Course and section filters require a Student or Parent audience.');
    if($course&&!scalar('SELECT COUNT(*) FROM courses WHERE id=? AND is_offered=1',[$course])||$section&&!scalar('SELECT COUNT(*) FROM sections WHERE id=?',[$section]))throw new InvalidArgumentException('Choose a current school course and valid section.');
    $questions=survey_questions((string)($input['questions']??''));
    db()->prepare('INSERT INTO campus_surveys(title,description,privacy,audience,course_id,section_id,questions,owner_id) VALUES(?,?,?,?,?,?,?,?)')->execute([$title,$description,$privacy,$audience,$course?:null,$section?:null,json_encode($questions,JSON_THROW_ON_ERROR),current_user()['id']]);
    return(int)db()->lastInsertId();
}
function survey_submit(int $id,array $answers): void {
    $pdo=db();$pdo->exec('BEGIN IMMEDIATE');
    try {
        $s=row('SELECT * FROM campus_surveys WHERE id=?',[$id]);$u=current_user();
        if(!$s||$s['state']!=='Published'||!survey_eligible($s,$u))throw new InvalidArgumentException('This survey is unavailable for your account.');
        if(scalar('SELECT COUNT(*) FROM campus_survey_participation WHERE survey_id=? AND user_id=?',[$id,$u['id']]))throw new InvalidArgumentException('You have already submitted this survey.');
        $questions=json_decode($s['questions'],true,512,JSON_THROW_ON_ERROR);$clean=[];
        foreach($questions as $i=>$q){$value=$answers[$i]??null;if(!is_string($value))throw new InvalidArgumentException('Answer every question.');$value=trim($value);if($value===''||strlen($value)>1000||($q['type']==='rating'&&!in_array($value,['1','2','3','4','5'],true))||($q['type']==='choice'&&!in_array($value,$q['options'],true)))throw new InvalidArgumentException('Answer every question using the available choices (text: up to 1000 characters).');$clean[]=$value;}
        $pdo->prepare('INSERT INTO campus_survey_participation(survey_id,user_id) VALUES(?,?)')->execute([$id,$u['id']]);
        $pdo->prepare('INSERT INTO campus_survey_responses(id,survey_id,user_id,answers) VALUES(?,?,?,?)')->execute([bin2hex(random_bytes(16)),$id,$s['privacy']==='Identified'?$u['id']:null,json_encode($clean,JSON_THROW_ON_ERROR)]);
        $pdo->exec('COMMIT');
    }catch(Throwable $e){$pdo->exec('ROLLBACK');throw $e;}
}

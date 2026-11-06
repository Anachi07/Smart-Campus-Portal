<?php
function school_gallery_slots(): array {return [1=>'Reception',2=>'Campus entrance',3=>'Campus grounds'];}
function school_gallery_image(string $value): bool {
    if(strlen($value)>1050000)return false;
    foreach(['image/jpeg','image/png'] as $mime){$prefix='data:'.$mime.';base64,';if(!str_starts_with($value,$prefix))continue;$bytes=base64_decode(substr($value,strlen($prefix)),true);if($bytes===false)return false;$info=@getimagesizefromstring($bytes);return $info&&$info['mime']===$mime&&$info[0]>0&&$info[1]>0&&$info[0]<=4000&&$info[1]<=4000;}
    return false;
}
function school_gallery_photos(): array {
    $photos=[];foreach(school_gallery_slots() as $slot=>$label){$image=setting('school_photo_'.$slot,'');if(school_gallery_image($image))$photos[]=['slot'=>$slot,'image'=>$image,'caption'=>setting('school_photo_caption_'.$slot,$label)?:$label];}return $photos;
}
function school_gallery_changes(array $files,array $input): array {
    $changes=[];
    foreach(school_gallery_slots() as $slot=>$label){
        $key='school_photo_'.$slot;$file=$files[$key]??null;$remove=isset($input['remove_photo_'.$slot]);
        if(isset($input['photo_caption_'.$slot])){if(!is_string($input['photo_caption_'.$slot]))throw new InvalidArgumentException('Enter a plain-text photo caption.');$caption=trim($input['photo_caption_'.$slot]);if(strlen($caption)>120)throw new InvalidArgumentException('Photo captions must be within 120 characters.');$changes['school_photo_caption_'.$slot]=$caption?:$label;}
        if(!$file||($file['error']??UPLOAD_ERR_NO_FILE)===UPLOAD_ERR_NO_FILE){if($remove)$changes[$key]='';continue;}
        if($remove)throw new InvalidArgumentException('Choose either a replacement photo or Remove for the same photo.');
        if(!is_int($file['error']??null)||$file['error']!==UPLOAD_ERR_OK||!is_string($file['tmp_name']??null)||!is_uploaded_file($file['tmp_name']))throw new InvalidArgumentException('The photo upload failed. Choose a JPG or PNG and try again.');
        if(filesize($file['tmp_name'])>768000)throw new InvalidArgumentException('Each campus photo must be under 750 KB.');
        $bytes=file_get_contents($file['tmp_name']);$info=@getimagesizefromstring($bytes);
        if(!$info||!in_array($info['mime']??'',['image/jpeg','image/png'],true)||$info[0]<1||$info[1]<1||$info[0]>4000||$info[1]>4000)throw new InvalidArgumentException('Use a valid JPG or PNG photo up to 4000 × 4000 pixels.');
        $changes[$key]='data:'.$info['mime'].';base64,'.base64_encode($bytes);
    }
    return $changes;
}

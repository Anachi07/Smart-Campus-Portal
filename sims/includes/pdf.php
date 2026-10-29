<?php
function pdf_escape_text(string $text): string {
    if(function_exists('iconv')) $text=iconv('UTF-8','Windows-1252//TRANSLIT',$text) ?: $text;
    else $text=strtr($text,['ñ'=>"\xF1",'Ñ'=>"\xD1",'á'=>"\xE1",'é'=>"\xE9",'í'=>"\xED",'ó'=>"\xF3",'ú'=>"\xFA",'–'=>'-','—'=>'-','→'=>'->']);
    $text=preg_replace('/[\x00-\x1F\x7F]/',' ',$text);
    return str_replace(['\\','(',')'],['\\\\','\\(','\\)'],$text);
}
function output_simple_pdf(string $filename,string $title,array $lines): void {
    $wrapped=[];
    foreach($lines?:['No matching records.'] as $line) foreach(explode("\n",wordwrap((string)$line,95,"\n",true)) as $part) $wrapped[]=$part;
    $pages=array_chunk($wrapped,43); $objects=[]; $kids=[];
    $objects[1]='<< /Type /Catalog /Pages 2 0 R >>';
    $objects[3]='<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>';
    foreach($pages as $index=>$page) {
        $pageId=4+$index*2; $contentId=$pageId+1; $kids[]="$pageId 0 R";
        $content='BT /F1 14 Tf 40 792 Td ('.pdf_escape_text($title).") Tj /F1 8 Tf 0 -22 Td ";
        foreach($page as $line) $content.='('.pdf_escape_text($line).") Tj 0 -16 Td ";
        $content.='ET BT /F1 8 Tf 40 26 Td (Page '.($index+1).' of '.count($pages).') Tj ET';
        $objects[$pageId]="<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents $contentId 0 R >>";
        $objects[$contentId]='<< /Length '.strlen($content)." >>\nstream\n$content\nendstream";
    }
    $objects[2]='<< /Type /Pages /Kids ['.implode(' ',$kids).'] /Count '.count($pages).' >>'; ksort($objects);
    $pdf="%PDF-1.4\n"; $offsets=[0];
    foreach($objects as $id=>$object) { $offsets[$id]=strlen($pdf); $pdf.="$id 0 obj\n$object\nendobj\n"; }
    $xref=strlen($pdf); $size=count($objects)+1; $pdf.="xref\n0 $size\n0000000000 65535 f \n";
    for($i=1;$i<$size;$i++) $pdf.=sprintf("%010d 00000 n \n",$offsets[$i]);
    $pdf.="trailer << /Size $size /Root 1 0 R >>\nstartxref\n$xref\n%%EOF";
    header('Content-Type: application/pdf'); header('Content-Disposition: attachment; filename="'.$filename.'"'); header('Cache-Control: no-store'); echo $pdf; exit;
}

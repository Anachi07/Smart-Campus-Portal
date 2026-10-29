<?php
function pdf_escape_text(string $s): string {
    $s = str_replace(['\\','(',')'], ['\\\\','\\(','\\)'], $s);
    return preg_replace('/[^\x20-\x7E]/', '', $s);
}
function output_simple_pdf(string $filename, string $title, array $lines): void {
    $content = "BT /F1 16 Tf 50 800 Td (" . pdf_escape_text($title) . ") Tj /F1 10 Tf 0 -24 Td ";
    foreach ($lines as $line) {
        $content .= "(" . pdf_escape_text(substr($line,0,110)) . ") Tj 0 -15 Td ";
    }
    $content .= "ET";
    $objects = [];
    $objects[] = "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n";
    $objects[] = "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n";
    $objects[] = "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n";
    $objects[] = "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n";
    $objects[] = "5 0 obj << /Length " . strlen($content) . " >> stream\n$content\nendstream endobj\n";
    $pdf = "%PDF-1.4\n";
    $offsets = [0];
    foreach ($objects as $obj) { $offsets[] = strlen($pdf); $pdf .= $obj; }
    $xref = strlen($pdf);
    $pdf .= "xref\n0 " . (count($objects)+1) . "\n0000000000 65535 f \n";
    for ($i=1; $i<=count($objects); $i++) { $pdf .= sprintf("%010d 00000 n \n", $offsets[$i]); }
    $pdf .= "trailer << /Size " . (count($objects)+1) . " /Root 1 0 R >>\nstartxref\n$xref\n%%EOF";
    header('Content-Type: application/pdf');
    header('Content-Disposition: attachment; filename="' . $filename . '"');
    echo $pdf;
    exit;
}
?>

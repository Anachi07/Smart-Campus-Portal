document.addEventListener('DOMContentLoaded',()=>{
 document.querySelectorAll('table').forEach(table=>{const body=table.tBodies[0];if(body&&!body.rows.length){const row=body.insertRow(),cell=row.insertCell();cell.colSpan=Math.max(1,table.tHead?.rows[0]?.cells.length||1);cell.textContent='No records to show yet.';cell.className='text-center text-muted py-4';}});
 const sidebar=document.getElementById('sidebar'),toggle=document.getElementById('sidebarToggle'),overlay=document.getElementById('sidebarOverlay');
 const closeNav=()=>{sidebar?.classList.remove('show');if(overlay)overlay.hidden=true;toggle?.setAttribute('aria-expanded','false');};
 toggle?.addEventListener('click',()=>{const open=!sidebar.classList.contains('show');sidebar.classList.toggle('show',open);overlay.hidden=!open;toggle.setAttribute('aria-expanded',String(open));});
 overlay?.addEventListener('click',closeNav); document.addEventListener('keydown',event=>{if(event.key==='Escape')closeNav();});
 document.getElementById('globalSearch')?.addEventListener('input',event=>{const query=event.target.value.toLowerCase();document.querySelectorAll('tbody tr,.searchable').forEach(row=>{row.hidden=!row.innerText.toLowerCase().includes(query);});});
 document.querySelectorAll('.confirm-delete').forEach(button=>button.addEventListener('click',event=>{if(!confirm('Delete this record? Linked records may prevent deletion.'))event.preventDefault();}));
 document.querySelectorAll('[data-password-toggle]').forEach(button=>button.addEventListener('click',()=>{const input=document.getElementById(button.dataset.passwordToggle);const show=input.type==='password';input.type=show?'text':'password';button.textContent=show?'Hide':'Show';button.setAttribute('aria-label',show?'Hide password':'Show password');}));
 document.querySelectorAll('form[method="post"]').forEach(form=>form.addEventListener('submit',()=>{const button=form.querySelector('button[type="submit"],button:not([type])');if(button){setTimeout(()=>{button.disabled=true;button.dataset.label=button.textContent;button.textContent='Please wait…';},0);}}));
 window.addEventListener('pageshow',()=>document.querySelectorAll('button[data-label]').forEach(button=>{button.disabled=false;button.textContent=button.dataset.label;}));
 const scan=document.getElementById('startScanner'),video=document.getElementById('scannerVideo'),status=document.getElementById('scannerStatus');
 let stream=null,scanning=false;
 const stop=()=>{scanning=false;stream?.getTracks().forEach(track=>track.stop());if(video)video.hidden=true;};
 scan?.addEventListener('click',async()=>{
  if(scanning){stop();scan.textContent='Use camera scanner';return;}
  if(!('BarcodeDetector'in window)||!navigator.mediaDevices?.getUserMedia){status.textContent='Camera scanning is not supported in this browser. Enter the student ID or use a keyboard scanner.';return;}
  try{
   stream=await navigator.mediaDevices.getUserMedia({video:{facingMode:'environment'},audio:false});video.srcObject=stream;video.hidden=false;await video.play();scanning=true;scan.textContent='Stop camera';status.textContent='Point the camera at a QR code containing the student number.';
   const detector=new BarcodeDetector({formats:['qr_code','code_128']});
   const read=async()=>{if(!scanning)return;try{const codes=await detector.detect(video);if(codes.length){const value=codes[0].rawValue.trim();if(/^[a-zA-Z0-9._-]{1,60}$/.test(value)){document.getElementById('student_no').value=value;status.textContent='ID read. Confirm the subject, then mark the student present.';stop();scan.textContent='Use camera scanner';return;}}}catch{}if(scanning)setTimeout(read,250);};read();
  }catch{stop();scan.textContent='Use camera scanner';status.textContent='Camera access is unavailable. You can still enter the student ID manually.';}
 });window.addEventListener('pagehide',stop);
});
const chartColors=['#176b39','#edc647','#748fa3','#d66d62'];
function makeLineChart(id,labels,data,label='Value'){
 if(!window.Chart||!document.getElementById(id))return;
 new Chart(document.getElementById(id),{type:'line',data:{labels,datasets:[{label,data,borderColor:'#176b39',backgroundColor:'#176b3912',pointBackgroundColor:'#176b39',pointRadius:3,borderWidth:2,tension:.3,fill:true,spanGaps:false}]},options:{responsive:true,animation:!matchMedia('(prefers-reduced-motion: reduce)').matches,plugins:{legend:{display:false}},scales:{y:{beginAtZero:true,max:100,grid:{color:'#edf2f5'}},x:{grid:{display:false}}}}});
}
function makeDoughnut(id,labels,data){
 if(!window.Chart||!document.getElementById(id))return;
 new Chart(document.getElementById(id),{type:'doughnut',data:{labels,datasets:[{data,backgroundColor:['#d66d62','#edc647','#176b39'],borderWidth:0,spacing:4}]},options:{responsive:true,cutout:'76%',plugins:{legend:{position:'bottom',labels:{usePointStyle:true,padding:18,font:{size:11}}}}}});
}
function makeBar(id,labels,data,label='Value'){
 if(!window.Chart||!document.getElementById(id))return;
 new Chart(document.getElementById(id),{type:'bar',data:{labels,datasets:[{label,data,backgroundColor:chartColors,borderRadius:5}]},options:{responsive:true,plugins:{legend:{display:false}},scales:{y:{beginAtZero:true}}}});
}

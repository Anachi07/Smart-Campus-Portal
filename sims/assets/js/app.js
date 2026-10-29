document.addEventListener('DOMContentLoaded',()=>{
  const sb=document.getElementById('sidebar');
  const tog=document.getElementById('sidebarToggle');
  if(tog) tog.addEventListener('click',()=>sb.classList.toggle('show'));
  const dm=document.getElementById('darkModeToggle');
  if(localStorage.getItem('darkMode')==='1') document.body.classList.add('dark-mode');
  if(dm) dm.addEventListener('click',()=>{document.body.classList.toggle('dark-mode');localStorage.setItem('darkMode',document.body.classList.contains('dark-mode')?'1':'0')});
  const s=document.getElementById('globalSearch');
  if(s) s.addEventListener('input',()=>{const q=s.value.toLowerCase();document.querySelectorAll('tbody tr,.searchable').forEach(el=>{el.style.display=el.innerText.toLowerCase().includes(q)?'':'none'})});
  document.querySelectorAll('.confirm-delete').forEach(a=>a.addEventListener('click',e=>{if(!confirm('Delete this record?'))e.preventDefault()}));
});
function makeLineChart(id, labels, data, label='Value'){
 if(!window.Chart||!document.getElementById(id)) return;
 new Chart(document.getElementById(id),{type:'line',data:{labels,datasets:[{label,data,tension:.4,fill:true}]},options:{responsive:true,plugins:{legend:{display:false}},scales:{y:{beginAtZero:true,max:100}}}});
}
function makeDoughnut(id, labels, data){
 if(!window.Chart||!document.getElementById(id)) return;
 new Chart(document.getElementById(id),{type:'doughnut',data:{labels,datasets:[{data}]},options:{responsive:true,cutout:'65%'}});
}
function makeBar(id, labels, data, label='Value'){
 if(!window.Chart||!document.getElementById(id)) return;
 new Chart(document.getElementById(id),{type:'bar',data:{labels,datasets:[{label,data}]},options:{responsive:true,plugins:{legend:{display:false}},scales:{y:{beginAtZero:true}}}});
}

(()=>{
 const catalog=JSON.parse(document.getElementById('campusServiceCatalog').textContent);
 const types=document.getElementById('RequestType'),student=document.getElementById('StudentNumber'),office=document.getElementById('requestOffice');
 function update(preserve=false){
  const service=document.querySelector('input[name="ServiceType"]:checked')?.value || 'Document Request';
  const selected=preserve?(types.dataset.selected||types.value):'';
  types.replaceChildren(...(catalog[service]||[]).map(value=>{const option=new Option(value,value);option.selected=value===selected;return option;}));
  office.textContent=service==='Document Request'?'Registrar':'Cashier';
  student.required=service!=='School Fees';student.minLength=student.required?3:0;
  document.getElementById('studentNumberHelp').textContent=student.required?'Required for this request. The office will verify the student record.':'Optional reference for the Cashier. Bring your school ID if available.';
 }
 document.querySelectorAll('input[name="ServiceType"]').forEach(radio=>radio.addEventListener('change',()=>update()));update(true);
})();

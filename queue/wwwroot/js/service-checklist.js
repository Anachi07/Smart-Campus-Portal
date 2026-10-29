(()=>{
 const box=document.getElementById('service-preflight'),type=document.getElementById('RequestType');if(!box||!type)return;let generation=0;
 const make=(tag,text)=>{const node=document.createElement(tag);node.textContent=text;return node;};
 async function refresh(){const id=++generation,service=document.querySelector('[name=ServiceType]:checked')?.value;if(!service)return;
  try{const response=await fetch('/Services/Checklist?serviceType='+encodeURIComponent(service)+'&requestType='+encodeURIComponent(type.value));if(!response.ok)throw Error();const data=await response.json();if(id!==generation)return;box.replaceChildren(make('h3','Before you visit'));
   if(data.requirements.length){const list=document.createElement('ul');for(const item of data.requirements)list.append(make('li',item.title+(item.required?' (required)':' (optional)')+(item.instructions?' · '+item.instructions:'')));box.append(list);}else box.append(make('p','No requirements are listed for this transaction yet. Confirm with the responsible office before visiting.'));
   box.append(make('p',data.notice));const link=make('a','Check office closures →');link.href='/Services/OfficeCalendar';box.append(link);
  }catch{if(id===generation)box.replaceChildren(make('p','Requirements are temporarily unavailable. Ask the office before visiting.'));}
 }
 document.querySelectorAll('[name=ServiceType]').forEach(r=>r.addEventListener('change',refresh));type.addEventListener('change',refresh);refresh();
})();

(() => {
 'use strict';
 const KEY='icas-accessibility-v1';
 const defaults=()=>({version:1,size:'normal',contrast:'system',motion:'system'});
 const normalize=value=>({version:1,size:['normal','large','extra'].includes(value?.size)?value.size:'normal',contrast:['system','standard','high'].includes(value?.contrast)?value.contrast:'system',motion:['system','reduce'].includes(value?.motion)?value.motion:'system'});
 let prefs=defaults(),storageAvailable=true;
 try{const raw=localStorage.getItem(KEY);try{prefs=normalize(raw&&raw.length<4096?JSON.parse(raw):null);}catch{prefs=defaults();}}catch{storageAvailable=false;}
 const root=document.documentElement;
 const reduced=matchMedia('(prefers-reduced-motion: reduce)'),contrast=matchMedia('(prefers-contrast: more)'),forced=matchMedia('(forced-colors: active)');
 let controls,status,details,summary,timer,marked=new Map(),layoutObserver;
 const observed=new WeakSet();
 function layout(){if(!document.body)return;const bars=[...document.querySelectorAll('.campus-accessibility,.campus-app-tools')];const height=bars.reduce((n,bar)=>n+bar.getBoundingClientRect().height,0);const a11y=document.querySelector('.campus-accessibility')?.getBoundingClientRect().height||0;root.style.setProperty('--campus-tools-height',height+'px');root.style.setProperty('--campus-a11y-height',a11y+'px');const header=document.querySelector('body.app-body>.topbar');if(header)root.style.setProperty('--campus-header-height',header.getBoundingClientRect().height+'px');for(const el of [...bars,...(header?[header]:[])])if(layoutObserver&&!observed.has(el)){observed.add(el);layoutObserver.observe(el);}}
 function resolve(){
  root.dataset.campusSize=prefs.size;
  root.dataset.campusContrast=prefs.contrast==='high'||prefs.contrast==='system'&&(contrast.matches||forced.matches)?'high':'standard';
  root.dataset.campusMotion=prefs.motion==='reduce'||reduced.matches?'reduce':'standard';
 }
 // Resolve visual preferences before page content is painted; no account data is read.
 resolve();
 function wrapTables(){
  for(const table of document.querySelectorAll('table:not([data-campus-table])')){table.dataset.campusTable='';if(table.closest('.table-responsive,.campus-table-scroll'))continue;const wrapper=document.createElement('div');wrapper.className='campus-table-scroll';table.before(wrapper);wrapper.append(table);}
 }
 function tableAccess(){for(const wrapper of document.querySelectorAll('.campus-table-scroll')){if(wrapper.scrollWidth>wrapper.clientWidth+1){wrapper.tabIndex=0;wrapper.setAttribute('role','region');wrapper.setAttribute('aria-label','Scrollable data table');}else{wrapper.removeAttribute('tabindex');wrapper.removeAttribute('role');wrapper.removeAttribute('aria-label');}}}
 function restoreFonts(){for(const [el,saved] of marked){if(saved.value)el.style.setProperty('font-size',saved.value,saved.priority);else el.style.removeProperty('font-size');el.removeAttribute('data-campus-font');}marked.clear();}
 function scaleText(){
  clearTimeout(timer);root.dataset.campusMeasuring='';
  try{restoreFonts();if(prefs.size==='normal'||!document.body)return;
  const elements=new Set(document.body.querySelectorAll('input,select,textarea,button'));
  const walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);
  while(walker.nextNode()){const node=walker.currentNode,el=node.parentElement;if(node.textContent.trim()&&el?.namespaceURI==='http://www.w3.org/1999/xhtml'&&!el.closest('script,style,noscript,template,svg,math,[aria-hidden="true"]'))elements.add(el);}
  // Measure every original size first to avoid compounding inherited sizes.
  const measurements=[];for(const el of elements){if(el.closest('script,style,template,svg,math'))continue;const px=parseFloat(getComputedStyle(el).fontSize);if(Number.isFinite(px)&&px>0)measurements.push([el,px,{value:el.style.getPropertyValue('font-size'),priority:el.style.getPropertyPriority('font-size')}]);}
  const factor=prefs.size==='extra'?1.3:1.15;
  for(const [el,px,saved] of measurements){marked.set(el,saved);el.dataset.campusFont='';el.style.setProperty('font-size',`${Math.round(px*factor*100)/100}px`,'important');}
  }finally{document.body?.getBoundingClientRect();delete root.dataset.campusMeasuring;tableAccess();}
 }
 function schedule(){clearTimeout(timer);timer=setTimeout(scaleText,60);}
 function updateCharts(){if(!window.Chart)return;for(const chart of Object.values(window.Chart.instances||{})){chart.stop();chart.options.animation=root.dataset.campusMotion==='reduce'?false:undefined;chart.update('none');}}
 function sync(){if(!controls)return;controls.size.value=prefs.size;controls.contrast.value=prefs.contrast;controls.motion.value=prefs.motion;}
 function announce(message){if(status)status.textContent=message;}
 function apply(){resolve();sync();scaleText();updateCharts();window.dispatchEvent(new CustomEvent('campus:accessibilitychange',{detail:{...prefs}}));}
 function save(){try{localStorage.setItem(KEY,JSON.stringify(prefs));storageAvailable=true;}catch{storageAvailable=false;}announce(storageAvailable?'Preferences saved on this browser.':'Applied to this page. Browser storage is unavailable, so preferences may not persist.');}
 function close(returnFocus=false){if(details){details.open=false;if(returnFocus)summary.focus();}}
 function ready(){
  const bar=document.createElement('aside');bar.className='campus-accessibility';bar.setAttribute('aria-label','Display preferences');
  details=document.createElement('details');summary=document.createElement('summary');summary.textContent='Accessibility';summary.setAttribute('aria-label','Accessibility preferences');
  const panel=document.createElement('div');panel.className='campus-accessibility-panel';
  const heading=document.createElement('h2');heading.textContent='Display preferences';
  const help=document.createElement('p');help.textContent='These settings apply to this browser, including shared devices. Your account and records are unchanged.';
  panel.append(heading,help);controls={};
  for(const [name,label,options] of [
   ['size','Text size',[['normal','Normal'],['large','Large (115%)'],['extra','Extra large (130%)']]],
   ['contrast','Contrast',[['system','Use device setting'],['standard','Standard'],['high','High contrast']]],
   ['motion','Motion',[['system','Use device setting'],['reduce','Reduce motion']]]
  ]){const labelNode=document.createElement('label'),select=document.createElement('select');select.id=`campus-accessibility-${name}`;labelNode.htmlFor=select.id;labelNode.textContent=label;for(const [value,text] of options){const option=document.createElement('option');option.value=value;option.textContent=text;select.append(option);}select.addEventListener('change',()=>{prefs=normalize({size:controls.size.value,contrast:controls.contrast.value,motion:controls.motion.value});apply();save();});controls[name]=select;panel.append(labelNode,select);}
  const reset=document.createElement('button');reset.type='button';reset.textContent='Reset display preferences';reset.addEventListener('click',()=>{prefs=defaults();try{localStorage.removeItem(KEY);storageAvailable=true;}catch{storageAvailable=false;}apply();announce(storageAvailable?'Display preferences reset to device defaults.':'Reset on this page. Browser storage is unavailable.');});
  const dismiss=document.createElement('button');dismiss.type='button';dismiss.textContent='Close preferences';dismiss.addEventListener('click',()=>close(true));
  status=document.createElement('p');status.setAttribute('role','status');status.setAttribute('aria-live','polite');status.textContent=storageAvailable?'Changes are saved automatically.':'Browser storage is unavailable; settings apply to this page.';
  panel.append(reset,dismiss,status);details.append(summary,panel);bar.append(details);document.body.prepend(bar);sync();wrapTables();scaleText();
  document.addEventListener('keydown',event=>{if(event.key==='Escape'&&details.open){event.preventDefault();close(true);}});
  document.addEventListener('click',event=>{if(details.open&&!details.contains(event.target))close();});
  details.addEventListener('focusout',event=>{if(event.relatedTarget&&!details.contains(event.relatedTarget))close();});
  if('ResizeObserver' in window)layoutObserver=new ResizeObserver(layout);layout();
  new MutationObserver(records=>{const added=records.flatMap(r=>[...r.addedNodes]);if(added.some(node=>node.nodeType===Node.ELEMENT_NODE))wrapTables();layout();if(prefs.size!=='normal'&&added.some(node=>node.nodeType===Node.ELEMENT_NODE||node.nodeType===Node.TEXT_NODE&&node.textContent.trim()&&!node.parentElement?.hasAttribute('data-campus-font')))schedule();}).observe(document.body,{childList:true,subtree:true});
  window.addEventListener('resize',()=>{schedule();layout();});document.fonts?.ready.then(schedule);
 }
 for(const media of [reduced,contrast,forced])media.addEventListener('change',apply);
 window.addEventListener('storage',event=>{if(event.key===KEY||event.key===null){try{prefs=normalize(event.newValue&&event.newValue.length<4096?JSON.parse(event.newValue):null);}catch{prefs=defaults();}apply();announce('Display preferences updated from another tab.');}});
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',ready,{once:true});else ready();
})();

(() => {
 const make=(tag,text)=>{const n=document.createElement(tag);n.textContent=text??'';return n;};
 document.querySelectorAll('[data-add-approval]').forEach(button=>button.addEventListener('click',()=>{const list=button.previousElementSibling;if(list.children.length>=10)return;const label=list.lastElementChild.cloneNode(true);label.firstChild.textContent='Approving office '+(list.children.length+1);label.querySelector('select').value='';list.append(label);}));
 const classWindow=document.querySelector('[data-class-window]');
 if(classWindow){let busy=false,ended=false,lastCode;const status=classWindow.querySelector('[data-class-status]');
  async function rotate(){if(busy||ended||document.hidden)return;busy=true;try{
   const response=await fetch(location.pathname,{method:'POST',body:new URLSearchParams({action:'rotate',session:classWindow.dataset.classWindow,csrf:classWindow.dataset.classCsrf}),cache:'no-store',signal:AbortSignal.timeout(10000)});
   if(!response.ok){ended=response.status===409;throw Error();}const data=await response.json();
   if(data.code===lastCode)return;
   const qr=await fetch('/CampusTools/AttendanceQr',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','X-Campus-Csrf':classWindow.dataset.classCsrf},body:new URLSearchParams({session:data.session,code:data.code}),cache:'no-store',signal:AbortSignal.timeout(10000)});
   if(!qr.ok)throw Error();const image=classWindow.querySelector('[data-class-qr]');image.src='data:image/svg+xml;base64,'+btoa(await qr.text());image.hidden=false;lastCode=data.code;
   classWindow.querySelector('[data-class-code]').textContent=data.code;status.textContent='Current code expires at '+new Date(data.expiresAt*1000).toLocaleTimeString();
  }catch{lastCode=null;classWindow.querySelector('[data-class-qr]').hidden=true;classWindow.querySelector('[data-class-code]').textContent='';status.textContent=ended?'This class window is closed.':'Unable to refresh the class code. Retrying…';}finally{busy=false;}}
  rotate();setInterval(rotate,3000);document.addEventListener('visibilitychange',rotate);
 }
 const code=document.querySelector('[data-student-code]');if(code){const value=new URLSearchParams(location.hash.slice(1)).get('code');if(value&&/^[a-f0-9]{32}$/.test(value)){code.value=value;history.replaceState(null,'',location.pathname+location.search);}}
 function renderTimeline(events){const timeline=document.querySelector('[data-campus-timeline]');if(!timeline)return;timeline.replaceChildren();for(const event of events.sort((a,b)=>new Date(b.at)-new Date(a.at)).slice(0,20)){const li=make('li'),a=make('a',event.title);a.href=event.url;li.append(a,make('small',new Date(event.at).toLocaleString([],{timeZone:'Asia/Manila'})+' · Manila'));timeline.append(li);}if(!events.length)timeline.append(make('li','No recent transactions.'));}
 renderTimeline(window.campusLocalTimeline||[]);
 async function queueOverview(){const cards=document.querySelector('[data-campus-summary]'),timeline=document.querySelector('[data-campus-timeline]'),calendar=document.querySelector('[data-campus-calendar]');if(!cards&&!timeline&&!calendar)return;
  try{const response=await fetch('/Services/CampusOverview',{cache:'no-store',signal:AbortSignal.timeout(10000)});if(!response.ok)throw Error();const data=await response.json();
   if(cards){cards.replaceChildren();for(const [label,count,path] of data.metrics){const a=make('a');a.href=path;a.append(make('strong',count),make('span',label));cards.append(a);}}
   if(timeline)renderTimeline([...(window.campusLocalTimeline||[]),...data.timeline]);
   if(calendar){calendar.replaceChildren();for(const visit of data.visits){const p=make('p');const a=make('a',visit.title+' · '+new Date(visit.at).toLocaleString([],{timeZone:'Asia/Manila'})+' · Manila');a.href=visit.url;p.append(a);calendar.append(p);}for(const closure of data.closures){calendar.append(make('p',`${closure.office}: ${closure.title} · ${closure.when}`));}if(!calendar.children.length)calendar.append(make('p','No upcoming reserved visits or office closures.'));}
   document.querySelectorAll('[data-campus-feed-status]').forEach(n=>n.textContent='Updated '+new Date().toLocaleTimeString());
  }catch{document.querySelectorAll('[data-campus-feed-status]').forEach(n=>n.textContent='Live office updates are unavailable. Open My service requests for current details.');}
 }
 queueOverview();
})();

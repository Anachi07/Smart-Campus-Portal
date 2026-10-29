(()=>{
 if(window.ICASAssistant)return;
 const make=(tag,cls,text)=>{const node=document.createElement(tag);if(cls)node.className=cls;if(text!==undefined)node.textContent=text;return node;};
 const root=make('div','icas-assistant');
 root.innerHTML='<button class="assistant-launcher" type="button" aria-label="Open ICAS Assistant" aria-expanded="false"><span aria-hidden="true">✦</span> Ask ICAS</button><section class="assistant-dialog" role="dialog" aria-label="ICAS Assistant" hidden><header class="assistant-header"><span class="assistant-emblem" aria-hidden="true">✦</span><div><strong>ICAS Assistant</strong><small>School guidance & campus visits</small></div><button type="button" class="assistant-close" aria-label="Close assistant">×</button></header><div class="assistant-messages" role="log" aria-live="polite" aria-relevant="additions"></div><form class="assistant-compose"><label class="visually-hidden" for="assistant-question">Your school question</label><input id="assistant-question" maxlength="1200" autocomplete="off" placeholder="Ask a school or queue question…" required><button type="submit" aria-label="Send question">Send</button></form><div class="assistant-privacy">Don’t share passwords or payment details.</div></section>';
 document.body.append(root);
 const dialog=root.querySelector('.assistant-dialog'),launcher=root.querySelector('.assistant-launcher'),messages=root.querySelector('.assistant-messages'),compose=root.querySelector('form'),input=root.querySelector('input');
 let context=null,history=[],busy=false,lastFocus=null,bookingPending=false;
 const format=value=>new Intl.DateTimeFormat('en-PH',{timeZone:'Asia/Manila',month:'short',day:'numeric',year:'numeric',hour:'numeric',minute:'2-digit'}).format(new Date(value));
 const day=value=>new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Manila',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date(value));
 const scroll=()=>{messages.scrollTop=messages.scrollHeight;};
 const reveal=node=>{messages.scrollTo({top:messages.scrollTop+node.getBoundingClientRect().top-messages.getBoundingClientRect().top-10,behavior:'instant'});};
 const message=(text,who='assistant')=>{const node=make('div','assistant-message '+who,text);messages.append(node);scroll();return node;};
 const button=(label,handler,cls='assistant-button')=>{const node=make('button',cls,label);node.type='button';node.addEventListener('click',handler);return node;};
 async function api(path,body){
  const response=await fetch('/Assistant/'+path,{method:body?'POST':'GET',credentials:'same-origin',headers:body?{'Content-Type':'application/json','RequestVerificationToken':context?.token||''}:{},body:body?JSON.stringify(body):undefined});
  let data;try{data=await response.json();}catch{throw Error('The assistant is temporarily unavailable. Please try again.');}
  if(!response.ok)throw Error(data.message||'Unable to complete this request. Please try again.');return data;
 }
 function quickActions(container=messages){const actions=make('div','assistant-quick-actions');actions.append(button('Find a visit time',()=>schedule()),button('My requests',()=>bookings()),button('Exchange requests',()=>exchanges()),button('School services',()=>ask('What can the campus portal do?')));container.append(actions);scroll();}
 async function ready(){if(!context)context=await api('Context');return context;}
 function signIn(){const box=message('Sign in with your campus account to reserve a time or view your own requests.');const link=make('a','assistant-button','Sign in to continue');link.href='/sims/login.php';box.append(link);scroll();}
 async function open(action='open'){
  lastFocus=document.activeElement;dialog.hidden=false;launcher.setAttribute('aria-expanded','true');window.ICASAssistant.isOpen=true;
  if(!messages.children.length){message('Hello! I can help with published school information and Registrar/Cashier visits. What would you like to do?');quickActions();}
  try{await ready();if(action==='schedule')await schedule();if(action==='bookings')await bookings();if(action==='exchanges')await exchanges();}catch(error){message(error.message);}
  if(action==='open'&&!matchMedia('(max-width:640px)').matches)input.focus();
 }
 function close(){dialog.hidden=true;launcher.setAttribute('aria-expanded','false');window.ICASAssistant.isOpen=false;lastFocus?.focus();}
 root.querySelector('.assistant-close').addEventListener('click',close);launcher.addEventListener('click',()=>dialog.hidden?open():close());
 document.addEventListener('click',event=>{const target=event.target.closest('[data-assistant-action]');if(target){event.preventDefault();open(target.dataset.assistantAction||'open');}});
 root.addEventListener('keydown',event=>{if(event.key==='Escape'){event.stopPropagation();close();}if(event.key==='Tab'&&!dialog.hidden){const nodes=[...dialog.querySelectorAll('button,a,input,select,textarea')].filter(n=>!n.disabled&&!n.hidden&&n.getClientRects().length);if(!nodes.length)return;const first=nodes[0],last=nodes.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}});
 window.ICASAssistant={isOpen:false,open};
 async function ask(text){
  if(busy)return;
  if(/(?:password|api[ _-]?key|access[ _-]?token)\s*(?:is|ay|:|=)\s*\S+|sk-[A-Za-z0-9]{20,}|AIza[A-Za-z0-9_-]{20,}/i.test(text)){input.value='';message('Please do not share passwords or access keys here. Use My profile to change a password or ask your school administrator for help.');return;}
  busy=true;compose.querySelector('button').disabled=true;message(text,'user');input.value='';const loading=message('Looking up school information…');
  try{
   await ready();const reply=await api('Chat',{message:text,history:history.slice(-4).map(h=>({...h,content:h.content.slice(0,1200)}))});loading.remove();
   const box=message(reply.answer);box.append(make('small','assistant-mode',reply.mode==='ai'?'AI-assisted answer · Published school sources':reply.mode==='workflow'?'Queue scheduling':'School guide · Published information'));
   if(reply.sources?.length){const list=make('div','assistant-sources');for(const source of reply.sources){const item=make('details');item.append(make('summary','',source.sourceLabel));item.append(make('p','',source.answer));list.append(item);}box.append(list);}
   history.push({role:'user',content:text},{role:'assistant',content:reply.answer});history=history.slice(-4);
   if(reply.suggestScheduling)box.append(button('Choose a visit time',()=>schedule(reply.serviceType)),button('Manage my visits',()=>bookings()));
  }catch(error){loading.remove();message(error.message);}finally{busy=false;compose.querySelector('button').disabled=false;scroll();}
 }
 compose.addEventListener('submit',event=>{event.preventDefault();const value=input.value.trim();if(value)ask(value);});
 function field(form,label,tag='input',options={}){const row=make('label','assistant-field',label);const node=make(tag);node.setAttribute('aria-label',label);Object.assign(node,options);row.append(node);form.append(row);return node;}
 async function schedule(initialService,preset={}){
  if(bookingPending)return;
  try{await ready();}catch(error){message(error.message);return;}
  if(!context.authenticated){signIn();return;}
  if(!context.settings.enableAppointments){message('Online visit reservations are currently paused. You can still request an ordinary queue ticket or contact the office.');return;}
  messages.querySelectorAll('.assistant-booking-form,.assistant-slot-card:not(.assistant-live-bookings),.assistant-review').forEach(n=>n.remove());
  const form=make('form','assistant-booking-form');form.append(make('h3','','Plan your campus visit'));
  form.append(make('p','assistant-help','Choose a transaction. I’ll show available times and consider your saved class schedule when one is available. All times are in Manila time.'));
  const service=field(form,'Service','select',{required:true});for(const value of Object.keys(context.services))service.add(new Option(value,value));if(initialService&&context.services[initialService])service.value=initialService;
  const type=field(form,'Transaction','select',{required:true});const date=field(form,'Preferred date (optional)','input',{type:'date',min:day(context.now)});
  const max=new Date(context.now);max.setUTCDate(max.getUTCDate()+context.settings.advanceBookingDays);date.max=day(max);if(preset.preferredDate)date.value=preset.preferredDate;
  const student=field(form,'Student / reference number','input',{value:preset.studentNumber??context.studentNumber??'',maxLength:40});
  const phone=field(form,'Contact number','input',{value:preset.phoneNumber??context.phoneNumber??'',type:'tel',inputMode:'numeric',pattern:'09[0-9]{9}',maxLength:11,placeholder:'09XXXXXXXXX',required:true});
  const notes=field(form,'Office notes (optional)','textarea',{rows:2,maxLength:400,value:preset.requestNotes||''});
  const update=()=>{type.replaceChildren(...context.services[service.value].map(value=>new Option(value,value)));student.required=service.value!=='School Fees';student.minLength=student.required?3:0;};service.addEventListener('change',update);update();if(preset.requestType&&context.services[service.value].includes(preset.requestType))type.value=preset.requestType;
  const submit=make('button','assistant-button primary','Find available times');submit.type='submit';form.append(submit);messages.append(form);reveal(form);
  form.addEventListener('submit',async event=>{event.preventDefault();submit.disabled=true;
   try{const data=await api('Slots?serviceType='+encodeURIComponent(service.value)+(date.value?'&date='+encodeURIComponent(date.value):''));const payload={serviceType:service.value,requestType:type.value,studentNumber:student.value.trim(),phoneNumber:phone.value,requestNotes:notes.value.trim(),preferredDate:date.value};if(data.slots?.length)form.hidden=true;showSlots(data,payload);}
   catch(error){message(error.message);}finally{submit.disabled=false;}
  });
 }
 function showSlots(data,payload){
  messages.querySelectorAll('.assistant-slot-card:not(.assistant-live-bookings),.assistant-review').forEach(n=>n.remove());
  const card=make('div','assistant-slot-card');card.append(make('h3','','Choose a visit time'));card.append(make('p','assistant-help',payload.serviceType+' · '+payload.requestType));
  if(!data.slots?.length){card.append(make('p','','No suitable times are available for that date. Try another day, review class assignments, or contact the office.'));messages.append(card);reveal(card);return;}
  card.append(make('p','assistant-help',data.notice));
  const list=make('div','assistant-slot-list');
  const sorted=[...data.slots].sort((a,b)=>Number(b.recommended)-Number(a.recommended)||new Date(a.startsAt)-new Date(b.startsAt));
  const render=items=>{for(const slot of items){const choose=button(format(slot.startsAt),()=>review(payload,slot),'assistant-slot');if(slot.recommended)choose.append(make('span','assistant-slot-tag','Suggested'));choose.append(make('small','',slot.reason+' '+slot.remainingCapacity+' reservation space(s) left.'));list.append(choose);}};
  let visible=3;render(sorted.slice(0,visible));card.append(list);
  if(sorted.length>visible)card.append(button('Show more times',event=>{render(sorted.slice(visible,visible+6));visible+=6;if(visible>=sorted.length)event.currentTarget.remove();},'assistant-text-button'));
  card.append(button('Change details',()=>schedule(payload.serviceType,payload),'assistant-text-button'));
  messages.append(card);reveal(card);
 }
 function review(payload,slot){
  if(bookingPending)return;messages.querySelectorAll('.assistant-review').forEach(n=>n.remove());
  messages.querySelectorAll('.assistant-slot-card:not(.assistant-live-bookings)').forEach(n=>n.hidden=true);
  const card=make('div','assistant-review');card.append(make('h3','','Review your visit'));
  const values=[['Office',slot.office],['Transaction',payload.serviceType+' · '+payload.requestType],['When',format(slot.startsAt)+' · Manila'],['Student / Ref.',payload.studentNumber||'Not supplied'],['Contact',payload.phoneNumber]];
  for(const [label,value]of values){const row=make('div','assistant-review-row');row.append(make('span','',label),make('strong','',value));card.append(row);}
  card.append(make('p','assistant-help','Nothing is booked yet. This reserves a visit, not a document release or payment. Actual service time depends on the office queue.'));
  const confirm=button('Confirm this visit',async()=>{confirm.disabled=true;bookingPending=true;try{const data=await api('Book',{...payload,startsAt:slot.startsAt,idempotencyKey:key,confirmed:true});card.replaceChildren();card.append(make('h3','','Your visit is reserved'),make('p','',data.message));renderBooking(card,data.booking);}
   catch(error){message(error.message);confirm.disabled=false;}finally{bookingPending=false;}reveal(card);},'assistant-button primary');
  const key=crypto.randomUUID();card.append(confirm,button('Choose another time',()=>{card.remove();const slots=messages.querySelector('.assistant-slot-card:not(.assistant-live-bookings)');if(slots){slots.hidden=false;reveal(slots);}},'assistant-text-button'));messages.append(card);reveal(card);
 }
 function renderBooking(container,booking){
  const card=make('div','assistant-booking');card.append(make('strong','',booking.ticketNumber),make('p','',booking.serviceType+' · '+booking.requestType),make('p','',booking.scheduledAt?format(booking.scheduledAt)+' · Manila':booking.estimatedWaitText),make('span','assistant-booking-status',booking.status));
  const link=make('a','assistant-text-button','Open ticket');link.href='/Queue/Ticket?id='+encodeURIComponent(booking.ticketNumber);card.append(link);
  if(booking.canCheckIn)card.append(button('I have arrived · Check in',async event=>{event.currentTarget.disabled=true;try{const data=await api('CheckIn',{ticketNumber:booking.ticketNumber});message(data.message);await bookings();}catch(error){message(error.message);event.currentTarget.disabled=false;}},'assistant-button primary'));
  if(booking.status==='Scheduled')card.append(make('p','assistant-help','Check in from 15 minutes before until 30 minutes after your visit time.'));
  if(booking.canChange)card.append(button('Change visit time',()=>changeVisit(booking),'assistant-text-button'),button('Request time exchange',()=>changeVisit(booking,true),'assistant-text-button'));
  if(booking.canCancel)card.append(button('Cancel this request',async event=>{if(!confirm('Cancel this request and release its reserved time?'))return;event.currentTarget.disabled=true;try{const data=await api('Cancel',{ticketNumber:booking.ticketNumber});message(data.message);card.remove();}catch(error){message(error.message);event.currentTarget.disabled=false;}},'assistant-text-button danger'));
  container.append(card);
 }
 function clearWizard(){messages.querySelectorAll('.assistant-booking-form,.assistant-slot-card:not(.assistant-live-bookings),.assistant-review,.assistant-changes,.assistant-exchanges').forEach(n=>n.remove());}
 async function changeVisit(booking,exchange=false){
  if(bookingPending)return;
  try{
   const data=await api((exchange?'ExchangeTimes':'ChangeSlots')+'?ticketNumber='+encodeURIComponent(booking.ticketNumber));clearWizard();
   const card=make('section','assistant-slot-card assistant-changes');card.append(make('h3','',exchange?'Request a voluntary exchange':'Change your visit time'),make('p','assistant-help',booking.ticketNumber+' · Current: '+format(booking.scheduledAt)),make('p','assistant-help',data.notice));
   const times=exchange?data.times:data.slots;
   if(!times?.length)card.append(make('p','assistant-help',exchange?'No eligible occupied times are available for exchange. Try an available time instead.':'No suitable alternative times are available. Your current time remains reserved.'));
   const list=make('div','assistant-slot-list');let shown=0;
   const more=button('Show more times',()=>render(),'assistant-text-button');
   const render=()=>{for(const time of times.slice(shown,shown+6))list.append(button(format(time.startsAt),()=>reviewChange(booking,time,exchange),'assistant-slot'));shown+=6;more.hidden=shown>=times.length;};
   if(times?.length){render();card.append(list,more);}
   if(!exchange)card.append(button('Ask for a time exchange',()=>changeVisit(booking,true),'assistant-text-button'));
   card.append(button('Back to my requests',()=>bookings(),'assistant-text-button'));messages.append(card);reveal(card);
  }catch(error){message(error.message);}
 }
 function reviewChange(booking,time,exchange){
  if(bookingPending)return;messages.querySelectorAll('.assistant-review').forEach(n=>n.remove());messages.querySelectorAll('.assistant-changes').forEach(n=>n.hidden=true);
  const card=make('section','assistant-review');card.append(make('h3','',exchange?'Review your exchange offer':'Review the time change'));
  for(const [label,value]of [['Your ticket',booking.ticketNumber],['Current time',format(booking.scheduledAt)],['Proposed time',format(time.startsAt)]]){const row=make('div','assistant-review-row');row.append(make('span','',label),make('strong','',value));card.append(row);}
  card.append(make('p','assistant-help',exchange?'Your current time stays reserved. This sends an in-app invitation to eligible holders at the proposed time. The first eligible acceptance exchanges both times together. No response or a decline leaves your visit unchanged. Invitations expire within 24 hours or one hour before either visit.':'Your old time is released only after you confirm and availability is checked again. Changes close one hour before your current visit. All times are in Manila time.'));
  const submit=button(exchange?'Send exchange invitation':'Confirm time change',async()=>{
   submit.disabled=true;bookingPending=true;
   try{const data=await api(exchange?'RequestExchange':'Reschedule',{ticketNumber:booking.ticketNumber,expectedStartsAt:booking.scheduledAt,startsAt:time.startsAt,confirmed:true});clearWizard();message(data.message);await bookings();if(exchange)await exchanges();}
   catch(error){message(error.message);submit.disabled=false;}finally{bookingPending=false;}
  },'assistant-button primary');
  card.append(submit,button('Go back',()=>{card.remove();const previous=messages.querySelector('.assistant-changes');if(previous){previous.hidden=false;reveal(previous);}},'assistant-text-button'));messages.append(card);reveal(card);
 }
 async function exchanges(){
  try{
   await ready();if(!context.authenticated){signIn();return;}
   const data=await api('Exchanges');messages.querySelectorAll('.assistant-exchanges').forEach(n=>n.remove());const card=make('section','assistant-slot-card assistant-exchanges');card.append(make('h3','','Exchange requests'),make('p','assistant-help','Only your own ticket and the offered times are shown. A time exchange needs both owners’ consent and must still fit both class schedules.'));
   if(!data.exchanges.length)card.append(make('p','assistant-help','No exchange invitations yet. Open My requests to offer a time exchange.'));
   const past=make('details','assistant-exchange-history');past.append(make('summary','','Previous exchanges'));const pastBody=make('div');past.append(pastBody);
   for(const item of data.exchanges){
    const box=make('div','assistant-booking');box.append(make('strong','',item.incoming?'Invitation to exchange':'Your exchange offer'),make('p','',item.ticketNumber),make('p','','Original: '+format(item.currentStartsAt)),make('p','','Proposed: '+format(item.proposedStartsAt)),make('span','assistant-booking-status',item.status));
    if(item.canRespond||item.canCancel)box.append(make('p','assistant-help','Respond by '+format(item.expiresAt)+' · Manila. Your reservation is unchanged while this is pending.'));
    const decisionButton=(label,decision)=>button(label,()=>{
     messages.querySelectorAll('.assistant-review').forEach(n=>n.remove());
     card.hidden=true;
     const review=make('section','assistant-review');review.append(make('h3','',label+'?'),make('p','assistant-help',decision==='accept'?'Accept changing your visit from '+format(item.currentStartsAt)+' to '+format(item.proposedStartsAt)+' (Manila). Both times will be checked and exchanged together.':'This will close your participation in the exchange. Your reservation will keep its current time.'));
     const confirm=button('Confirm '+decision,async()=>{confirm.disabled=true;try{const result=await api('RespondExchange',{id:item.id,decision,confirmed:true});review.remove();message(result.message);await exchanges();await refreshBadge();}catch(error){message(error.message);confirm.disabled=false;}},'assistant-button primary');
     review.append(confirm,button('Keep pending',()=>{review.remove();card.hidden=false;reveal(card);},'assistant-text-button'));messages.append(review);reveal(review);
    },decision==='accept'?'assistant-button primary':'assistant-text-button');
    if(item.canRespond)box.append(decisionButton('Accept exchange','accept'),decisionButton('Decline','decline'));
    if(item.canCancel)box.append(decisionButton('Withdraw offer','withdraw'));
    if(item.canRespond||item.canCancel)card.append(box);else pastBody.append(box);
   }
   if(pastBody.children.length)card.append(past);
   card.append(button('Refresh exchanges',()=>exchanges(),'assistant-text-button'));messages.append(card);reveal(card);
  }catch(error){message(error.message);}
 }
 const badge=make('span','assistant-invitation-badge');badge.hidden=true;launcher.append(badge);
 async function refreshBadge(){try{if(document.hidden)return;const latest=await api('Context');context=latest;badge.hidden=!latest.pendingExchanges;badge.textContent=latest.pendingExchanges?String(latest.pendingExchanges):'';badge.title='Pending exchange invitations';}catch{}}
 setTimeout(refreshBadge,2000);setInterval(()=>{if(context?.authenticated)refreshBadge();},60000);

 async function bookings(){try{await ready();if(!context.authenticated){signIn();return;}const data=await api('Bookings');messages.querySelectorAll('.assistant-live-bookings').forEach(n=>n.remove());const card=make('section','assistant-slot-card assistant-live-bookings');card.append(make('h3','','Your active requests'));if(!data.bookings.length)card.append(make('p','','You have no active requests or reserved visits.'));for(const booking of data.bookings)renderBooking(card,booking);card.append(button('Refresh my requests',()=>bookings(),'assistant-text-button'),button('Exchange requests',()=>exchanges(),'assistant-text-button'));messages.append(card);reveal(card);}catch(error){message(error.message);}}
})();

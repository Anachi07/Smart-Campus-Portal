if(process.env.CAMPUS_ALLOW_DESTRUCTIVE_TESTS!=='1')throw Error('Use disposable data and set CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1; this suite restores a workspace backup.');
const {chromium}=require(process.env.CAMPUS_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.CAMPUS_TEST_URL||'http://127.0.0.1:18080';
const assert=(ok,message)=>{if(!ok)throw Error(message);};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CAMPUS_CHROMIUM||undefined,args:['--no-sandbox']});const errors=[],results=[];
 const login=async(page,user,password)=>{await page.goto(base+'/sims/login.php');await page.locator('#username').fill(user);await page.locator('#password').fill(password);await Promise.all([page.waitForURL('**/portal'),page.getByRole('button',{name:/Sign in/}).click()]);};
 const api=async(page,path,data)=>{const response=await page.evaluate(async({path,data})=>{const context=await fetch('/Assistant/Context').then(r=>r.json());const r=await fetch('/Assistant/'+path,{method:data?'POST':'GET',headers:data?{'Content-Type':'application/json','RequestVerificationToken':context.token}:{},body:data?JSON.stringify(data):undefined});return {status:r.status,body:await r.json()};},{path,data});assert(response.status===200,path+': '+JSON.stringify(response));return response.body;};
 const ownerContext=await browser.newContext();const owner=await ownerContext.newPage();await login(owner,'ADMIN','ADMIN');
 const original=await owner.request.get(base+'/sims/modules/admin/backup.php?download=1').then(r=>r.json());
 try{
  for(const [device,viewport]of [['desktop',{width:1440,height:1000}],['mobile',{width:390,height:844}]]){
   const ac=await browser.newContext({viewport}),bc=await browser.newContext({viewport});const a=await ac.newPage(),b=await bc.newPage();
   for(const p of [a,b])p.on('pageerror',e=>errors.push(device+': '+e.message));
   const screenshot=async(page,label)=>{assert(!await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),'Page overflow');await page.screenshot({path:'.runtime/screens/'+device+'-'+label+'.png',fullPage:true});results.push(device+' '+label);};
   // Register, verify pending status, and approve through the actual Super Admin screen.
   const username='browser_pending_'+device+'_'+Date.now();
   await a.goto(base+'/sims/register.php');await a.locator('#full_name').fill('Browser approval check');await a.locator('#username').fill(username);await a.locator('#email').fill(username+'@example.test');await a.locator('#password').fill('BrowserApproval123!');await a.locator('#confirm_password').fill('BrowserApproval123!');
   await a.getByRole('button',{name:'Submit for approval'}).click();await a.getByText('Wait for Super Admin approval before signing in.',{exact:false}).waitFor();
   await owner.goto(base+'/sims/modules/admin/manage.php?entity=users');const row=owner.getByRole('row').filter({hasText:username});await row.getByRole('button',{name:'Approve',exact:true}).click();
   await login(a,username,'BrowserApproval123!');await screenshot(a,'approved-student');
   await ac.close();await bc.close();
   const ca=await browser.newContext({viewport}),cb=await browser.newContext({viewport});const pa=await ca.newPage(),pb=await cb.newPage();for(const p of [pa,pb])p.on('pageerror',e=>errors.push(device+': '+e.message));
   await login(pa,'2024-0002','student123');await login(pb,'PARENT001','parent123');
   const first=(await api(pa,'Slots?serviceType=Document%20Request')).slots;const other=new Set((await api(pb,'Slots?serviceType=Document%20Request')).slots.map(s=>s.startsAt));const common=first.filter(s=>other.has(s.startsAt));assert(common.length>5,'Need compatible test times');
   const book=async(page,at,ref)=>api(page,'Book',{serviceType:'Document Request',requestType:'Other Document',studentNumber:ref,phoneNumber:'09121110001',startsAt:at,idempotencyKey:require('crypto').randomUUID(),confirmed:true}).then(r=>r.booking);
   const ta=await book(pa,common[0].startsAt,'2024-0002'),tb=await book(pb,common[4].startsAt,'2024-0001');
   let changes=0,offers=0,accepts=0;pa.on('request',r=>{if(r.method()==='POST'&&r.url().endsWith('/Assistant/Reschedule'))changes++;if(r.method()==='POST'&&r.url().endsWith('/Assistant/RequestExchange'))offers++;});pb.on('request',r=>{if(r.method()==='POST'&&r.url().endsWith('/Assistant/RespondExchange'))accepts++;});
   await pa.getByRole('button',{name:'Open ICAS Assistant'}).click();await pa.getByRole('button',{name:'My requests',exact:true}).click();await pa.getByRole('button',{name:'Change visit time',exact:true}).last().click();await pa.locator('.assistant-changes .assistant-slot').first().click();await pa.getByRole('button',{name:'Confirm time change',exact:true}).waitFor();assert(changes===0,'No reschedule before confirmation');await screenshot(pa,'reschedule-review');
   await pa.getByRole('button',{name:'Confirm time change',exact:true}).click();await pa.getByText('Your visit was rescheduled. The old time has been released.',{exact:true}).waitFor();assert(changes===1,'Exactly one confirmed reschedule');
   await pa.getByRole('button',{name:'Request time exchange',exact:true}).last().click();await pa.locator('.assistant-changes .assistant-slot').first().click();await pa.getByRole('button',{name:'Send exchange invitation',exact:true}).waitFor();assert(offers===0,'No invitation before confirmation');await screenshot(pa,'exchange-offer-review');await pa.getByRole('button',{name:'Send exchange invitation',exact:true}).click();
   await pa.getByRole('button',{name:'Withdraw offer',exact:true}).waitFor();assert(offers===1,'One invitation expected');
   await pb.getByRole('button',{name:'Open ICAS Assistant'}).click();await pb.getByRole('button',{name:'Exchange requests',exact:true}).click();await pb.getByRole('button',{name:'Accept exchange',exact:true}).waitFor();await screenshot(pb,'exchange-invitation');await pb.getByRole('button',{name:'Accept exchange',exact:true}).click();await pb.getByRole('button',{name:'Confirm accept',exact:true}).waitFor();assert(accepts===0,'No swap before recipient confirms');await screenshot(pb,'exchange-accept-review');await pb.getByRole('button',{name:'Confirm accept',exact:true}).click();await pb.getByText('Exchange confirmed. Both visit times were updated together.',{exact:true}).waitFor();assert(accepts===1,'One swap expected');
   const current=(await api(pa,'Bookings')).bookings.find(t=>t.ticketNumber===ta.ticketNumber);assert(current.scheduledAt===tb.scheduledAt,'Requester did not receive target time');
   await api(pa,'Cancel',{ticketNumber:ta.ticketNumber});await api(pb,'Cancel',{ticketNumber:tb.ticketNumber});await ca.close();await cb.close();
  }
 }finally{
  await owner.goto(base+'/sims/modules/admin/restore.php');const csrf=await owner.locator('input[name=csrf]').first().inputValue();const response=await owner.request.post(base+'/sims/modules/admin/restore.php',{multipart:{csrf,confirm_restore:'RESTORE',backup:{name:'backup.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(original))}}});assert(response.url().includes('restored=1'),'Test fixture restore failed');await browser.close();
 }
 console.log(JSON.stringify({results,errors},null,2));if(errors.length)process.exit(1);
})().catch(e=>{console.error(e);process.exit(1)});

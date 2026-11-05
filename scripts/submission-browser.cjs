const {chromium}=require(process.env.CAMPUS_PLAYWRIGHT_MODULE || 'playwright');
const assert=require('node:assert/strict');
if(process.env.CAMPUS_ALLOW_DESTRUCTIVE_TESTS!=='1')throw Error('Use disposable data and CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1; this suite restores a backup.');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CAMPUS_CHROMIUM||'/usr/bin/chromium',args:['--no-sandbox']});
 const base=process.env.CAMPUS_TEST_URL||'http://127.0.0.1:8080';
 let archive,owner;
 const errors=[];
 async function login(page,user,password){
  await page.goto(base+'/sims/login.php');if(new URL(page.url()).pathname==='/portal')return;await page.getByLabel('Username / student number').fill(user);await page.getByLabel('Password',{exact:true}).fill(password);
  await Promise.all([page.waitForURL('**/portal'),page.getByRole('button',{name:/Sign in/}).click()]);
 }
 async function form(page,path,values){
  const ctx=await (await page.request.get(base+'/Assistant/Context')).json();
  const encoded=new URLSearchParams();for(const [key,value] of Object.entries(values)){for(const entry of Array.isArray(value)?value:[value])encoded.append(key,String(entry));}
  const response=await page.request.post(base+path,{data:encoded.toString(),headers:{'Content-Type':'application/x-www-form-urlencoded',RequestVerificationToken:ctx.token}});assert.equal(response.status(),200);return response;
 }
 try {
  owner=await browser.newPage();await login(owner,'ADMIN','ADMIN');archive=await (await owner.request.get(base+'/sims/modules/admin/backup.php?download=1')).body();
  for(const viewport of [{width:1440,height:1000},{width:390,height:844}]){
   await form(owner,'/Services/SaveRequirement',{ServiceType:'Document Request',RequestType:'Other Document',Title:'QA private scan '+viewport.width,Required:'true',Active:'true'});
   const context=await browser.newContext({viewport});const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
   await login(page,'2024-0002','student123');
   const response=await form(page,'/Queue/GetTicket',{ServiceType:'Document Request',RequestType:'Other Document',PhoneNumber:'09121110002',StudentNumber:'2024-0002',PriorityType:'Regular'});
   const number=new URL(response.url()).searchParams.get('id')||new URL(response.url()).pathname.split('/').pop();
   const path='/Services/Request?id='+encodeURIComponent(number);
   await page.goto(base+path);
   const item=page.locator('.submission-item').first();
   await item.locator('input[type=file]').setInputFiles({name:'oversize.pdf',mimeType:'application/pdf',buffer:Buffer.alloc(2*1024*1024+1)});
   await item.getByRole('button',{name:'Submit privately'}).click();
   assert.equal(await item.locator('input[type=file]').evaluate(input=>input.validationMessage),'Choose a file of up to 2 MB.');
   assert.equal(await item.getByRole('button',{name:'Submit privately'}).evaluate(button=>button.classList.contains('is-loading')),false);

   await item.locator('input[type=file]').setInputFiles({name:'scan.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n%%EOF\n')});
   await Promise.all([page.waitForNavigation({waitUntil:'domcontentloaded'}),item.getByRole('button',{name:'Submit privately'}).click()]);
   await page.locator('.submission-item').first().getByText('Pending review',{exact:true}).waitFor();
   await owner.goto(base+path);
   const office=owner.locator('.submission-item').first();
   await office.locator('[data-review-decision]').selectOption('return');
   assert.equal(await office.locator('[data-review-note]').getAttribute('required'),'');
   await office.locator('[data-review-note]').fill('Please upload the complete page.');
   await Promise.all([owner.waitForNavigation({waitUntil:'domcontentloaded'}),office.getByRole('button',{name:'Save review'}).click()]);
   await page.reload();await page.getByText('Please upload the complete page.',{exact:true}).waitFor();
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false,'Mobile upload panel must fit');
   const returned=page.locator('.submission-item').first();
   await returned.locator('input[type=file]').setInputFiles({name:'corrected.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n% corrected\n%%EOF\n')});
   await Promise.all([page.waitForNavigation({waitUntil:'domcontentloaded'}),returned.getByRole('button',{name:'Submit new version'}).click()]);
   await page.getByText('Previous submissions (1)',{exact:true}).waitFor();
   await owner.goto(base+path);await owner.locator('.submission-item').first().locator('[data-review-decision]').selectOption('accept');
   await Promise.all([owner.waitForNavigation({waitUntil:'domcontentloaded'}),owner.locator('.submission-item').first().getByRole('button',{name:'Save review'}).click()]);
   await page.reload();await page.locator('.submission-item').first().getByText('Accepted',{exact:true}).waitFor();
   const [download]=await Promise.all([page.waitForEvent('download'),page.locator('.submission-download').first().click()]);assert.equal(download.suggestedFilename(),'corrected.pdf');
   await page.evaluate(()=>navigator.serviceWorker.ready);
   assert.ok(await page.evaluate(async()=>{for(const name of await caches.keys()){for(const request of await (await caches.open(name)).keys()){if(new URL(request.url).pathname.startsWith('/Services/'))return false;}}return true;}));
   // Release before creating the next viewport's request; required items may also be checked in person.
   const backup=await (await owner.request.get(base+'/sims/modules/admin/backup.php?download=1')).json();const ticket=backup.queue_data.Tickets.find(t=>t.TicketNumber===number);
   await form(owner,'/Services/Verify',{id:number,verified:ticket.Checklist.map(c=>c.RequirementId)});
   await form(owner,'/Services/Progress',{id:number,stage:'Processing',note:'QA review'});
   await form(owner,'/Services/Progress',{id:number,stage:'Ready for pickup',note:'QA ready'});
   await form(owner,'/Services/Progress',{id:number,stage:'Released',note:'QA recipient verified in person'});
   await context.close();console.log(`PASS ${viewport.width}px: upload, return reason, corrected resubmission, acceptance, download, and no offline file cache`);
  }
  assert.deepEqual(errors,[]);
 } finally {
  try {
   if(archive&&owner){await login(owner,'ADMIN','ADMIN');const response=await owner.request.get(base+'/sims/modules/admin/restore.php');const text=await response.text();const token=text.match(/name="csrf" value="([^"]+)"/)[1];const restored=await owner.request.post(base+'/sims/modules/admin/restore.php',{multipart:{csrf:token,confirm_restore:'RESTORE',backup:{name:'backup.json',mimeType:'application/json',buffer:archive}}});assert.ok(restored.url().includes('restored=1'));console.log('RESTORED original disposable workspace');}
  }finally{await browser.close();}
 }
})().catch(e=>{console.error(e);process.exitCode=1;});

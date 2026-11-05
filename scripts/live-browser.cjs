const {chromium}=require(process.env.CAMPUS_PLAYWRIGHT_MODULE || 'playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CAMPUS_CHROMIUM||'/usr/bin/chromium',args:['--no-sandbox']});
 try {
  const base=process.env.CAMPUS_TEST_URL||'http://127.0.0.1:8080';
  for(const viewport of [{width:1440,height:1000},{width:390,height:844}]){
   const context=await browser.newContext({viewport});const page=await context.newPage();const errors=[];
   page.on('pageerror',e=>errors.push(e.message));
   let updates=0;
   await page.route('**/Home/LiveQueue',async route=>{
    const response=await route.fetch();const data=await response.json();
    if(++updates>1)data.serving=[{ticketNumber:'LIVE-CHECK',office:'Registrar',counterName:'Test counter',serviceType:'Document Request',requestType:'Live update',predictedCounterName:''}];
    await route.fulfill({response,json:data});
   });
   await page.goto(base+'/Home/Display');
   const initialPermission=await page.evaluate(()=>Notification.permission);
   let navigations=0;page.on('framenavigated',frame=>{if(frame===page.mainFrame())navigations++;});
   await page.waitForFunction(()=>document.querySelector('[data-live-status]').textContent.startsWith('Live ·'));
   await page.getByText('LIVE-CHECK',{exact:true}).waitFor();assert.equal(navigations,0,'Live display must not reload the page');
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false,'Display overflow');
   await page.goto(base+'/sims/login.php');
   await page.getByLabel('Username / student number').fill('2024-0002');
   await page.getByLabel('Password',{exact:true}).fill('student123');
   await Promise.all([page.waitForURL('**/portal'),page.getByRole('button',{name:/Sign in/}).click()]);
   await page.goto(base+'/Queue/MyTicket');
   await page.waitForFunction(()=>document.querySelector('[data-live-status]')?.textContent.startsWith('Live ·'));
   await page.locator('a[href^="/Services/Request?id="]').first().click();
   await page.waitForFunction(()=>document.querySelector('[data-request-live-status]').textContent.startsWith('Live ·'));
   assert.ok(await page.getByRole('heading',{name:'Your next step'}).isVisible());
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false,'Request overflow');
   await page.goto(base+'/Services/Requests');
   await page.locator('#request-search').fill('nonexistent-query-123');
   assert.equal(await page.locator('.ops-request-row:visible').count(),0);
   await page.locator('#request-search').fill('');
   assert.ok(await page.locator('.ops-request-row:visible').count()>0);
   await page.evaluate(()=>navigator.serviceWorker.ready);
   const manifest=await (await page.request.get(base+'/manifest.webmanifest')).json();
   for(const icon of manifest.icons){const response=await page.request.get(base+icon.src);assert.equal(response.status(),200);assert.ok(response.headers()['content-type'].includes('image/png'));}
   assert.equal(await page.evaluate(()=>Notification.permission),initialPermission,'No automatic permission prompt');
   await context.grantPermissions(['notifications']);
   await page.evaluate(()=>window.dispatchEvent(new Event('campus:notification')));
   await page.waitForFunction(async()=>{const worker=await navigator.serviceWorker.ready;const notices=await worker.getNotifications();return notices.some(n=>n.title==='ICAS Campus update'&&n.body==='You have a new campus service update. Open the portal to view it.');});
   const keys=await page.evaluate(async()=>{const names=await caches.keys();const result=[];for(const n of names){const c=await caches.open(n);for(const req of await c.keys())result.push(new URL(req.url).pathname);}return result.sort();});
   assert.deepEqual(keys,['/offline.html','/pwa/icon.svg']);
   await context.setOffline(true);await page.goto(base+'/sims/dashboard.php');
   await page.getByRole('heading',{name:"You're offline"}).waitFor();
   assert.equal(await page.locator('.sidebar').count(),0,'No cached private dashboard');
   await context.setOffline(false);await page.goto(base+'/Services/Requests');
   assert.ok(await page.getByRole('heading',{name:'My service requests'}).isVisible());
   assert.deepEqual(errors,[]);console.log(`PASS ${viewport.width}px: live updates without reload, requests, search, public-only cache, offline recovery`);
   await context.close();
  }
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});

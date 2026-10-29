const {chromium}=require(process.env.CAMPUS_PLAYWRIGHT_MODULE||'playwright');
const base=process.env.CAMPUS_TEST_URL||'http://127.0.0.1:18080';
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CAMPUS_CHROMIUM||undefined,args:['--no-sandbox']});const errors=[],results=[];
 for(const [name,viewport] of [['desktop',{width:1440,height:1000}],['mobile',{width:390,height:844}]]){
  const context=await browser.newContext({viewport});const page=await context.newPage();let bookCalls=0,chatCalls=0;
  page.on('pageerror',e=>errors.push(name+': '+e.message));page.on('request',r=>{if(r.url().endsWith('/Assistant/Book')&&r.method()==='POST')bookCalls++;if(r.url().endsWith('/Assistant/Chat')&&r.method()==='POST')chatCalls++;});
  const capture=async label=>{const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);if(overflow)throw Error(name+' '+label+' page overflow');await page.screenshot({path:'.runtime/screens/'+name+'-'+label+'.png',fullPage:true});results.push({name,label,overflow});};
  await page.goto(base+'/sims/login.php',{waitUntil:'networkidle'});
  const text=await page.locator('body').innerText();for(const value of ['student123','teacher123','parent123','ADMIN / ADMIN'])if(text.includes(value))throw Error('Credential hint still visible');
  if(await page.locator('#password').getAttribute('type')!=='password')throw Error('Password must remain masked');
  await capture('clean-login');
  await page.getByRole('button',{name:'Open ICAS Assistant'}).click();
  await page.getByLabel('Your school question').fill('How do I request school documents?');await page.getByRole('button',{name:'Send question'}).click();
  await page.locator('.assistant-mode').filter({hasText:'School guide'}).waitFor();
  const previous=chatCalls;await page.getByLabel('Your school question').fill('my password is private-browser-fixture');await page.getByRole('button',{name:'Send question'}).click();
  await page.getByText('Please do not share passwords or access keys here.',{exact:false}).waitFor();if(chatCalls!==previous||await page.locator('.assistant-messages').innerText().then(t=>t.includes('private-browser-fixture')))throw Error('Secret-like text was forwarded or displayed');
  await capture('school-guide');
  await page.getByRole('button',{name:'Close assistant'}).click();
  await page.getByLabel('Username / student number').fill('2024-0002');await page.getByLabel('Password',{exact:true}).fill('student123');
  await Promise.all([page.waitForURL('**/portal'),page.getByRole('button',{name:/Sign in/}).click()]);await page.waitForLoadState('networkidle');await capture('clean-overview');
  await page.getByRole('button',{name:'Plan a campus visit',exact:true}).click();const form=page.locator('.assistant-booking-form');await form.waitFor();
  await form.getByLabel('Transaction',{exact:true}).selectOption('Good Moral Certificate');await form.getByLabel('Contact number').fill('09121110002');await form.getByLabel('Office notes (optional)').fill('Browser QA visit');
  await form.getByRole('button',{name:'Find available times'}).click();await page.locator('.assistant-slot').first().waitFor();
  if(await page.locator('.assistant-slot').count()!==3)throw Error('Only three initial suggestions should be shown');
  await page.locator('.assistant-slot').first().click();await page.getByRole('button',{name:'Confirm this visit'}).waitFor();
  if(bookCalls!==0)throw Error('A visit was booked before confirmation');
  const bounds=await page.locator('.assistant-dialog').boundingBox();if(bounds.x<0||bounds.x+bounds.width>viewport.width+1||bounds.y<0||bounds.y+bounds.height>viewport.height+1)throw Error('Assistant dialog leaves viewport');
  await capture('visit-review');await page.getByRole('button',{name:'Confirm this visit'}).click();await page.getByRole('heading',{name:'Your visit is reserved'}).waitFor();
  if(bookCalls!==1)throw Error('Confirmation should issue one booking request');await capture('visit-confirmed');
  page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name:'Cancel this request',exact:true}).last().click();await page.getByText('Your request was cancelled and its reservation was released.',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Close assistant'}).click();await context.close();
 }
 await browser.close();console.log(JSON.stringify({results,errors},null,2));if(errors.length)process.exit(1);
})().catch(error=>{console.error(error);process.exit(1)});

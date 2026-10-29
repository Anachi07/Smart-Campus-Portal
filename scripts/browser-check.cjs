const {chromium}=require(process.env.CAMPUS_PLAYWRIGHT_MODULE || 'playwright');
(async()=>{
const browser=await chromium.launch({headless:true,executablePath:process.env.CAMPUS_CHROMIUM || undefined,args:['--no-sandbox']});
const errors=[];const results=[];
for(const [name,viewport] of [['desktop',{width:1440,height:1000}],['mobile',{width:390,height:844}]]){
 const context=await browser.newContext({viewport});const page=await context.newPage();
 page.on('pageerror',e=>errors.push(`${name}: ${e.message}`));
 page.on('response',r=>{if(r.status()>=400)errors.push(`${name}: ${r.status()} ${r.url()}`);});
 const check=async(label,path)=>{await page.goto((process.env.CAMPUS_TEST_URL || 'http://127.0.0.1:18080')+path,{waitUntil:'networkidle'});const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);results.push({name,label,overflow});if(overflow)throw Error(`Horizontal page overflow: ${name} ${label}`);await page.screenshot({path:`.runtime/screens/${name}-${label}.png`,fullPage:true});};
 await check('home','/');await check('login','/sims/login.php');
 await page.getByLabel('Username / student number').fill('ADMIN');await page.getByLabel('Password',{exact:true}).fill('ADMIN');await Promise.all([page.waitForURL('**/portal'),page.getByRole('button',{name:/Sign in/}).click()]);
 await check('portal','/portal');
 if(name==='mobile'){await page.getByRole('button',{name:'Open navigation'}).click();if(!await page.locator('#sidebar').evaluate(e=>e.classList.contains('show')))throw Error('Mobile sidebar did not open');await page.keyboard.press('Escape');}
 await check('dashboard','/sims/dashboard.php');await check('records','/sims/modules/admin/manage.php?entity=users');await check('calendar','/sims/modules/shared/calendar.php');await check('queue','/Admin/Dashboard');await check('public-queue','/Home/Display');
 await context.close();
 const studentContext=await browser.newContext({viewport});const studentPage=await studentContext.newPage();
 studentPage.on('pageerror',e=>errors.push(`${name} student: ${e.message}`));
 await studentPage.goto((process.env.CAMPUS_TEST_URL||'http://127.0.0.1:18080')+'/sims/login.php');
 await studentPage.getByLabel('Username / student number').fill('2024-0002');await studentPage.getByLabel('Password',{exact:true}).fill('student123');
 await Promise.all([studentPage.waitForURL('**/portal'),studentPage.getByRole('button',{name:/Sign in/}).click()]);
 await studentPage.goto((process.env.CAMPUS_TEST_URL||'http://127.0.0.1:18080')+'/Queue/GetTicket',{waitUntil:'networkidle'});
 const studentOverflow=await studentPage.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);
 if(studentOverflow)throw Error('Student service form overflows '+name);results.push({name,label:'student-services',overflow:studentOverflow});
 await studentPage.screenshot({path:`.runtime/screens/${name}-student-services.png`,fullPage:true});
 await studentPage.getByRole('radio',{name:/School fees/}).check();
 if(await studentPage.locator('#StudentNumber').evaluate(e=>e.required))throw Error('School fees student reference must be optional');
 if(await studentPage.locator('#requestOffice').textContent()!=='Cashier')throw Error('School fees office mismatch');
 await studentPage.getByRole('radio',{name:/Tuition/}).check();
 if(!await studentPage.locator('#StudentNumber').evaluate(e=>e.required))throw Error('Tuition needs a student number');
 if(!await studentPage.locator('#RequestType').getByRole('option',{name:'Current Tuition Assessment'}).count())throw Error('Tuition types missing');
 await studentPage.getByRole('radio',{name:/Documents/}).check();
 if(await studentPage.locator('#requestOffice').textContent()!=='Registrar')throw Error('Document office mismatch');
 await studentContext.close();
}
console.log(JSON.stringify({results,errors},null,2));await browser.close();if(errors.length)process.exit(1);
})().catch(e=>{console.error(e);process.exit(1)});

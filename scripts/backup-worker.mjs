import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const php=process.env.CAMPUS_BACKUP_PHP || process.env.PHP_BIN || 'php';
const args=JSON.parse(process.env.CAMPUS_BACKUP_PHP_ARGS || '[]');
const raw=Number(process.env.CAMPUS_BACKUP_INTERVAL_MINUTES || 1440);
const minutes=Number.isFinite(raw)?Math.max(5,Math.min(43200,raw)):1440;
let child,closing=false,timer;
function run(){if(child||closing)return;child=spawn(php,[...args,'scripts/backup.php'],{cwd:root,env:process.env,stdio:['ignore','inherit','inherit']});child.on('error',()=>{console.error('Scheduled backup could not start. Check the PHP runtime and private backup directory.');});child.on('close',code=>{child=null;if(process.argv.includes('--once'))process.exitCode=code??1;else if(!closing)timer=setTimeout(run,minutes*60000);});}
if(process.argv.includes('--once'))run();else timer=setTimeout(run,60000);
for(const signal of ['SIGTERM','SIGINT'])process.on(signal,()=>{closing=true;clearTimeout(timer);if(child)child.kill('SIGTERM');else process.exit(0);});

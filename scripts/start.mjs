import {spawn, spawnSync} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {existsSync, mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const localPhp = '/workspace/tools/php';
const php = process.env.PHP_BIN || (existsSync(`${localPhp}/usr/bin/php8.4`) ? `${localPhp}/usr/bin/php8.4` : 'php');
const dotnet = process.env.DOTNET_BIN || (existsSync('/workspace/tools/dotnet/dotnet') ? '/workspace/tools/dotnet/dotnet' : 'dotnet');
const env = {...process.env, CAMPUS_INTERNAL_SECRET: randomBytes(32).toString('hex'), ASPNETCORE_ENVIRONMENT: 'Development'};
mkdirSync(path.join(root, '.runtime/sessions'), {recursive: true});
let phpArgs = [];
if (php.startsWith(localPhp)) {
 env.LD_LIBRARY_PATH = `${localPhp}/usr/lib/x86_64-linux-gnu${env.LD_LIBRARY_PATH ? ':'+env.LD_LIBRARY_PATH : ''}`;
 const ext = `${localPhp}/usr/lib/php/20240924`;
 phpArgs = ['-n', '-d', `extension=${ext}/pdo.so`, '-d', `extension=${ext}/pdo_sqlite.so`, '-d', `extension=${ext}/sqlite3.so`];
}
phpArgs.push('-d', `session.save_path=${root}/.runtime/sessions`);
const build = spawnSync(dotnet, ['build', 'queue/SmartQueueSystemGUI.csproj', '--no-restore'], {cwd:root, env, stdio:'inherit'});
if (build.status !== 0) process.exit(build.status || 1);
const children = [];
let closing = false;
function close(code = 0) { if (closing) return; closing = true; for (const child of children) child.kill('SIGTERM'); setTimeout(() => process.exit(code), 500); }
function launch(command, args, cwd) {
 const child = spawn(command, args, {cwd, env, stdio: 'inherit'}); children.push(child);
 child.on('error', error => {console.error(error.message); close(1);});
 child.on('exit', code => { if (!closing) close(code || 1); });
}
launch(php, [...phpArgs, '-S', '127.0.0.1:8082', '-t', 'sims', 'scripts/php-router.php'], root);
launch(dotnet, ['bin/Debug/net8.0/SmartQueueSystemGUI.dll', '--urls', 'http://127.0.0.1:8081'], path.join(root,'queue'));
launch(process.execPath, ['scripts/gateway.mjs'], root);
process.on('SIGINT', () => close()); process.on('SIGTERM', () => close());

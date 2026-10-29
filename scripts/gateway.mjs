import http from 'node:http';
const port = Number(process.env.PORT || 8080);
const server = http.createServer((req, res) => {
  const path = new URL(req.url, 'http://campus.local').pathname;
  if (path === '/') { res.writeHead(302, {Location: '/portal'}); res.end(); return; }
  const sims = path.startsWith('/sims/') || path === '/portal';
  if (path === '/sims') { res.writeHead(302, {Location: '/sims/'}); res.end(); return; }
  const headers = {...req.headers};
  headers['x-forwarded-proto'] = process.env.CAMPUS_PUBLIC_HTTPS === '1' ? 'https' : 'http';
  delete headers['x-campus-secret']; // Never trust an external caller's internal credential.
  const upstream = http.request({hostname: '127.0.0.1', port: sims ? 8082 : 8081,
    method: req.method, path: path === '/portal' ? '/portal.php' : sims ? req.url.slice(5) : req.url,
    headers}, response => {
      const responseHeaders = {...response.headers, 'cache-control': 'no-store'};
      res.writeHead(response.statusCode, responseHeaders); response.pipe(res);
    });
  upstream.on('error', () => { if (!res.headersSent) res.writeHead(503, {'Content-Type': 'text/plain'}); res.end('Campus service is starting or unavailable.'); });
  req.on('aborted', () => upstream.destroy());
  req.pipe(upstream);
});
server.listen(port, process.env.HOST || '127.0.0.1', () => console.log(`Campus gateway listening on port ${port}`));

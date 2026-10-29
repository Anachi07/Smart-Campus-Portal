import http from 'node:http';
const port = Number(process.env.PORT || 8080);
const maxBody = 12 * 1024 * 1024;
const server = http.createServer((req, res) => {
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Referrer-Policy', 'same-origin');
  res.setHeader('X-Frame-Options', 'DENY');
  res.setHeader('Permissions-Policy', 'camera=(self), microphone=(), geolocation=()');
  res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; media-src 'self' blob:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'");
  let path;
  try { path = new URL(req.url, 'http://campus.local').pathname; } catch { res.writeHead(400); res.end('Invalid request.'); return; }
  if (path !== '/') res.setHeader('X-Robots-Tag', 'noindex, nofollow');
  if (path.startsWith('/internal/')) { res.writeHead(404); res.end(); return; }
  if (path === '/robots.txt') { res.writeHead(200, {'Content-Type':'text/plain'}); res.end('User-agent: *\nAllow: /$\nDisallow: /portal\nDisallow: /sims/\nDisallow: /Admin/\nDisallow: /Queue/\n'); return; }
  if (Number(req.headers['content-length'] || 0) > maxBody) { res.writeHead(413); res.end('Upload limit is 12 MB.'); return; }
  const sims = path === '/' || path.startsWith('/sims/') || path === '/portal';
  if (path === '/sims') { res.writeHead(302, {Location: '/sims/'}); res.end(); return; }
  const headers = {...req.headers};
  headers['x-forwarded-proto'] = process.env.CAMPUS_PUBLIC_HTTPS === '1' ? 'https' : 'http';
  delete headers['x-campus-secret'];
  const upstreamPath = path === '/' ? '/index.php' : path === '/portal' ? '/portal.php' : sims ? req.url.slice(5) : req.url;
  const upstream = http.request({hostname:'127.0.0.1',port:sims?8082:8081,method:req.method,path:upstreamPath,headers}, response => {
    const responseHeaders={...response.headers, 'cache-control':'no-store'};
    delete responseHeaders['x-powered-by']; delete responseHeaders.server;
    res.writeHead(response.statusCode,responseHeaders); response.pipe(res);
  });
  upstream.setTimeout(30000,()=>upstream.destroy(new Error('Service timeout')));
  upstream.on('error',()=>{ if(!res.headersSent) res.writeHead(503,{'Content-Type':'text/plain'}); if(!res.writableEnded) res.end('Campus service is temporarily unavailable. Please try again shortly.'); });
  let bytes=0;
  req.on('data',chunk=>{ bytes+=chunk.length; if(bytes>maxBody) { if(!res.headersSent) res.writeHead(413); res.end('Upload limit is 12 MB.'); upstream.destroy(); } });
  req.on('aborted',()=>upstream.destroy()); res.on('close',()=>{ if(!res.writableEnded) upstream.destroy(); });
  req.pipe(upstream);
});
server.headersTimeout=15000; server.requestTimeout=60000;
server.listen(port,process.env.HOST||'127.0.0.1',()=>console.log(`Campus gateway listening on port ${port}`));
process.on('SIGTERM',()=>server.close(()=>process.exit(0)));

/**
 * strict-mock-server.js — Server lifecycle + web endpoints for
 * StrictMockProvider. Kept separate so the main class file
 * stays within the 300-line Kolmogorov line budget.
 */

import http from 'node:http';
import { sendJSON } from './strict-mock-sse.js';

const READY_TIMEOUT_MS = 30000;

export function startHttpServer(handler) {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => handler(req, res));
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const addr = server.address();
      resolve({ server, port: addr.port, url: `http://127.0.0.1:${addr.port}` });
    });
  });
}

export function stopHttpServer(server) {
  if (!server) return Promise.resolve();
  return new Promise((resolve) => {
    server.close(() => resolve());
  });
}

export function handleWebSearch(req, res) {
  readRequestBody(req).then((body) => {
    const query = body.query || '';
    if (query.includes('trigger_500')) {
      res.writeHead(500, { 'Content-Type': 'text/plain' });
      res.end('Internal Server Error');
    } else if (query.includes('trigger_malformed')) {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end('{ malformed json');
    } else {
      sendJSON(res, 200, {
        results: [{ title: 'Test Search Title', url: 'http://example.com', content: 'Test search content for E2E.' }],
      });
    }
  }).catch(() => {
    sendJSON(res, 400, { error: 'bad request' });
  });
}

export function handleWebFetch(req, res) {
  readRequestBody(req).then((body) => {
    const url = body.url || '';
    if (url.includes('to-private') || url.includes('redirect-private')) {
      res.writeHead(400, { 'Content-Type': 'text/plain' });
      res.end('redirect to private IP blocked');
    } else if (url.includes('trigger_500')) {
      res.writeHead(500, { 'Content-Type': 'text/plain' });
      res.end('Internal Server Error');
    } else if (url.includes('trigger_malformed')) {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end('{ malformed json');
    } else {
      sendJSON(res, 200, {
        title: 'Example Domain',
        byline: 'IANA',
        length: 500,
        content: 'Example Domain\n\nThis domain is for use in documentation examples.',
      });
    }
  }).catch(() => {
    sendJSON(res, 400, { error: 'bad request' });
  });
}

export function readRequestBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.on('data', (chunk) => { raw += chunk; });
    req.on('end', () => {
      if (!raw) return resolve({});
      try { resolve(JSON.parse(raw)); } catch (e) { reject(e); }
    });
    req.on('error', reject);
  });
}

export { READY_TIMEOUT_MS };

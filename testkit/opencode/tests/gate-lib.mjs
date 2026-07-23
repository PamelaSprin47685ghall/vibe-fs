/**
 * gate-lib.mjs — Shared helpers for harness gate regression.
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';

export function assertEq(actual, expected, msg) {
  if (actual !== expected) {
    throw new Error(`${msg}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

export function assertTrue(v, msg) {
  if (!v) throw new Error(msg || 'assertion failed');
}

export function tmpScenarioDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'gate-'));
}

export async function startSseServer(events) {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      if (req.url !== '/event') {
        res.writeHead(404).end();
        return;
      }
      res.writeHead(200, {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection': 'keep-alive',
      });
      for (const ev of events) {
        res.write(`data: ${JSON.stringify(ev)}\n\n`);
      }
      res.end();
    });
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const port = server.address().port;
      resolve({
        url: `http://127.0.0.1:${port}`,
        close: () => new Promise((r) => {
          try { server.closeAllConnections(); } catch {}
          server.close(() => r());
        }),
      });
    });
  });
}

export async function postJson(url, body) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  return { status: res.status, ok: res.ok, text };
}

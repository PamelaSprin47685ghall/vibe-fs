/**
 * p0-canary-tests-recovery-helpers.js — Shared helpers for recovery E2E tests.
 */

import { TIMEOUTS, sleep } from './p0-canary-utils.js';
import fs from 'node:fs';

export async function waitForLoopCommand(client) {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    const res = await client.request('GET', '/command');
    if (res.ok && Array.isArray(res.data) && res.data.some(c => c.name === 'loop')) {
      return;
    }
    await sleep(200);
  }
  throw new Error('Timeout waiting for loop command to register');
}

export async function waitForAsyncCondition(read, predicate, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() <= deadline) {
    const val = await read();
    if (predicate(val)) return val;
    await sleep(200);
  }
  throw new Error(`Timed out waiting for async condition after ${timeoutMs}ms`);
}

export function validateNdjsonLine(line, index) {
  let obj;
  try {
    obj = JSON.parse(line);
  } catch (e) {
    throw new Error(`NDJSON line ${index + 1} is not valid JSON: ${line.slice(0, 200)}`);
  }
  if (typeof obj.V !== 'number') {
    throw new Error(`line ${index + 1}: V missing/invalid`);
  }
  if (!obj.Session || typeof obj.Session !== 'string') {
    throw new Error(`line ${index + 1}: Session missing/invalid`);
  }
  if (!obj.Kind || typeof obj.Kind !== 'string') {
    throw new Error(`line ${index + 1}: Kind missing/invalid`);
  }
  if (!obj.At || typeof obj.At !== 'string') {
    throw new Error(`line ${index + 1}: At (event ID) missing/invalid`);
  }
  if (typeof obj.Payload !== 'object' || obj.Payload === null) {
    throw new Error(`line ${index + 1}: Payload not object`);
  }
  return obj;
}

export function validateNdjson(workDir, allowedSessions) {
  const p = workDir + '/.wanxiangshu.ndjson';
  if (!fs.existsSync(p)) throw new Error('NDJSON file missing');
  const raw = fs.readFileSync(p, 'utf8');
  const lines = raw.split('\n');
  const seenLines = new Set();
  const events = [];
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (!line.trim()) continue;
    if (seenLines.has(line)) {
      console.log('NDJSON DUPLICATE FOUND: ' + line);
      console.log('FULL NDJSON:\n' + raw);
      throw new Error(`line ${i + 1}: duplicate NDJSON line`);
    }
    seenLines.add(line);
    const ev = validateNdjsonLine(line, i);
    events.push(ev);
  }
  if (allowedSessions) {
    for (const ev of events) {
      if (!allowedSessions.includes(ev.Session)) {
        throw new Error(`NDJSON event with unexpected session ${ev.Session}`);
      }
    }
  }
  return events;
}

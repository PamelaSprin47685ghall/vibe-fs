/**
 * p0-canary-utils.js — Shared helpers for P0 canary tests.
 * Kept in its own module so p0-canary.js stays under the 300-line
 * Kolmogorov line budget.
 */

import fs from 'node:fs';

const NEWLINE = Buffer.from([0x0a]);
export const content = (s) => s + NEWLINE.toString();

export function writeWorkFile(workDir, rel, body) {
  const abs = workDir + '/' + rel;
  fs.writeFileSync(abs, body, 'utf8');
  return abs;
}

export function findPtySessionId(reqBody) {
  const allText = JSON.stringify(reqBody.messages || []);
  const candidates = allText.match(/pty_[a-zA-Z0-9]+/g) || [];
  const toolNames = new Set(['pty_spawn', 'pty_kill', 'pty_list', 'pty_read', 'pty_write']);
  const real = candidates.find((id) => !toolNames.has(id));
  return real || 'unknown-pty';
}

export function extractToolNames(tools) {
  return (tools || []).map((t) => t.function?.name || t.name);
}

export async function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

export const TIMEOUTS = {
  prompt: 60000,
  quick: 30000,
  long: 90000,
  idleNudge: 5000,
  postNudgeObserve: 3000,
  ptyPostObserve: 200,
};

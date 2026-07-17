/**
 * process-host-utils.js — Pure helpers for ProcessHost: child lifecycle,
 * socket/PID/process-tree checks, listen-port parsing.
 *
 * Side-effect-free functions live here so the main class file stays under
 * the 200-line Kolmogorov line budget.
 */

import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const STDOUT_RING_MAX = 100;

export const SIGTERM_GRACE_MS = 5000;
export const SIGKILL_GRACE_MS = 1000;
export const READY_POLL_INTERVAL_MS = 100;
export const READY_POLL_MAX_TRIES = 50;
export const PROCESS_TREE_TIMEOUT_MS = 2000;

export function parseListenPort(listenLine) {
  const m = listenLine.match(/http:\/\/127\.0\.0\.1:(\d+)/)
    || listenLine.match(/http:\/\/localhost:(\d+)/)
    || listenLine.match(/:(\d+)/);
  return m ? Number(m[1]) : 0;
}

export function pidIsAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    if (err.code === 'ESRCH') return false;
    if (err.code === 'EPERM') return true;
    return false;
  }
}

export function ringPush(buffer, s) {
  buffer.push(s);
  if (buffer.length > STDOUT_RING_MAX) buffer.shift();
}

export async function terminateChild(child, termMs, killMs) {
  try { child.kill('SIGTERM'); } catch {}
  const exited = await new Promise((resolve) => {
    const timer = setTimeout(() => resolve(false), termMs);
    child.once('exit', () => { clearTimeout(timer); resolve(true); });
  });
  if (exited) return;
  try { child.kill('SIGKILL'); } catch {}
  await new Promise((resolve) => {
    const timer = setTimeout(resolve, killMs);
    child.once('exit', () => { clearTimeout(timer); resolve(); });
  });
}

export async function initGitWorkspace(workDir) {
  const gitDir = path.join(workDir, '.git');
  if (fs.existsSync(gitDir)) return;
  try {
    const { execSync } = await import('node:child_process');
    execSync('git init', { cwd: workDir, stdio: 'ignore' });
    execSync('git config user.email test@example.com', { cwd: workDir, stdio: 'ignore' });
    execSync('git config user.name test', { cwd: workDir, stdio: 'ignore' });
    fs.writeFileSync(path.join(workDir, 'AGENTS.md'), '- e2e workspace\n');
    execSync('git add -A', { cwd: workDir, stdio: 'ignore' });
    execSync('git commit -m init', { cwd: workDir, stdio: 'ignore' });
  } catch {
    // Non-fatal.
  }
}

export function spawnOpencodeServe(workDir, env, hooks) {
  const child = spawn(
    'opencode',
    ['serve', '--port', '0', '--hostname', '127.0.0.1'],
    {
      cwd: workDir,
      env,
      stdio: ['pipe', 'pipe', 'pipe'],
      windowsHide: true,
    },
  );
  child.stdout.on('data', (chunk) => hooks.onStdoutChunk(chunk.toString()));
  child.stderr.on('data', (chunk) => hooks.onStderrChunk(chunk.toString()));
  child.on('exit', (code, signal) => hooks.onExit(code, signal));
  return child;
}

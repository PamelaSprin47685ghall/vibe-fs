/**
 * process-host-checks.js — Resource leak checks: socket, PID, process tree.
 * Pure side-effect-free checkers, imported by the main class file.
 */

import net from 'node:net';
import { pidIsAlive } from './process-lifecycle.js';
import { PROCESS_TREE_TIMEOUT_MS, SOCKET_CHECK_TIMEOUT_MS } from './time-budget.js';

export const isPidAlive = pidIsAlive;

export async function checkSocketClosed(port, timeoutMs = SOCKET_CHECK_TIMEOUT_MS) {
  if (!port) return true;
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const closed = await new Promise((resolve) => {
      const socket = new net.Socket();
      socket.setTimeout(150);
      socket.on('connect', () => { socket.destroy(); resolve(false); });
      socket.on('error', () => { resolve(true); });
      socket.on('timeout', () => { socket.destroy(); resolve(false); });
      try { socket.connect(port, '127.0.0.1'); } catch { resolve(true); }
    });
    if (closed) return true;
    await new Promise((r) => setTimeout(r, 30));
  }
  return false;
}

export async function checkProcessTree(pid, timeoutMs = PROCESS_TREE_TIMEOUT_MS) {
  if (!pid) return '';
  try {
    const { execSync } = await import('node:child_process');
    const cmd = process.platform === 'linux'
      ? `ps --ppid ${pid} -o pid=,cmd= 2>/dev/null || true`
      : process.platform === 'darwin'
        ? `pgrep -P ${pid} 2>/dev/null || true`
        : `wmic process where (ParentProcessId=${pid}) get ProcessId 2>/dev/null || true`;
    return execSync(cmd, { timeout: timeoutMs }).toString().trim();
  } catch {
    return '';
  }
}

export async function getChildPids(pid) {
  if (!pid) return [];
  try {
    const { execSync } = await import('node:child_process');
    let cmd = '';
    if (process.platform === 'linux') {
      cmd = `ps --ppid ${pid} -o pid= 2>/dev/null || true`;
    } else if (process.platform === 'darwin') {
      cmd = `pgrep -P ${pid} 2>/dev/null || true`;
    } else {
      cmd = `wmic process where (ParentProcessId=${pid}) get ProcessId 2>/dev/null || true`;
    }
    const output = execSync(cmd).toString().trim();
    return output
      .split(/\r?\n/)
      .map((p) => p.trim())
      .filter((p) => p && !p.toLowerCase().includes('processid'))
      .map((p) => parseInt(p, 10))
      .filter((p) => !isNaN(p) && p > 0);
  } catch {
    return [];
  }
}

export async function getDescendantPids(pid) {
  const result = [];
  const queue = [pid];
  while (queue.length > 0) {
    const current = queue.shift();
    const children = await getChildPids(current);
    for (const child of children) {
      if (!result.includes(child)) {
        result.push(child);
        queue.push(child);
      }
    }
  }
  return result;
}

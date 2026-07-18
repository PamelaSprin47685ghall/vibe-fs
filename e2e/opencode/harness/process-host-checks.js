/**
 * process-host-checks.js — Resource leak checks: socket, PID, process tree.
 * Pure side-effect-free checkers; main class file imports these so it
 * stays within the 200-line Kolmogorov line budget.
 */

import net from 'node:net';

const SOCKET_CHECK_TIMEOUT_MS = 2000;
const PROCESS_TREE_TIMEOUT_MS = 2000;

export function isPidAlive(pid) {
  if (!pid) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    if (err.code === 'ESRCH') return false;
    if (err.code === 'EPERM') return true;
    return false;
  }
}

export async function checkSocketClosed(port, timeoutMs = SOCKET_CHECK_TIMEOUT_MS) {
  if (!port) return true;
  return new Promise((resolve) => {
    const socket = new net.Socket();
    socket.setTimeout(timeoutMs);
    socket.on('connect', () => { socket.destroy(); resolve(false); });
    socket.on('error', () => { resolve(true); });
    socket.on('timeout', () => { socket.destroy(); resolve(false); });
    try { socket.connect(port, '127.0.0.1'); } catch { resolve(true); }
  });
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

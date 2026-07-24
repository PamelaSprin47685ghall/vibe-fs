/**
 * gate-timeout-cases.mjs — Timeout-protection regression cases.
 *
 * Covers the watchdog port (tests-next 1s-renew heartbeat -> testkit
 * silence watchdog) and the concurrent-awaitEvent timer clobber that
 * hung the host-restart canary: two parallel awaits on one probe used
 * to share a single timer handle, so the loser never timed out.
 */

import http from 'node:http';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { assertEq, assertTrue } from './gate-lib.mjs';
import { EventProbe } from '../event-probe.js';

const execFileAsync = promisify(execFile);
const watchdogUrl = new URL('../watchdog.js', import.meta.url).href;

async function runWatchdogChild(script) {
  try {
    const { stdout, stderr } = await execFileAsync(process.execPath, ['--input-type=module', '-e', script]);
    return { code: 0, stdout, stderr };
  } catch (err) {
    return { code: err.code, stdout: err.stdout || '', stderr: err.stderr || '' };
  }
}

async function runWatchdogFiresOnSilence() {
  const script =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `new Watchdog({ timeoutMs: 150, label: 'gate-silent' });\n` +
    `setInterval(() => {}, 1000);\n`;
  const r = await runWatchdogChild(script);
  assertEq(r.code, 1, 'silent watchdog must exit 1');
  assertTrue(r.stderr.includes('WATCHDOG'), `stderr must carry WATCHDOG diagnostic: ${r.stderr}`);
  assertTrue(r.stderr.includes('gate-silent'), 'diagnostic carries the label');
}

async function runWatchdogRenewsOnPet() {
  const script =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `const w = new Watchdog({ timeoutMs: 200, label: 'gate-pet' });\n` +
    `const iv = setInterval(() => w.pet('tick'), 50);\n` +
    `setTimeout(() => { clearInterval(iv); w.stop(); }, 500);\n`;
  const r = await runWatchdogChild(script);
  assertEq(r.code, 0, `petted watchdog must not fire: ${r.stderr}`);
  assertTrue(!r.stderr.includes('WATCHDOG'), 'no diagnostic on clean exit');
}

function startDelayedSseServer(events, delayMs) {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      res.writeHead(200, {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection': 'keep-alive',
      });
      setTimeout(() => {
        for (const ev of events) res.write(`data: ${JSON.stringify(ev)}\n\n`);
      }, delayMs);
    });
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => resolve({
      url: `http://127.0.0.1:${server.address().port}`,
      close: () => new Promise((r) => {
        try { server.closeAllConnections(); } catch {}
        server.close(() => r());
      }),
    }));
  });
}

async function runConcurrentAwaitTimeouts() {
  const server = await startDelayedSseServer(
    [{ type: 'session.status', properties: { sessionID: 's1', status: 'busy' } }],
    100,
  );
  const probe = new EventProbe(server.url, '/tmp');
  await probe.connect();
  const hit = probe.awaitEvent((e) => e.type === 'session.status', 3000);
  const miss = probe.awaitEvent((e) => e.type === 'never.arrives', 300);
  const [hitResult, missResult] = await Promise.allSettled([hit, miss]);
  assertEq(hitResult.status, 'fulfilled', 'matching concurrent await resolves');
  assertEq(missResult.status, 'rejected', 'non-matching concurrent await must still time out');
  assertTrue(missResult.reason.message.includes('timed out'), 'timeout rejection, not a hang');
  await probe.close();
  await server.close();
}

export const timeoutCases = [
  { name: 'watchdog fires on silence', fn: runWatchdogFiresOnSilence },
  { name: 'watchdog renews on pet', fn: runWatchdogRenewsOnPet },
  { name: 'concurrent awaitEvent timeouts stay independent', fn: runConcurrentAwaitTimeouts },
];

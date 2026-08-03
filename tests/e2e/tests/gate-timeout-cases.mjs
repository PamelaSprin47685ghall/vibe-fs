/**
 * gate-timeout-cases.mjs — the silence criterion of VERIFY-004, as regressions.
 *
 * Four of the thirteen 禁止退化 items are watchdog semantics, and each has a case here:
 *
 *   2  让原始 SSE 或 provider 流量续期 watchdog
 *   3  让背景车道进展续期 watchdog
 *   4  删除 watchdog 的诊断转储，只保留退出码
 *   5  让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口
 *
 * Every watchdog case spawns a real child process and reads its exit code, its stderr, and its
 * wall clock. An in-process fake timer could not prove either of the two properties that matter
 * most: `unref` is only observable as a process that exits while a timer is armed, and the
 * diagnostic dump is only observable as text a human would read. A test that asked the class
 * about its own flags would agree with whatever the class did.
 *
 * Also covers the concurrent-awaitEvent timer clobber that hung the host-restart canary: two
 * parallel awaits on one probe used to share a single timer handle, so the loser never timed out.
 */

import http from 'node:http';
import { execFile, execFileSync } from 'node:child_process';
import { promisify } from 'node:util';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertEq, assertTrue, tmpScenarioDir } from './gate-lib.mjs';
import { EventProbe } from '../event-probe.js';
import { WAIT_FACT_WINDOW_MS, WATCHDOG_TIMEOUT_MS } from '../time-budget.js';
import { walk } from '../../../scripts/repo-scan.mjs';

const execFileAsync = promisify(execFile);
const watchdogUrl = new URL('../watchdog.js', import.meta.url).href;
const budgetUrl = new URL('../time-budget.js', import.meta.url).href;
const driverUrl = new URL('../canary-driver.mjs', import.meta.url).href;
const REPO_ROOT = fileURLToPath(new URL('../../../', import.meta.url));

/**
 * Run a module source as a child and report how it ended.
 *
 * `killAfterMs` exists so a case can distinguish "the subject ended it" from "we ended it":
 * SIGTERM arrives as `signal`, not as an exit code, so a child that had to be killed cannot be
 * mistaken for one that decided to exit 1.
 */
async function runWatchdogChild(script, killAfterMs, budgetEnv) {
  const startedAt = Date.now();
  const options = {
    ...(killAfterMs ? { timeout: killAfterMs, killSignal: 'SIGKILL' } : {}),
    ...(budgetEnv ? { env: { ...process.env, ...budgetEnv } } : {}),
  };
  try {
    const { stdout, stderr } = await execFileAsync(
      process.execPath,
      ['--input-type=module', '-e', script],
      options,
    );
    return { code: 0, signal: null, stdout, stderr, elapsedMs: Date.now() - startedAt };
  } catch (err) {
    return {
      code: err.code ?? null,
      signal: err.signal ?? null,
      stdout: err.stdout || '',
      stderr: err.stderr || '',
      elapsedMs: Date.now() - startedAt,
    };
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

async function runWatchdogRenewsOnProgress() {
  const script =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `const w = new Watchdog({ timeoutMs: 200, label: 'gate-progress' });\n` +
    `const iv = setInterval(() => w.advance({ reason: 'tick', lane: 'gate' }), 50);\n` +
    `setTimeout(() => { clearInterval(iv); w.stop(); }, 500);\n`;
  const r = await runWatchdogChild(script);
  assertEq(r.code, 0, `causal progress must renew watchdog: ${r.stderr}`);
  assertTrue(!r.stderr.includes('WATCHDOG'), 'no diagnostic on clean exit');
}

async function runWatchdogRejectsBackgroundNoise() {
  const script =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `const w = new Watchdog({ timeoutMs: 150, label: 'gate-background' });\n` +
    `setInterval(() => w.advance({ reason: 'blogger', lane: 'blogger', blocking: false }), 30);\n`;
  const r = await runWatchdogChild(script);
  assertEq(r.code, 1, 'background-only activity must not renew watchdog');
  assertTrue(r.stderr.includes('background progress'), 'diagnostic must preserve background activity');
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

// ── VERIFY-004 watchdog properties, one case per 禁止退化 item ────────────────

/**
 * A lane in the shape `scenario-parallel.js` builds from a consumed expectation. Assembled from
 * parts because `gate-path-criterion-cases.mjs` reads every quoted argument to `.includes` in
 * this tree and would resolve a slash-bearing literal against the repo root — its file header
 * declares that residual cost, and paying it here is cheaper than an exemption there.
 */
const CAUSAL_LANE = ['publish', 'main', 'manager', 'turn-1'].join('/');

/**
 * 「让原始 SSE 或 provider 流量续期 watchdog」 — the transport half.
 *
 * `gate-cases.mjs` already covers one named instance (session.created must stay diagnostic
 * data). This covers the shape the clause names last and most bluntly: 任何「有字节在动」的证据.
 * An await with a predicate that accepts every event asks the transport whether bytes moved,
 * not whether the causal chain moved, so a reconnecting SSE reader satisfies it forever. Where
 * such an await feeds `advance`, the silence budget is renewed by motion.
 *
 * Measured instance: `canary-driver.mjs` awaited `() => true` on a 500ms slice inside the
 * `waitFact` loop and renewed on every slice, so a fact that never arrived kept a wrong
 * watchdog alive for the whole 兜底 window.
 *
 * Residual gap, stated rather than hidden: this reads the predicate, not the renewal. A poll
 * loop that renews on the clock while awaiting a NAMED event would pass here — that is what
 * the wall-clock case below measures behaviourally.
 */
function runNoWildcardEventAwait() {
  const WILDCARD_AWAIT = /awaitEvent\(\s*\(?[\w$,\s]*\)?\s*=>\s*(?:true|1)\b/;
  const offenders = [];
  for (const file of walk(join(REPO_ROOT, 'tests/e2e'), ['.js', '.mjs'])) {
    const rel = relative(REPO_ROOT, file);
    readFileSync(file, 'utf8').split('\n').forEach((text, index) => {
      if (WILDCARD_AWAIT.test(text)) offenders.push(`${rel}:${index + 1} ${text.trim()}`);
    });
  }
  assertEq(
    offenders.length,
    0,
    `an await whose predicate accepts any host event is transport motion, not causal progress ` +
      `(VERIFY-004 禁止退化清单 2): ${offenders.join(' | ')}`,
  );
}

/**
 * 「删除 watchdog 的诊断转储，只保留退出码」.
 *
 * The clause requires the dump to answer 「最后一次进展是什么」 — reason AND lane — plus how long
 * ago the last background progress was. Exit code alone is the degradation, and so is a dump
 * that reports a number a reader will misread: the pre-W6 implementation counted background
 * advances into the same total as causal ones, so a scenario whose only activity was a blogger
 * sidecar printed "7 progress update(s)" next to "last: start". Both halves are true and
 * together they say the opposite of what happened.
 */
async function runDiagnosticDumpIsComplete() {
  const backgroundOnly =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `const w = new Watchdog({ timeoutMs: 150, label: 'gate-diagnostic' });\n` +
    `const iv = setInterval(() => w.advance({ reason: 'blogger-projection', lane: 'blogger', blocking: false }), 20);\n` +
    `iv;\n`;
  const r1 = await runWatchdogChild(backgroundOnly);
  assertEq(r1.code, 1, `background-only run must still fire: ${r1.stderr}`);
  assertTrue(
    r1.stderr.includes('0 blocking progress update(s)'),
    `dump must not count background advances as progress: ${r1.stderr}`,
  );
  assertTrue(
    r1.stderr.includes('last progress: start lane=startup'),
    `dump must name the last causal progress by reason AND lane: ${r1.stderr}`,
  );
  assertTrue(
    /background progress \d+ms ago: blogger-projection lane=blogger/.test(r1.stderr),
    `dump must age the last background progress and name its lane: ${r1.stderr}`,
  );

  const oneCausalStep =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `const w = new Watchdog({ timeoutMs: 150, label: 'gate-diagnostic' });\n` +
    `w.advance({ reason: 'expectation:manager.0', lane: ${JSON.stringify(CAUSAL_LANE)}, expectationId: 'manager.0' });\n` +
    `setInterval(() => {}, 60000);\n`;
  const r2 = await runWatchdogChild(oneCausalStep);
  assertEq(r2.code, 1, `a run that stops progressing must fire: ${r2.stderr}`);
  assertTrue(
    r2.stderr.includes('1 blocking progress update(s)'),
    `dump must count the causal advances it renewed on: ${r2.stderr}`,
  );
  assertTrue(
    r2.stderr.includes(`last progress: expectation:manager.0 lane=${CAUSAL_LANE}`),
    `dump must carry the reason and lane of the last renewal: ${r2.stderr}`,
  );
  assertTrue(
    r2.stderr.includes('expectation=manager.0'),
    `dump must name the expectation that was consumed last: ${r2.stderr}`,
  );
  assertTrue(
    !r2.stderr.includes('background progress'),
    `a run with no background lane must not invent a background age: ${r2.stderr}`,
  );
}

/**
 * 「让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口」.
 *
 * The property is `unref`, and it is not observable from inside the process: a test that asked
 * the timer for its own flags would agree with whatever the implementation did. What is
 * observable is the wall clock of a child that finished its work and let its handles close.
 * The silence window here is the real WATCHDOG_TIMEOUT_MS, so the margin asserted is the one a
 * canary gets.
 */
async function runTimerDoesNotHoldEventLoop() {
  const script =
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `import { WATCHDOG_TIMEOUT_MS } from '${budgetUrl}';\n` +
    `const w = new Watchdog({ timeoutMs: WATCHDOG_TIMEOUT_MS, label: 'gate-unref' });\n` +
    `w.advance({ reason: 'only-step', lane: 'gate' });\n` +
    `console.log('done');\n`;
  const r = await runWatchdogChild(script);
  assertEq(r.code, 0, `a scenario that ran out of work must exit clean: ${r.stderr}`);
  assertEq(r.stdout.trim(), 'done', 'the child must reach the end of its work');
  assertTrue(
    r.elapsedMs < WATCHDOG_TIMEOUT_MS,
    `an armed watchdog must not hold the event loop: exited after ${r.elapsedMs}ms of a ` +
      `${WATCHDOG_TIMEOUT_MS}ms silence window`,
  );
}

/**
 * The `waitFact` barrier: renewal must follow an observation, not the poll clock.
 *
 * Two children, because the defect and its over-correction fail in opposite directions and only
 * one assertion each would leave the other open. Deleting every `advance` from the barrier would
 * satisfy the first child and break every real canary that crosses a slow publish chain; renewing
 * on the clock satisfies the second and is the measured defect.
 *
 *   nothing observed        the fake event source answers every slice — the transport saying
 *                           bytes moved — and the journal never gains a line. The barrier must
 *                           be ended by the silence budget, not by the WAIT_FACT_WINDOW_MS 兜底.
 *   journal appending       lines land steadily and the awaited fact only at the end, past two
 *                           silence windows. The barrier must survive on those appends and then
 *                           return, because a production reducer committing durable facts is
 *                           the causal chain moving.
 *
 * The kill deadline in the first child is what turns red if renewal goes back on the clock: a
 * barrier that renews unconditionally reaches it and comes back killed by signal rather than
 * having exited 1.
 */
async function runWaitFactRenewsOnlyOnObservation() {
  // 时间尺度注入：同一「只认观察」语义在半尺度上跑。缩放后仍满足轮询切片（500ms）< 窗口
  // （1000ms）的构造性不等式；值派生自 time-budget.js，经 budgetFromEnv 进子进程。
  const scaledWatchdogMs = WATCHDOG_TIMEOUT_MS / 2;
  const budgetEnv = { WATCHDOG_TIMEOUT_MS: String(scaledWatchdogMs) };
  const silentDir = tmpScenarioDir();
  execFileSync('git', ['-C', silentDir, 'init', '-q'], { encoding: 'utf8' });
  const silent = await runWatchdogChild(
    factBarrierScript(silentDir, 'FactThatNeverAppears', 'gate-wait-fact-silent'),
    scaledWatchdogMs * 2,
    budgetEnv,
  );
  assertEq(
    silent.code,
    1,
    `a fact that never advances must be ended by the silence budget, not by the ` +
      `${WAIT_FACT_WINDOW_MS}ms fallback: exited with code ${silent.code} signal ${silent.signal} ` +
      `after ${silent.elapsedMs}ms`,
  );
  assertTrue(
    silent.elapsedMs < scaledWatchdogMs * 2,
    `barrier survived two injected silence windows (${silent.elapsedMs}ms), so a poll slice renewed it`,
  );
  assertTrue(silent.stderr.includes('WATCHDOG'), `the watchdog must be what ended it: ${silent.stderr}`);
  assertTrue(
    silent.stderr.includes('gate-wait-fact-silent'),
    `diagnostic must name the scenario: ${silent.stderr}`,
  );

  const appendingDir = tmpScenarioDir();
  execFileSync('git', ['-C', appendingDir, 'init', '-q'], { encoding: 'utf8' });
  const journalDir = join(appendingDir, '.git', 'wanxiangshu-next', 'runtimes');
  mkdirSync(journalDir, { recursive: true });
  const journalFile = join(journalDir, 'gate.ndjson');
  writeFileSync(journalFile, '');
  // appendEvery 在父进程求值后插值进子脚本，故必须用注入尺度派生——用 WATCHDOG_TIMEOUT_MS
  // 会让子进程的追加节奏停留在未缩放尺度上（实测：窗口已缩而间隔未缩，用例耗时不变）。
  const appendEvery = Math.floor(scaledWatchdogMs / 4);
  const appendsBeforeFact = 12;
  const appending = await runWatchdogChild(
    `import { appendFileSync } from 'node:fs';\n` +
      `let appended = 0;\n` +
      `const iv = setInterval(() => {\n` +
      `  appended += 1;\n` +
      `  const fact = appended < ${appendsBeforeFact} ? 'UnrelatedProgressFact' : 'AwaitedFact';\n` +
      `  appendFileSync(${JSON.stringify(journalFile)}, JSON.stringify({ type: fact, n: appended }) + '\\n');\n` +
      `}, ${appendEvery});\n` +
      factBarrierScript(appendingDir, 'AwaitedFact', 'gate-wait-fact-appending') +
      `clearInterval(iv);\n` +
      `console.log('barrier returned after ' + appended + ' appends');\n`,
    WAIT_FACT_WINDOW_MS,
    budgetEnv,
  );
  assertEq(
    appending.code,
    0,
    `journal appends are the production reducer committing facts, so the barrier must survive ` +
      `them: ${appending.stderr}`,
  );
  assertTrue(
    appending.elapsedMs > scaledWatchdogMs,
    `this child must outlive one injected silence window for its survival to mean anything, ran ${appending.elapsedMs}ms`,
  );
  assertEq(
    appending.stdout.trim(),
    `barrier returned after ${appendsBeforeFact} appends`,
    `the barrier must return on the awaited fact, not on an earlier one: ${appending.stdout}`,
  );
}

/** The child body both halves share: the real barrier, a real watchdog, a fake event source. */
function factBarrierScript(workDir, factName, label) {
  return (
    `import { Watchdog } from '${watchdogUrl}';\n` +
    `import { awaitFactBarrier } from '${driverUrl}';\n` +
    `import { WATCHDOG_TIMEOUT_MS } from '${budgetUrl}';\n` +
    `const scenario = {\n` +
    `  host: { workDir: ${JSON.stringify(workDir)} },\n` +
    // Answers every slice, and answers nothing semantic. If the barrier ever consults an event
    // source again, this is what it will hear, and this case says that must not renew anything.
    `  events: { awaitEvent: (_predicate, ms) => new Promise((resolve) => setTimeout(resolve, ms)) },\n` +
    `  watchdog: new Watchdog({ timeoutMs: WATCHDOG_TIMEOUT_MS, label: ${JSON.stringify(label)} }),\n` +
    `};\n` +
    `await awaitFactBarrier(scenario, { waitFact: { name: ${JSON.stringify(factName)}, eq: 1 }, lane: 'fact-lane' });\n` +
    `scenario.watchdog.stop();\n`
  );
}

export const timeoutCases = [
  { name: 'watchdog fires on silence', fn: runWatchdogFiresOnSilence },
  { name: 'watchdog renews on causal progress', fn: runWatchdogRenewsOnProgress },
  { name: 'watchdog rejects background-only noise', fn: runWatchdogRejectsBackgroundNoise },
  { name: 'concurrent awaitEvent timeouts stay independent', fn: runConcurrentAwaitTimeouts },
  { name: 'VERIFY-004 no watchdog feed awaits an unspecified host event', fn: runNoWildcardEventAwait },
  { name: 'VERIFY-004 the timeout dump separates causal progress from background', fn: runDiagnosticDumpIsComplete },
  { name: 'VERIFY-004 a clean scenario is not held to the end of the silence window', fn: runTimerDoesNotHoldEventLoop },
  { name: 'VERIFY-004 waitFact renews only on an observation', fn: runWaitFactRenewsOnlyOnObservation },
];

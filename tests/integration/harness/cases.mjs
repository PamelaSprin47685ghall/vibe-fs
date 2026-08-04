/**
 * gate-cases.mjs — Behavior test cases for tests/e2e quality gates.
 */

import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import { spawn } from 'node:child_process';
import {
  assertEq,
  assertTrue,
  tmpScenarioDir,
  startSseServer,
  postJson,
} from './lib.mjs';
import { StrictMockProvider } from '../../e2e/support/strict-mock-provider.js';
import { kindOf } from '../../e2e/support/runtime-key.js';
import { EventProbe } from '../../e2e/support/event-probe.js';
import { shapeFromParsed } from '../../e2e/support/event-shape.js';
import { ProcessHost } from '../../e2e/support/process-host.js';
import { createIsolatedEnv } from '../../e2e/support/isolated-env.js';
import { gatherDiagnostics } from '../../e2e/support/diagnostics.js';
import { createScenarioTurn } from '../../e2e/support/scenario-turn.js';
import { runStabilityGate } from '../../e2e/support/stability-checker.js';
import {
  DEFAULT_AWAIT_TIMEOUT_MS,
  GATE_PROBE_TIMEOUT_MS,
  GATE_HOST_START_TIMEOUT_MS,
} from '../../e2e/support/time-budget.js';
import { laneCases } from './lane-cases.mjs';

async function runIsolationHardening() {
  const scenarioDir = tmpScenarioDir();
  const env = createIsolatedEnv({
    scenarioDir,
    llmUrl: 'http://127.0.0.1:9999/v1',
    extraEnv: {
      HOME: '/should-be-overwritten',
      XDG_CONFIG_HOME: '/should-be-overwritten',
      PATH: '/custom/bin',
      CUSTOM_VAR: 'kept',
    },
  });

  assertTrue(env.HOME.startsWith(scenarioDir), 'HOME must be scenario-specific');
  assertTrue(env.XDG_CONFIG_HOME.startsWith(scenarioDir), 'XDG_CONFIG_HOME must be scenario-specific');
  assertTrue(env.PATH.includes('/custom/bin'), 'PATH must include custom extraEnv segment');
  assertTrue(typeof env.PATH === 'string' && env.PATH.length > 0, 'PATH must remain defined');
  assertEq(env.CUSTOM_VAR, 'kept', 'non-isolation extraEnv vars preserved');
  assertTrue(!fs.existsSync(path.join(scenarioDir, 'workspace', 'node_modules')), 'dependency links must stay outside the Git worktree');
}

async function runProcessHostEnvIsolation() {
  const scenarioDir = tmpScenarioDir();
  const provider = new StrictMockProvider();
  const providerUrl = await provider.start();

  const host = new ProcessHost();
  try {
    await host.start({
      scenarioDir,
      providerUrl: `${providerUrl}/v1`,
      startTimeoutMs: GATE_HOST_START_TIMEOUT_MS,
      extraEnv: {
        HOME: '/evil',
        XDG_CONFIG_HOME: '/evil',
        CUSTOM_VAR: 'kept',
      },
    });

    assertTrue(host._env.HOME.startsWith(scenarioDir), 'ProcessHost env HOME must be scenario-specific');
    assertTrue(host._env.XDG_CONFIG_HOME.startsWith(scenarioDir), 'ProcessHost XDG_CONFIG_HOME isolated');
    assertEq(host._env.CUSTOM_VAR, 'kept', 'ProcessHost preserves non-isolation extraEnv');
  } finally {
    try { await host.stop({ assert: true }); } catch {}
    try { await provider.stop(); } catch {}
    try { fs.rmSync(scenarioDir, { recursive: true, force: true }); } catch {}
  }

  assertEq(host.pid, null, 'ProcessHost must reset pid after stop');
  assertEq(host.baseUrl, null, 'ProcessHost must reset baseUrl after stop');
  assertTrue(!host._started && !host._stopped, 'ProcessHost start/stop flags reset');
}

async function runProcessHostHealthDeadline() {
  const sockets = new Set();
  const server = net.createServer((socket) => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));

  const address = server.address();
  const host = new ProcessHost();
  host._baseUrl = `http://127.0.0.1:${address.port}`;
  let guardTimer;

  try {
    let failure;
    try {
      await Promise.race([
        host._waitForHealth(200),
        new Promise((_, reject) => {
          guardTimer = setTimeout(() => reject(new Error('health request ignored its deadline')), 500);
        }),
      ]);
    } catch (error) {
      failure = error;
    }

    assertTrue(
      failure?.message?.includes('Health-check failed'),
      `a connected server that never answers must reach the health deadline: ${failure?.message}`,
    );
  } finally {
    clearTimeout(guardTimer);
    for (const socket of sockets) socket.destroy();
    await new Promise((resolve) => server.close(resolve));
  }
}

async function runProcessHostStderrCapture() {
  const host = new ProcessHost();
  host._onStderr('stderr-warning\n');
  host._onStdout('stdout-line\n');
  assertTrue(host.stderrLog.includes('stderr-warning'), 'stderr ring buffer captured');
  assertTrue(host.stdoutLog.includes('stdout-line'), 'stdout ring buffer captured');
}









async function runStabilityRepeatCap() {
  let rejected = false;
  try {
    await runStabilityGate({
      test: { name: 'repeat-cap', fn: async () => {} },
      repeat: 4,
    });
  } catch (err) {
    rejected = true;
    assertTrue(err.message.includes('1 through 3'), 'repeat cap diagnostics must name the allowed range');
  }
  assertTrue(rejected, 'stability gate must reject more than three runs');
}

function runTitleHistoryIsolation() {
  const body = {
    messages: [
      { role: 'system', content: 'You are a coding agent.' },
      { role: 'user', content: 'An older request.' },
      { role: 'assistant', content: 'An older answer.' },
      { role: 'user', content: 'Another older request.' },
      { role: 'user', content: 'Generate a title for this conversation: old request' },
      { role: 'assistant', content: 'Old title' },
      { role: 'user', content: 'Continue the real task.' },
    ],
  };
  assertEq(kindOf(body), 'chat', 'historical title prompt must not classify the current request as title');
}

function runSessionCreatedIsNotWatchdogHeartbeat() {
  const scenarioCode = fs.readFileSync(new URL('../../e2e/support/scenario-parallel.js', import.meta.url), 'utf8');
  const driverCode = fs.readFileSync(new URL('../../e2e/support/scenario-driver.mjs', import.meta.url), 'utf8');
  assertTrue(scenarioCode.includes('sessionCreatedDiagnostics'), 'session.created must remain diagnostic data');
  assertTrue(!scenarioCode.includes("reason: 'session-created'"), 'session.created must not be a global watchdog heartbeat');
  assertTrue(
    !driverCode.includes("e.type === 'session.created' && e.sessionAgent === agent"),
    'bindChild must read the Host session snapshot instead of treating an event as identity data',
  );
  assertTrue(
    driverCode.includes("query: { scope: 'project' }"),
    'bindChild must query the project-wide session snapshot across flattened worktree instances',
  );
}

function runWatchdogTimeoutIsCentralized() {
  // The VALUE is pinned in gate-budget-cases.mjs, against the whole budget table at once. What
  // remains here is narrower and not derivable from that pin: scenario setup must take its
  // silence window FROM the table. `budget-gate`'s anti-drift rule proves the constant is
  // referenced somewhere in scope; this proves it is referenced at the one call site that
  // decides how long a canary may go quiet.
  const scenarioCode = fs.readFileSync(new URL('../../e2e/support/scenario-parallel.js', import.meta.url), 'utf8');
  assertTrue(
    scenarioCode.includes("from './time-budget.js'"),
    'scenario setup must import its budgets from the single source',
  );
  assertTrue(
    scenarioCode.includes('WATCHDOG_TIMEOUT_MS'),
    'scenario setup must consume the centralized watchdog timeout',
  );
}

async function runEventProbeReconnectAndStatus() {
  const server1 = await startSseServer([
    { type: 'session.status', properties: { sessionID: 's1', status: { type: 'busy' } } },
  ]);
  const probe = new EventProbe(server1.url, '/tmp');
  await probe.connect();
  const busy = await probe.awaitEvent((e) => e.type === 'session.status', GATE_PROBE_TIMEOUT_MS);
  assertEq(busy.status, 'busy', 'status object must be normalised to string');
  assertEq(busy.sessionID, 's1', 'sessionID extracted');
  await probe.close();

  const server2 = await startSseServer([
    { type: 'session.idle', properties: { sessionID: 's1' } },
  ]);
  probe._baseUrl = server2.url;
  await probe.connect();
  const idle = await probe.awaitEvent((e) => e.type === 'session.idle', GATE_PROBE_TIMEOUT_MS);
  assertEq(idle.type, 'session.idle', 'reconnect should receive events');
  assertEq(idle.sessionID, 's1', 'sessionID preserved after reconnect');
  await probe.close();

  await server1.close();
  await server2.close();
}

function runEventProbeSessionAndToolNormalisation() {
  const childCreated = shapeFromParsed({
    type: 'session.created',
    properties: {
      sessionID: 'child',
      info: { parentID: 'parent', agent: 'coder' },
    },
  });
  assertEq(childCreated.parentSessionID, 'parent', 'session parent identity extracted');
  assertEq(childCreated.sessionAgent, 'coder', 'session agent extracted');

  const completedTool = shapeFromParsed({
    type: 'message.part.updated',
    properties: {
      sessionID: 'child',
      part: { type: 'tool', tool: 'write', state: { status: 'completed' } },
    },
  });
  assertEq(completedTool.toolName, 'write', 'tool name extracted');
  assertEq(completedTool.toolStatus, 'completed', 'tool completion extracted');
}

async function runTerminalIdleWithObjectStatus() {
  const server = await startSseServer([
    { type: 'session.status', properties: { sessionID: 's1', status: { type: 'idle' } } },
  ]);
  const probe = new EventProbe(server.url, '/tmp');
  await probe.connect();

  const scenario = { events: probe };
  const turn = createScenarioTurn(scenario).start('s1');
  await turn.awaitTerminal({
    requireActivity: false,
    requireAssistantTerminal: false,
    timeoutMs: GATE_PROBE_TIMEOUT_MS,
  });

  await probe.close();
  await server.close();
}

async function runNoFixedSleepCriticalAssertion() {
  const probe = new EventProbe('http://127.0.0.1:1', '/tmp');
  probe._events.push({ seq: 1, type: 'message.updated', finishReason: 'stop' });
  const start = Date.now();
  await probe.awaitEvent((e) => e.type === 'message.updated', DEFAULT_AWAIT_TIMEOUT_MS);
  const elapsed = Date.now() - start;
  assertTrue(elapsed < 20, `awaitEvent on existing event should be immediate, took ${elapsed}ms`);
}

async function runProcessHostLeakProbe() {
  const host = new ProcessHost();
  const portServer = net.createServer();
  const port = await new Promise((resolve) => {
    portServer.listen(0, '127.0.0.1', () => resolve(portServer.address().port));
  });

  host._port = port;
  host._started = true;
  let threw = false;
  try {
    await host.assertNoLeak();
  } catch (err) {
    threw = true;
    assertTrue(err.message.includes('still listening'), 'open socket leak should be detected');
  } finally {
    await new Promise((resolve) => portServer.close(resolve));
  }
  assertTrue(threw, 'assertNoLeak must throw for open port');

  const child = spawn(process.execPath, ['-e', 'setInterval(()=>{}, 10000)'], { detached: true });
  host._port = null;
  host._pid = child.pid;
  let pidThrew = false;
  try {
    await host.assertNoLeak();
  } catch (err) {
    pidThrew = true;
    assertTrue(err.message.includes('still alive'), 'surviving PID leak should be detected');
  } finally {
    try { process.kill(child.pid, 'SIGKILL'); } catch {}
    try { process.kill(-child.pid, 'SIGKILL'); } catch {}
  }
  assertTrue(pidThrew, 'assertNoLeak must throw for surviving PID');
}

async function runDiagnosticsCollection() {
  const scenarioDir = tmpScenarioDir();
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  fs.writeFileSync(path.join(workDir, '.wanxiangshu.ndjson'), '{"kind":"test"}\n', 'utf8');
  fs.writeFileSync(path.join(workDir, 'file.txt'), 'hello', 'utf8');

  try {
    const events = {
      allEvents: [{ seq: 1, type: 'session.created', sessionID: 's1', time: Date.now() }],
      lastSeq: 1,
    };
    const provider = {
      requests: [{ messages: [{ role: 'user', content: 'hi' }], tools: [] }],
      unexpectedRequests: [],
      remainingExpectations: 0,
    };
    const client = {
      messages: async () => ({ ok: true, data: { data: [{ info: { role: 'user' }, parts: [] }] } }),
      request: async () => ({ ok: true, data: { data: { s1: { type: 'idle' } } } }),
    };
    const host = { workDir, stderrLog: 'stderr-line', pid: null, baseUrl: 'http://x' };

    const scenario = { scenarioDir, events, provider, client, host, sessionIds: ['s1'] };
    const diag = await gatherDiagnostics(scenario);

    assertEq(diag.stderr, 'stderr-line', 'diagnostics captures stderr');
    assertEq(diag.ndjson.lineCount, 1, 'diagnostics captures NDJSON');
    assertTrue(diag.workspaceFiles.includes(path.join(workDir, 'file.txt')), 'workspace files listed');
    assertEq(diag.events.length, 1, 'events captured');
    assertEq(diag.mockRequests.length, 1, 'mock requests captured');
    assertEq(diag.sessionStatuses.s1.type, 'idle', 'session statuses captured');
  } finally {
    fs.rmSync(scenarioDir, { recursive: true, force: true });
  }
}

async function runProcessHostDisposeContract() {
  const hostCode = fs.readFileSync(new URL('../../e2e/support/process-host.js', import.meta.url), 'utf8');
  const checksCode = fs.readFileSync(new URL('../../e2e/support/process-host-checks.js', import.meta.url), 'utf8');
  assertTrue(hostCode.includes('checkProcessTree'), 'ProcessHost dispose must inspect process tree');
  assertTrue(hostCode.includes('checkSocketClosed'), 'ProcessHost dispose must check port closed');
  assertTrue(hostCode.includes('isPidAlive'), 'ProcessHost dispose must check pid dead');
  assertTrue(hostCode.includes('SIGKILL'), 'ProcessHost dispose must escalate to SIGKILL');
  assertTrue(checksCode.includes('function checkProcessTree'), 'process-tree helper exported');
  assertTrue(checksCode.includes('function checkSocketClosed'), 'socket helper exported');
  assertTrue(checksCode.includes('function getDescendantPids'), 'descendant PID helper exported');
}

async function runScenarioStrictDefault() {
  const stateCode = fs.readFileSync(new URL('../../e2e/support/strict-mock-state.js', import.meta.url), 'utf8');
  assertTrue(stateCode.includes('strict: true'), 'StrictMockProvider state defaults strict=true');
  const provider = new StrictMockProvider();
  assertEq(provider.strict, true, 'new StrictMockProvider().strict === true');
  await provider.start();
  try {
    const res = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      messages: [{ role: 'user', content: 'no expectation' }],
    });
    assertEq(res.status, 500, 'empty queue under strict must 500');
    assertEq(provider.unexpectedRequests.length, 1, 'empty queue records unexpected');
  } finally {
    await provider.stop();
  }
}

export const cases = [
  { name: 'isolation hardening', fn: runIsolationHardening },
  { name: 'ProcessHost env isolation + dispose reset', fn: runProcessHostEnvIsolation },
  { name: 'ProcessHost health request obeys its deadline', fn: runProcessHostHealthDeadline },
  { name: 'ProcessHost stderr/stdout ring buffer capture', fn: runProcessHostStderrCapture },
  ...laneCases,
  { name: 'stability repeat cap is three', fn: runStabilityRepeatCap },
  { name: 'title classification uses current user turn', fn: runTitleHistoryIsolation },
  { name: 'session.created noise does not renew watchdog', fn: runSessionCreatedIsNotWatchdogHeartbeat },
  { name: 'watchdog timeout comes from the budget module', fn: runWatchdogTimeoutIsCentralized },
  { name: 'EventProbe reconnect and status normalisation', fn: runEventProbeReconnectAndStatus },
  { name: 'EventProbe session and tool normalisation', fn: runEventProbeSessionAndToolNormalisation },
  { name: 'terminal idle with object status', fn: runTerminalIdleWithObjectStatus },
  { name: 'no fixed-sleep critical assertion', fn: runNoFixedSleepCriticalAssertion },
  { name: 'ProcessHost leak probe', fn: runProcessHostLeakProbe },
  { name: 'diagnostics collection', fn: runDiagnosticsCollection },
  { name: 'ProcessHost dispose contract', fn: runProcessHostDisposeContract },
  { name: 'scenario strict mock default empty-queue fails', fn: runScenarioStrictDefault },
];

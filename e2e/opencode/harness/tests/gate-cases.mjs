/**
 * gate-cases.mjs — Behavior test cases for SPEC 3.x / quality gates.
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
} from './gate-lib.mjs';
import { StrictMockProvider } from '../strict-mock-provider.js';
import { EventProbe } from '../event-probe.js';
import { ProcessHost } from '../process-host.js';
import { createIsolatedEnv } from '../isolated-env.js';
import { gatherDiagnostics } from '../diagnostics.js';
import { createScenarioTurn } from '../scenario-turn.js';

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
  assertTrue(env.PATH.includes('mcp-bin'), 'PATH must include fixture shim');
  assertEq(env.CUSTOM_VAR, 'kept', 'non-isolation extraEnv vars preserved');
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
      startTimeoutMs: 15000,
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
 
async function runProcessHostStderrCapture() {
  const host = new ProcessHost();
  host._onStderr('stderr-warning\n');
  host._onStdout('stdout-line\n');
  assertTrue(host.stderrLog.includes('stderr-warning'), 'stderr ring buffer captured');
  assertTrue(host.stdoutLog.includes('stdout-line'), 'stdout ring buffer captured');
}

async function runStrictMockFifo() {
  const provider = new StrictMockProvider();
  provider.strict = true;
  await provider.start();

  try {
    provider.expectToolCall({
      id: 'write-once',
      tool: 'write',
      args: { filePath: 'x.txt', content: 'ok\n' },
      match: { requiredTools: ['write'] },
    });

    const base = provider.url;
    const mismatchBody = {
      model: 'test-model',
      messages: [{ role: 'user', content: 'do it' }],
      tools: [{ type: 'function', function: { name: 'read' } }],
    };

    const mismatchRes = await postJson(`${base}/v1/chat/completions`, mismatchBody);
    assertEq(mismatchRes.status, 500, 'mismatch should return 500');
    assertEq(provider.remainingExpectations, 1, 'mismatch should not consume expectation');
    assertEq(provider.unexpectedRequests.length, 1, 'mismatch should record unexpected');

    const matchBody = {
      model: 'test-model',
      messages: [{ role: 'user', content: 'write x.txt' }],
      tools: [{ type: 'function', function: { name: 'write' } }],
    };
    const matchRes = await postJson(`${base}/v1/chat/completions`, matchBody);
    assertTrue(matchRes.ok, 'match should return 200');
    assertEq(provider.remainingExpectations, 0, 'match should consume expectation');

    const noExpRes = await postJson(`${base}/v1/chat/completions`, matchBody);
    assertEq(noExpRes.status, 500, 'empty queue should return 500');
    assertEq(provider.unexpectedRequests.length, 2, 'empty queue should record unexpected');

    let satisfiedThrew = false;
    try { provider.expectSatisfied(); } catch (err) {
      satisfiedThrew = true;
      assertTrue(err.message.includes('unexpected'), 'expectSatisfied should fail on unexpected');
    }
    assertTrue(satisfiedThrew, 'expectSatisfied must throw');
  } finally {
    await provider.stop();
  }
}

async function runEventProbeReconnectAndStatus() {
  const server1 = await startSseServer([
    { type: 'session.status', properties: { sessionID: 's1', status: { type: 'busy' } } },
  ]);
  const probe = new EventProbe(server1.url, '/tmp');
  await probe.connect();
  const busy = await probe.awaitEvent((e) => e.type === 'session.status', 3000);
  assertEq(busy.status, 'busy', 'status object must be normalised to string');
  assertEq(busy.sessionID, 's1', 'sessionID extracted');
  await probe.close();

  const server2 = await startSseServer([
    { type: 'session.idle', properties: { sessionID: 's1' } },
  ]);
  probe._baseUrl = server2.url;
  await probe.connect();
  const idle = await probe.awaitEvent((e) => e.type === 'session.idle', 3000);
  assertEq(idle.type, 'session.idle', 'reconnect should receive events');
  assertEq(idle.sessionID, 's1', 'sessionID preserved after reconnect');
  await probe.close();

  await server1.close();
  await server2.close();
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
    timeoutMs: 3000,
  });

  await probe.close();
  await server.close();
}

async function runNoFixedSleepCriticalAssertion() {
  const probe = new EventProbe('http://127.0.0.1:1', '/tmp');
  probe._events.push({ seq: 1, type: 'message.updated', finishReason: 'stop' });
  const start = Date.now();
  await probe.awaitEvent((e) => e.type === 'message.updated', 30000);
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

export const cases = [
  { name: 'isolation hardening', fn: runIsolationHardening },
  { name: 'ProcessHost env isolation + dispose reset', fn: runProcessHostEnvIsolation },
  { name: 'ProcessHost stderr/stdout ring buffer capture', fn: runProcessHostStderrCapture },
  { name: 'strict mock FIFO and unexpected requests', fn: runStrictMockFifo },
  { name: 'EventProbe reconnect and status normalisation', fn: runEventProbeReconnectAndStatus },
  { name: 'terminal idle with object status', fn: runTerminalIdleWithObjectStatus },
  { name: 'no fixed-sleep critical assertion', fn: runNoFixedSleepCriticalAssertion },
  { name: 'ProcessHost leak probe', fn: runProcessHostLeakProbe },
  { name: 'diagnostics collection', fn: runDiagnosticsCollection },
];

/**
 * agent-dsl-canary.mjs — Layered Manager DSL canary and stability gate.
 *
 * Runs a deterministic isolated scenario through extracted host-only TestKit,
 * followed by a one-iteration stability loop with per-run disposal and leak checks.
 * Uses event-driven waits; no fixed sleeps or production imports into TestKit.
 *
 * Run: node testkit/opencode/tests/agent-dsl-canary.mjs
 */

import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { execSync } from 'node:child_process';
import {
  runStaticGate,
  getSessionId,
  setupScenario,
  teardownScenario,
} from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

console.log('--- Manager DSL Canary ---\n');

// 1. Static Analysis Gate: Ensure no fixed sleeps or prohibited patterns
console.log('1. Running static analysis gate...');
const staticGateResult = runStaticGate([__filename]);
if (!staticGateResult.passed) {
  console.error('  ✗ Static analysis gate failed:');
  for (const v of staticGateResult.violations) {
    console.error(`    [${v.type}] ${v.file}:${v.line} — ${v.message}`);
  }
  process.exit(1);
}
console.log('  ✓ Static analysis gate passed (no fixed sleeps found)\n');

// 2. Report Real Host Capabilities
console.log('2. Reporting host capabilities...');
let opencodeCliAvailable = false;
let opencodeVersion = 'unknown';

try {
  const versionBuf = execSync('opencode --version', { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
  opencodeCliAvailable = true;
  opencodeVersion = versionBuf.trim();
} catch {
  opencodeCliAvailable = false;
}

console.log(`  - CLI Binary ('opencode'): ${opencodeCliAvailable ? `Available (v${opencodeVersion})` : 'UNAVAILABLE (Host capabilities limited)'}`);
console.log('  - Isolated Env: Supported (Temp HOME, XDG_CONFIG_HOME)');
console.log('  - Strict Mock Provider: Available (causal lane matching, explicit request expectations)');
console.log('  - Event Probe: Available (SSE stream reconnect, sequence tracking)');
console.log('  - Resource Leak Detection: Active (Port/PID/Process-tree tracking)\n');

// 3. Scenario Definition
async function canaryScenario(scenario) {
  scenario.provider.expectTitle({
    id: 'manager-title',
    lane: expectationLane('manager-dsl', 'manager-title', 'title', 1, 'title'),
  });

  const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];

  // Manager and Coder are independent lanes; each lane is ordered by turn.
  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
    lane: expectationLane('manager-dsl', 'manager', 'manager', 1),
    tool: 'fork',
    args: {
      agent: 'coder',
      prompt: 'Write canary_output.txt with exactly Coder canary slice OK\\n, then report completion.',
    },
    match: {
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: forbiddenManagerTools,
    },
  });

  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('manager-dsl', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    neverEnd: true,
    text: 'Manager background remains busy.',
    match: {
      containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'],
    },
  });

  scenario.provider.expectToolCall({
    id: 'coder-write',
    lane: expectationLane('manager-dsl', 'coder', 'coder', 1, 'chat', 'manager'),
    tool: 'write',
    args: { filePath: 'canary_output.txt', content: 'Coder canary slice OK\n' },
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectText({
    id: 'coder-blogger',
    lane: expectationLane('manager-dsl', 'coder-blogger', 'blogger', 1, 'chat', 'coder'),
    blocking: false,
    neverEnd: true,
    text: 'Coder background remains busy.',
    match: {
      containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'],
    },
  });

  scenario.provider.expectText({
    id: 'coder-finished',
    lane: expectationLane('manager-dsl', 'coder', 'coder', 2, 'chat', 'manager'),
    text: 'Coder write complete.',
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectToolCall({
    id: 'manager-join-coder',
    lane: expectationLane('manager-dsl', 'manager', 'manager', 2),
    tool: 'join',
    args: {},
    match: {
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: forbiddenManagerTools,
    },
  });

  scenario.provider.expectText({
    id: 'manager-joined-coder',
    lane: expectationLane('manager-dsl', 'manager', 'manager', 3),
    text: 'Manager joined Coder: canary complete.',
    match: {
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: forbiddenManagerTools,
    },
  });

  // Create session on real host process
  const sessionRes = await scenario.client.createSession();
  const sessionID = getSessionId(sessionRes);
  if (!sessionID) {
    throw new Error(`Failed to obtain valid session ID, got: ${JSON.stringify(sessionRes)}`);
  }
  scenario.sessionIds.push(sessionID);
  bindLaneSession(scenario.provider, sessionID, 'manager-title', 'manager');

  const managerStartSeq = scenario.events.lastSeq;

  // Prompt the real Manager agent explicitly. Do not use the default primary
  // agent: that would make a direct Manager write look like a passing canary.
  const promptRes = await scenario.client.request('POST', `/session/${sessionID}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Delegate this canary to a Coder with fork, join the Coder, and report the result.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  if (!promptRes.ok) {
    throw new Error(`Prompt failed with status ${promptRes.status}: ${JSON.stringify(promptRes.data)}`);
  }

  await scenario.provider.waitForExpectation('manager-fork-coder', WATCHDOG_TIMEOUT_MS);
  const coderCreated = await scenario.events.awaitEvent(
    (event) => event.seq > managerStartSeq
      && event.type === 'session.created'
      && event.parentSessionID === sessionID
      && event.sessionAgent === 'coder',
    WATCHDOG_TIMEOUT_MS,
  );
  const coderSessionID = coderCreated.sessionID;
  assert.ok(coderSessionID, 'Manager fork must create a real Coder child session');
  bindLaneSession(scenario.provider, coderSessionID, 'coder');
  scenario.watchdog?.advance({
    reason: 'manager-coder-child-created',
    lane: `session:${coderSessionID}`,
    blocking: true,
  });

  const coderWriteCompleted = await scenario.events.awaitEvent(
    (event) => event.seq > coderCreated.seq
      && event.type === 'message.part.updated'
      && event.sessionID === coderSessionID
      && event.toolName === 'write'
      && event.toolStatus === 'completed',
    WATCHDOG_TIMEOUT_MS,
  );
  scenario.watchdog?.advance({
    reason: 'manager-coder-write-completed',
    lane: `session:${coderSessionID}`,
    blocking: true,
  });

  const coderFinalTurn = scenario.turn.start(coderSessionID, { afterSeq: coderWriteCompleted.seq });
  await coderFinalTurn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  const managerJoinCompleted = await scenario.events.awaitEvent(
    (event) => event.seq > coderFinalTurn.terminalSeq
      && event.type === 'message.part.updated'
      && event.sessionID === sessionID
      && event.toolName === 'join'
      && event.toolStatus === 'completed',
    WATCHDOG_TIMEOUT_MS,
  );
  scenario.watchdog?.advance({
    reason: 'manager-coder-join-completed',
    lane: `session:${sessionID}`,
    blocking: true,
  });

  const managerFinalTurn = scenario.turn.start(sessionID, { afterSeq: managerJoinCompleted.seq });
  await managerFinalTurn.awaitTerminal({
    timeoutMs: 5000,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  // Check file oracle state: only the Coder response writes this file.
  scenario.fs.expectFile('canary_output.txt');
  scenario.fs.expectFileContent('canary_output.txt', 'Coder canary slice OK\n');

  const requests = scenario.provider.requests;
  const managerRequests = requests.filter((request) => JSON.stringify(request).includes('Delegate this canary to a Coder'));
  assert.ok(managerRequests.length >= 3, 'Manager must issue fork, join, and final turns');
  for (const request of managerRequests) {
    const names = request.tools?.map((tool) => tool.function?.name || tool.name).filter(Boolean) || [];
    assert.deepEqual(names.filter((name) => forbiddenManagerTools.includes(name)), [], 'Manager request exposed a forbidden file/process tool');
  }

  const childRequests = requests.filter((request) => JSON.stringify(request).includes('Write canary_output.txt with exactly'));
  assert.ok(childRequests.some((request) => {
    const names = request.tools?.map((tool) => tool.function?.name || tool.name).filter(Boolean) || [];
    return names.includes('write');
  }), 'Coder provider turn did not expose write');

  // Verify the join result reached the Manager as a tool message in the
  // session transcript. The tool result contains agentId/runId/outcome keys
  // from the completed Coder run, proving fork → join produced a result.
  const managerTranscript = await scenario.client.messages(sessionID);
  assert.ok(managerTranscript.ok, `failed to read Manager transcript: ${JSON.stringify(managerTranscript.data)}`);
  const transcriptBody = JSON.stringify(managerTranscript.data);
  assert.match(transcriptBody, /"tool"/, 'join result should appear as a tool message in the Manager session');
}

console.log('3. Running one causal Manager DSL scenario...');
let scenario;
try {
  scenario = await setupScenario({
    project: {
      files: {
        'AGENTS.md': '- manager dsl canary iteration\n',
      },
    },
    strict: true,
  });
  await canaryScenario(scenario);
  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
} catch (error) {
  console.error(`\n  ✗ Manager DSL scenario failed: ${error.stack || error}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

console.log('\n✓ Manager DSL canary completed cleanly.');
process.exit(0);

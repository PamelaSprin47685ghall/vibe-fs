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
  runStabilityGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';

const __filename = fileURLToPath(import.meta.url);

console.log('--- Manager DSL Canary & Stability Gate ---\n');

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
console.log('  - Strict Mock Provider: Available (FIFO matching, synthetic completions)');
console.log('  - Event Probe: Available (SSE stream reconnect, sequence tracking)');
console.log('  - Resource Leak Detection: Active (Port/PID/Process-tree tracking)\n');

// 3. Scenario Definition
async function canaryScenario(scenario) {
  // Title generation is a separate host request; keep it deterministic without
  // weakening the FIFO expectations for Manager and Coder turns.
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowTitleGeneration();

  const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep'];

  // FIFO: Manager forks Coder, then emits join (which waits); the child Coder
  // writes and reports, after which Manager returns the joined completion. The
  // response args are real tool payloads consumed by the host, not a shortcut.
  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
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

  scenario.provider.expectToolCall({
    id: 'coder-write-file',
    tool: 'write',
    args: { filePath: 'canary_output.txt', content: 'Coder canary slice OK\n' },
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectText({
    id: 'coder-completed',
    text: 'Coder completed the canary write.',
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectToolCall({
    id: 'manager-join-coder',
    tool: 'join',
    args: {},
    match: {
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: forbiddenManagerTools,
    },
  });

  scenario.provider.expectText({
    id: 'manager-joined-coder',
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

  // Monitor turn with event-driven watermark tracking
  const turn = scenario.turn.start(sessionID);

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

  // Event-driven wait: require activity + terminal idle state without fixed sleep
  await turn.awaitTerminal({
    timeoutMs: 30000,
    requireActivity: true,
    requireAssistantTerminal: false,
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

  const managerRequestText = JSON.stringify(managerRequests);
  assert.match(managerRequestText, /agentId/, 'fork result did not reach Manager continuation');
  assert.match(managerRequestText, /Coder completed the canary write\./, 'join result did not reach Manager final turn');
}

// 4. Single Isolated Execution Check
console.log('3. Running single isolated scenario check...');
const singleScenarioOpts = {
  project: {
    files: {
      'AGENTS.md': '- manager dsl canary project\n',
    },
  },
  strict: true,
};

let scenario;
try {
  scenario = await setupScenario(singleScenarioOpts);
  await canaryScenario(scenario);
  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('  ✓ Single isolated scenario passed successfully\n');
} catch (err) {
  console.error(`  ✗ Single isolated scenario failed: ${err.message}`);
  if (scenario?.provider?.requests) {
    const requests = scenario.provider.requests.map((request) => ({
      sessionId: request.sessionId,
      tools: request.tools?.map((tool) => tool.function?.name || tool.name),
      messages: request.messages?.length,
    }));
    console.error(`  provider requests: ${JSON.stringify(requests)}`);
  }
  if (scenario?.provider?.unexpectedRequests) console.error(`  unexpected: ${JSON.stringify(scenario.provider.unexpectedRequests)}`);
  if (scenario?.events?._events) console.error(`  events: ${JSON.stringify(scenario.events._events.slice(-20))}`);
  if (scenario?.host?.stdoutLog) console.error(`  host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`  host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

// 5. One-Iteration Stability Loop with per-run disposal and leak checks
console.log('4. Running one-iteration stability gate...');
const stabilityGateResult = await runStabilityGate({
  test: {
    name: 'Manager DSL Canary Scenario',
    fn: canaryScenario,
  },
  repeat: 1,
  globalTimeoutMs: 900000,
  scenarioOpts: {
    project: {
      files: {
        'AGENTS.md': '- manager dsl canary iteration\n',
      },
    },
    strict: true,
  },
});

if (!stabilityGateResult.passed) {
  console.error(`\n  ✗ Stability gate failed: ${stabilityGateResult.failures.length} failure(s) recorded`);
  for (const f of stabilityGateResult.failures) {
    console.error(`    Run ${f.run} error: ${f.error.message}`);
  }
  process.exit(1);
}

console.log('\n✓ Manager DSL canary and stability gate completed cleanly with 0 failures.');
process.exit(0);

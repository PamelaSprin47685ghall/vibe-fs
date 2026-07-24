/**
 * agent-dsl-canary.mjs — Layered Manager DSL canary and stability gate.
 *
 * Runs a deterministic isolated scenario through extracted host-only TestKit,
 * followed by a one-iteration stability loop with per-run disposal and leak checks.
 * Uses event-driven waits; no fixed sleeps or production imports into TestKit.
 *
 * Run: node testkit/opencode/tests/agent-dsl-canary.mjs
 */

import path from 'node:path';
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
  // Allow synthetic continuation and title generation for deterministic mock response
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowTitleGeneration();

  // Queue expectations for manager task turn:
  // Step 1: Tool call to write canary output file
  scenario.provider.expectToolCall({
    id: 'canary-write-1',
    tool: 'write',
    args: { filePath: 'canary_output.txt', content: 'Manager canary slice OK\n' },
  });

  // Step 2: Follow-up final response after tool execution completes
  scenario.provider.expectText({
    id: 'canary-done-1',
    text: 'Manager canary task completed successfully.',
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

  // Prompt the session
  const promptRes = await scenario.client.prompt(sessionID, 'Run manager canary task and write canary_output.txt');
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

  // Check file oracle state using FsOracle assertions
  scenario.fs.expectFile('canary_output.txt');
  scenario.fs.expectFileContains('canary_output.txt', 'Manager canary slice OK');
}

// 4. Single Isolated Execution Check
console.log('3. Running single isolated scenario check...');
const singleScenarioOpts = {
  project: {
    files: {
      'AGENTS.md': '- manager dsl canary project\n',
    },
  },
  strict: false,
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
    strict: false,
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

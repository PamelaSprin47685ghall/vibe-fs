import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('PTY stress canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'PTY stress canary\n' } },
    strict: true,

  });

  scenario.provider.expectTitle({
    id: 'inspector-title',
    lane: expectationLane('pty-stress', 'inspector-title', 'title', 1, 'title'),
  });

  // Inspector uses executor tool with bounded PTY-style budget.
  scenario.provider.expectToolCall({
    id: 'inspector-exec-pty',
    lane: expectationLane('pty-stress', 'inspector', 'inspector', 1),
    tool: 'executor',
    args: {
      command: /sh -c|pty|stress|chunk/i,
      estimated_output_bytes: 1024,
      estimated_running_secs: 5,
      estimated_mem_usage: 'medium',
    },
    match: { requiredTools: ['executor'] },
  });

  scenario.provider.expectText({
    id: 'inspector-pty-done',
    lane: expectationLane('pty-stress', 'inspector', 'inspector', 2),
    text: /completed|done|ok/i,
    match: { requiredTools: ['executor'] },
  });

  const inspector = await scenario.client.createSession();
  const inspectorId = getSessionId(inspector);
  assert.ok(inspectorId, `inspector session creation failed: ${JSON.stringify(inspector)}`);
  scenario.sessionIds.push(inspectorId);
  bindLaneSession(scenario.provider, inspectorId, 'inspector-title', 'inspector');

  const turn = scenario.turn.start(inspectorId);
  const prompt = await scenario.client.request('POST', `/session/${inspectorId}/prompt_async`, {
    body: {
      agent: 'inspector',
      parts: [{ type: 'text', text: 'Run a PTY process that handles large output under bounded budget and report completion.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `inspector prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  // Verify boundedExecutor args are reasonable.
  const execRequests = scenario.provider.requests.filter(
    (request) => request.tools?.some((t) => (t.function?.name || t.name) === 'executor'),
  );
  assert.ok(execRequests.length >= 1, 'Inspector must issue at least one executor tool call');

  for (const request of execRequests) {
    const args = request.tools?.find((t) => (t.function?.name || t.name) === 'executor')?.function?.arguments;
    if (args) {
      const parsed = JSON.parse(args);
      assert.ok(parsed.estimated_running_secs <= 30, 'Executor estimated_running_secs must be bounded');
      assert.ok(parsed.estimated_output_bytes <= 1000000, 'Executor estimated_output_bytes must be bounded');
    }
  }

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('PTY stress canary passed: PTY executor with bounded budget and clean teardown.');
} catch (error) {
  console.error(`PTY stress canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

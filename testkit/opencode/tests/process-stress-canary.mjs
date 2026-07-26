import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const SIGKILL_COMMAND = 'sh -lc \'trap "" TERM; sleep 1000\'';

let scenario;
try {
  if (!runStaticGate([__filename]).passed) throw new Error('process stress canary contains prohibited polling');
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'process stress canary\n' } }, strict: true });
  scenario.provider.expectTitle({
    id: 'inspector-title',
    lane: expectationLane('process-stress', 'inspector-title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'inspector-executor-sigkill',
    lane: expectationLane('process-stress', 'inspector', 'inspector', 1),
    tool: 'executor',
    args: {
      command: SIGKILL_COMMAND,
      estimated_output_bytes: 1024,
      estimated_running_secs: 0.2,
      estimated_mem_usage: 'medium',
    },
    match: { requiredTools: ['executor'] },
  });
  scenario.provider.expectText({
    id: 'inspector-sigkill-final',
    lane: expectationLane('process-stress', 'inspector', 'inspector', 2),
    text: 'timeout observed.',
    match: { requiredTools: ['executor'] },
  });

  const created = await scenario.client.createSession();
  const sessionId = getSessionId(created);
  assert.ok(sessionId, `inspector creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(sessionId);
  bindLaneSession(scenario.provider, sessionId, 'inspector-title', 'inspector');
  const turn = scenario.turn.start(sessionId);
  const prompt = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      agent: 'inspector',
      parts: [{ type: 'text', text: 'Run the command and report if it timed out.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `inspector prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const anyRequest = JSON.stringify(scenario.provider.requests);
  assert.ok(
    anyRequest.includes('TimeoutExceeded'),
    `executor tool result must report a timeout`,
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Process stress canary passed: SIGKILL timeout, no orphan processes, and clean teardown.');
} catch (error) {
  console.error(`Process stress canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

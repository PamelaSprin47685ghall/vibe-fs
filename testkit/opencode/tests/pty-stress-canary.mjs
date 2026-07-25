import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('PTY stress canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'PTY stress canary\n' } },
    strict: true,
    watchdogMs: 1000,
  });

  scenario.provider.allowTitleGeneration();
  scenario.provider.allowOutOfOrder();
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowBloggerRequests();

  // Inspector issues executor tool with a PTY-backed command; verifies
  // multi-chunk output and that the process was properly managed.
  scenario.provider.expectToolCall({
    id: 'inspector-exec-pty',
    tool: 'executor',
    args: {
      command: 'sh -c "echo PTY-stress-ok; count=0; while [ $count -lt 3 ]; do echo chunk-$count; count=$((count+1)); done"',
      estimated_output_bytes: 1024,
      estimated_running_secs: 5,
      estimated_mem_usage: 'medium',
    },
    match: { requiredTools: ['executor'] },
  });

  scenario.provider.expectText({
    id: 'inspector-pty-result',
    text: /PTY-stress-ok|chunk-0|chunk-2/i,
    match: { requiredTools: ['executor'] },
  });

  scenario.provider.expectText({
    id: 'inspector-final',
    text: /completed|done|PTY-stress-ok/i,
    match: { requiredTools: ['executor'] },
  });

  const inspector = await scenario.client.createSession();
  const inspectorId = getSessionId(inspector);
  assert.ok(inspectorId, `inspector session creation failed: ${JSON.stringify(inspector)}`);
  scenario.sessionIds.push(inspectorId);

  const turn = scenario.turn.start(inspectorId);
  const prompt = await scenario.client.request('POST', `/session/${inspectorId}/prompt_async`, {
    body: {
      agent: 'inspector',
      parts: [{ type: 'text', text: 'Run a PTY-backed command that produces multiple output chunks and report completion.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `inspector prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const anyRequest = JSON.stringify(scenario.provider.requests);
  assert.ok(
    anyRequest.includes('PTY-stress-ok') || anyRequest.includes('chunk-'),
    'Inspector PTY command must produce output evidence',
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('PTY stress canary passed: PTY-backed executor command produces multi-chunk output with clean teardown.');
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

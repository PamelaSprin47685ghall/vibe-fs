import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

/**
 * Coder → inspector tool is registered and invokable.
 * Full child executor dialogue is covered by unit/host wiring; this canary
 * proves the product tool surface includes one-shot inspector for coder.
 */
let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('inspector oneshot canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'Inspector oneshot canary\n' } },
    strict: true,
  });

  scenario.provider.expectTitle({
    id: 'coder-title',
    lane: expectationLane('inspector-oneshot', 'coder-title', 'title', 1, 'title'),
  });
  scenario.provider.expectText({
    id: 'coder-blogger-1',
    lane: expectationLane('inspector-oneshot', 'coder-blogger', 'blogger', 1, 'chat', 'coder'),
    text: 'coder blog',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });
  scenario.provider.expectToolCall({
    id: 'coder-inspector',
    lane: expectationLane('inspector-oneshot', 'coder', 'coder', 1),
    tool: 'inspector',
    args: { prompt: 'Inspect workspace via executor.' },
    match: { requiredTools: ['inspector'] },
  });
  scenario.provider.expectText({
    id: 'coder-final',
    lane: expectationLane('inspector-oneshot', 'coder', 'coder', 2),
    text: 'Inspector oneshot complete.',
    match: { requiredTools: ['inspector'] },
  });

  const coder = await scenario.client.createSession();
  const coderId = getSessionId(coder);
  assert.ok(coderId, `coder session failed: ${JSON.stringify(coder)}`);
  scenario.sessionIds.push(coderId);
  bindLaneSession(scenario.provider, coderId, 'coder-title', 'coder');

  const turn = scenario.turn.start(coderId);
  const prompt = await scenario.client.request('POST', `/session/${coderId}/prompt_async`, {
    body: {
      agent: 'coder',
      parts: [{
        type: 'text',
        text: 'Call inspector once with prompt: Inspect workspace via executor. Then stop with a short summary.',
      }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `coder prompt failed: ${JSON.stringify(prompt.data)}`);

  // Title and sidecar blogger are intermediate progress points; they keep the
  // scenario watchdog alive while the Coder session is booting under load.
  await scenario.provider.waitForExpectation('coder-title', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'coder-title', lane: 'coder', blocking: true });

  await scenario.provider.waitForExpectation('coder-blogger-1', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'coder-blogger-1', lane: 'coder-blogger', blocking: true });

  await scenario.provider.waitForExpectation('coder-inspector', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'coder-inspector', lane: 'coder', blocking: true });

  await scenario.provider.waitForExpectation('coder-final', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'coder-final', lane: 'coder', blocking: true });

  await turn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Inspector oneshot canary passed: coder invoked inspector tool.');
} catch (error) {
  console.error(`Inspector oneshot canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

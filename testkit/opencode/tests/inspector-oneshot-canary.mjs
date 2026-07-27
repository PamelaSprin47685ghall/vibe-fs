import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

/**
 * Coder → one-shot inspector tool surface.
 * Proves inspector is registered for coder and creates a child executor-only path.
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

  // Inspector child session: bind on first request dynamically via afterExpectation.
  scenario.provider.afterExpectation('coder-inspector', () => {
    scenario.provider.expectToolCall({
      id: 'inspector-executor',
      lane: expectationLane('inspector-oneshot', 'inspector', 'inspector', 1),
      tool: 'executor',
      args: {},
      match: { requiredTools: ['executor'] },
    });
    scenario.provider.expectText({
      id: 'inspector-done',
      lane: expectationLane('inspector-oneshot', 'inspector', 'inspector', 2),
      text: 'inspection done',
      match: { requiredTools: ['executor'] },
    });
  });

  const coder = await scenario.client.createSession();
  const coderId = getSessionId(coder);
  assert.ok(coderId, `coder session failed: ${JSON.stringify(coder)}`);
  scenario.sessionIds.push(coderId);
  bindLaneSession(scenario.provider, coderId, 'coder-title', 'coder');

  // When inspector child is created, bind its session for expectation lanes.
  scenario.events.onEvent((event) => {
    if (event.type === 'session.created' || event.type === 'session.updated') {
      const sid = event.sessionID || event.properties?.sessionID || event.properties?.info?.id;
      const parent = event.properties?.parentID || event.properties?.parentId;
      if (sid && parent === coderId) {
        bindLaneSession(scenario.provider, sid, 'inspector', 'inspector');
      }
    }
  });

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

  await scenario.provider.waitForExpectation('coder-inspector', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'coder-inspector', lane: 'coder', blocking: true });

  await turn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS * 2,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  // Remaining inspector expectations may be unconsumed if tool short-circuited;
  // require at least coder-inspector was hit (above). Drop optional child expects.
  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Inspector oneshot canary passed.');
} catch (error) {
  console.error(`Inspector oneshot canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

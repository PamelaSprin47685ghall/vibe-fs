import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../time-budget.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const BLOGGER_MARKER = 'You are the blogger of a coding agent session.';
const managerRole = 'manager';
const managerAgent = 'fast-manager';
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'executor', 'inspector', 'fork-pty'];

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'manager companion canary\n' } },
    strict: true,
  });

  scenario.provider.expectTitle({
    id: 'manager-title',
    lane: expectationLane('manager-companion', 'manager-title', 'title', 1, 'title'),
  });

  // 1. Manager first turn expectText
  scenario.provider.expectText({
    id: 'manager-first',
    lane: expectationLane('manager-companion', 'manager', managerRole, 1),
    text: 'Manager turn 1.',
    match: {
      requiredTools: managerTools,
      forbiddenTools: forbiddenManagerTools,
    },
  });

  // 2. Manager's Blogger companion first turn expectText
  scenario.provider.expectText({
    id: 'manager-blogger-1',
    lane: expectationLane('manager-companion', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    text: 'Manager Blogger paragraph 1.',
    match: { containsText: [BLOGGER_MARKER] },
  });

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: managerAgent, model: { providerID: 'test', id: 'test-model' } },
  });
  const managerId = getSessionId(parent);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(managerId);
  bindLaneSession(scenario.provider, managerId, 'manager-title', 'manager');

  const turn = scenario.turn.start(managerId);
  const prompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: managerAgent,
      parts: [{ type: 'text', text: 'Start manager work.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  // Verify that a session.created event was emitted for the blogger with parentSessionID = managerId
  const bloggerCreatedEvent = scenario.events.allEvents.find(
    (e) => e.type === 'session.created' && e.parentSessionID === managerId && e.sessionAgent === 'fast-blogger'
  );
  assert.ok(bloggerCreatedEvent, 'session.created event for fast-blogger with manager parentSessionID must be present');

  // Verify blogger expectation satisfied
  await scenario.provider.waitForExpectation('manager-blogger-1', WATCHDOG_TIMEOUT_MS);

  console.log('Manager companion canary passed.');
} catch (error) {
  console.error('Manager companion canary failed:', error);
  scenario?.dumpLogs();
  process.exitCode = 1;
} finally {
  if (scenario) await teardownScenario(scenario);
}

import assert from 'node:assert/strict';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../time-budget.js';
import { bindLaneSession } from './lane.mjs';
import { compileScenario } from '../scenario-schema.js';
import { ScenarioRuntime } from '../scenario-runtime.js';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'executor', 'inspector', 'fork-pty'];

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'manager companion canary\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scripts/manager-companion.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'manager-companion.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));
  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);
  const managerAgent = compiled.scenario.session.agent;
  const managerPrompt = compiled.scenario.prompt.text;

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
      parts: [{ type: 'text', text: managerPrompt }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  const managerRequest = scenario.provider.requests.find((request) => Array.isArray(request.tools) && request.tools.length > 0);
  assert.ok(managerRequest, 'manager provider request must be recorded');
  const managerRequestTools = managerRequest.tools.map((tool) => tool?.function?.name ?? tool?.name);
  assert.ok(managerTools.every((tool) => managerRequestTools.includes(tool)), 'manager tool schema must include manager tools');
  assert.ok(forbiddenManagerTools.every((tool) => !managerRequestTools.includes(tool)), 'manager tool schema must exclude forbidden tools');

  // Verify that a session.created event was emitted for the blogger with parentSessionID = managerId
  const bloggerCreatedEvent = scenario.events.allEvents.find(
    (e) => e.type === 'session.created' && e.parentSessionID === managerId && e.sessionAgent === 'fast-blogger'
  );
  assert.ok(bloggerCreatedEvent, 'session.created event for fast-blogger with manager parentSessionID must be present');

  const bloggerId = bloggerCreatedEvent.sessionID;
  await scenario.events.awaitEvent(
    (event) => event.type === 'session.idle' && event.sessionID === bloggerId,
    WATCHDOG_TIMEOUT_MS,
  );
  assert.deepEqual(runtime.unanswered().map((entry) => entry.id), [], 'all non-internal scenario steps must complete');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');

  console.log('Manager companion canary passed.');
} catch (error) {
  console.error('Manager companion canary failed:', error);
  scenario?.dumpLogs();
  process.exitCode = 1;
} finally {
  if (scenario) await teardownScenario(scenario);
}

import assert from 'node:assert/strict';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../support/index.js';
import { bindLaneSession } from '../support/lane.mjs';
import { compileScenario } from '../support/scenario-schema.js';
import { ScenarioRuntime } from '../support/scenario-runtime.js';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);

  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'manager unhappy path canary\n', 'src/main.txt': 'target\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scenarios/manager-unhappy-path.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'manager-unhappy-path.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));

  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: compiled.scenario.session.agent, model: { providerID: 'test', id: 'test-model' } },
  });
  const managerId = getSessionId(parent);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(managerId);
  bindLaneSession(scenario.provider, managerId, 'mgr-title', 'fast-manager');

  const turn = scenario.turn.start(managerId);
  const prompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: compiled.scenario.prompt.agent,
      parts: [{ type: 'text', text: compiled.scenario.prompt.text }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `prompt failed: ${JSON.stringify(prompt.data)}`);

  await turn.awaitTerminal({ requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const messagesResponse = await scenario.client.request('GET', `/session/${managerId}/message`);
  assert.equal(messagesResponse.ok, true, JSON.stringify(messagesResponse.data));
  const messages = messagesResponse.data?.data ?? messagesResponse.data ?? [];
  const encodedMessages = JSON.stringify(messages);

  assert.match(encodedMessages, /Legacy agent name 'manager' is not supported/,
    'B1: legacy manager fork must be rejected');
  assert.match(encodedMessages, /Unknown managed agent 'fast-nonexistent'/,
    'B2: nonexistent managed agent fork must be rejected');
  assert.match(encodedMessages, /count = 0/,
    'B3: empty list must report count = 0');
  assert.match(encodedMessages, /Your work still walks the world/,
    'B5: premature suicide must be rejected while the coder remains outstanding');
  assert.match(encodedMessages, /Your final words have been received/,
    'B6: final suicide must be confirmed');

  assert.deepEqual(runtime.unanswered().map((entry) => entry.id), [], 'all non-internal scenario steps must complete');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');
} catch (error) {
  console.error('Manager unhappy path canary failed:', error);
  if (scenario) {
    console.error(`Host stdout:\n${scenario.host.stdoutLog}`);
    console.error(`Host stderr:\n${scenario.host.stderrLog}`);
    console.error(`Event tail:\n${scenario.events.dump(20)}`);
  }
  process.exitCode = 1;
} finally {
  if (scenario) await teardownScenario(scenario);
}

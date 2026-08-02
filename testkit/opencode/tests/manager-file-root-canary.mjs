import assert from 'node:assert/strict';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';
import { bindLaneSession } from './lane.mjs';
import { compileScenario } from '../scenario-schema.js';
import { ScenarioRuntime } from '../scenario-runtime.js';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);

  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'manager file-root canary\n', 'src/main.txt': 'seed\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scripts/manager-file-root.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'manager-file-root.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));

  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: compiled.scenario.session.agent, model: { providerID: 'test', id: 'test-model' } },
  });
  const managerId = getSessionId(parent);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(managerId);
  bindLaneSession(scenario.provider, managerId, 'manager-title', 'fast-manager');

  const turn = scenario.turn.start(managerId);
  const fileBody = Buffer.from('SSOT/13 attached canary body\n', 'utf8').toString('base64');
  const prompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: compiled.scenario.prompt.agent,
      parts: [
        { type: 'text', text: compiled.scenario.prompt.text },
        { type: 'file', mime: 'text/plain', filename: 'SSOT/13.md', url: `data:text/plain;base64,${fileBody}` },
      ],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `prompt failed: ${JSON.stringify(prompt.data)}`);

  await turn.awaitTerminal({ requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  const messagesResponse = await scenario.client.request('GET', `/session/${managerId}/message`);
  assert.equal(messagesResponse.ok, true, JSON.stringify(messagesResponse.data));
  const messages = messagesResponse.data?.data ?? messagesResponse.data ?? [];
  const encodedMessages = JSON.stringify(messages);
  assert.match(encodedMessages, /Called the Read tool with the following input/, 'Host must expand the attached file into synthetic read text');
  assert.doesNotMatch(encodedMessages, /no Authority Root fixes this session's role/, 'manager fork must not be rejected by Authority');

  assert.deepEqual(runtime.unanswered().map((entry) => entry.id), [], 'all non-internal scenario steps must complete');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');

  console.log('Manager file-root canary passed.');
} catch (error) {
  console.error('Manager file-root canary failed:', error);
  if (scenario) {
    console.error(`Host stdout:\n${scenario.host.stdoutLog}`);
    console.error(`Host stderr:\n${scenario.host.stderrLog}`);
    console.error(`Event tail:\n${scenario.events.dump(20)}`);
  }
  process.exitCode = 1;
} finally {
  if (scenario) await teardownScenario(scenario);
}

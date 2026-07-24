import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  if (!runStaticGate([__filename]).passed) throw new Error('host lifecycle canary contains prohibited polling');
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'host lifecycle canary\n' } }, strict: true });
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowBloggerRequests();
  scenario.provider.allowOutOfOrder();
  scenario.provider.expectText({ id: 'child-a1', text: 'A1 terminal.', match: {} });
  scenario.provider.expectText({ id: 'child-a2', text: 'A2 terminal.', match: {} });
  scenario.provider.expectText({ id: 'child-a3', text: 'A3 terminal.', match: {} });

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  const child = await scenario.client.request('POST', '/api/session', {
    body: { parentID: parentId, title: 'nudge child', agent: 'coder', model: { providerID: 'test', id: 'test-model' } },
  });
  const childId = getSessionId(child);
  assert.ok(childId, `child creation failed: ${JSON.stringify(child)}`);
  scenario.sessionIds.push(childId);

  for (const text of ['first run', 'second run', 'third run']) {
    const turn = scenario.turn.start(childId);
    const prompt = await scenario.client.request('POST', `/session/${childId}/prompt_async`, {
      body: { agent: 'coder', parts: [{ type: 'text', text }], model: { providerID: 'test', modelID: 'test-model' } },
    });
    assert.ok(prompt.ok, `child prompt failed: ${JSON.stringify(prompt.data)}`);
    await turn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });
  }

  const childTranscript = await scenario.client.messages(childId);
  const assistantTurns = childTranscript.data.filter((message) => message.info?.role === 'assistant');
  assert.ok(assistantTurns.length >= 3, 'same child must produce three assistant turns');

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Host nudge canary passed: one child completed three independent turns.');
} catch (error) {
  console.error(`Host nudge canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

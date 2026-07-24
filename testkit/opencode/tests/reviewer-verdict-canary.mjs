import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const TREE_FILE = 'review_target.txt';

function toolNames(request) {
  return request.tools?.map((tool) => tool.function?.name || tool.name).filter(Boolean) || [];
}

async function runScenario(scenario) {
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowOutOfOrder();
  scenario.provider.expectToolCall({
    id: 'review-perfect-1',
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'], containsText: ['Review review_target.txt'] },
  });
  scenario.provider.expectToolCall({
    id: 'review-perfect-2',
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'], containsText: ['Review review_target.txt'] },
  });
  scenario.provider.expectText({
    id: 'review-finished',
    text: 'Review confirmed.',
    match: { requiredTools: ['verdict'] },
  });

  const manager = await scenario.client.createSession();
  const managerId = getSessionId(manager);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(manager)}`);
  scenario.sessionIds.push(managerId);

  const child = await scenario.client.request('POST', '/api/session', {
    body: {
      parentID: managerId,
      title: 'reviewer canary',
      agent: 'reviewer',
      model: { providerID: 'test', id: 'test-model' },
    },
  });
  const reviewerId = getSessionId(child);
  assert.ok(reviewerId, `reviewer creation failed: ${JSON.stringify(child)}`);
  scenario.sessionIds.push(reviewerId);

  const turn = scenario.turn.start(reviewerId);
  const prompt = await scenario.client.request('POST', `/session/${reviewerId}/prompt_async`, {
    body: {
      agent: 'reviewer',
      parts: [{ type: 'text', text: `Review ${TREE_FILE} and submit two PERFECT verdicts.` }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `reviewer prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const reviewerRequests = scenario.provider.requests.filter((request) => JSON.stringify(request).includes('Review review_target.txt'));
  assert.ok(reviewerRequests.length >= 3, 'Reviewer must receive two tool results and then finish');
  assert.ok(reviewerRequests.every((request) => toolNames(request).includes('verdict')), 'Reviewer request omitted verdict tool');
  const verdictResults = JSON.stringify(reviewerRequests);
  assert.match(verdictResults, /NEEDS_REVIEW/, 'first PERFECT must require confirmation');
  assert.match(verdictResults, /CONFIRMED/, 'second PERFECT must confirm');

}

if (!runStaticGate([__filename]).passed) {
  throw new Error('Reviewer canary contains a prohibited fixed sleep or polling loop');
}

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { [TREE_FILE]: 'review target\n' } },
    strict: true,
  });
  await runScenario(scenario);
  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Reviewer verdict canary passed: real GitTreePort, Journal, and double PERFECT confirmation.');
} catch (error) {
  console.error(`Reviewer verdict canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

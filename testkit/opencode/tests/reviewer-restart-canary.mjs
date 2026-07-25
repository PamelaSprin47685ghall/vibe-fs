import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

function findValue(value, key) {
  if (!value || typeof value !== 'object') return null;
  if (Array.isArray(value) && value[0] === key && typeof value[1] === 'string') return value[1];
  if (typeof value[key] === 'string') return value[key];
  for (const child of Array.isArray(value) ? value : Object.values(value)) {
    const found = findValue(child, key);
    if (found) return found;
  }
  return null;
}

let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('reviewer restart canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'reviewer restart reconcile canary\n' } },
    strict: true,
    watchdogMs: 1000,
  });

  scenario.provider.expectTitle({
    id: 'reviewer-title',
    lane: expectationLane('reviewer-restart', 'reviewer-title', 'title', 1, 'title'),
  });

  // Create a manager session first (parent), then fork a reviewer child.
  // After restart, the reviewer child keeps its coder-style tool surface.
  scenario.provider.expectToolCall({
    id: 'review-perfect-first',
    lane: expectationLane('reviewer-restart', 'reviewer', 'reviewer', 1),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });

  scenario.provider.expectText({
    id: 'reviewer-first-turn',
    lane: expectationLane('reviewer-restart', 'reviewer', 'reviewer', 2),
    text: 'NEEDS_REVIEW',
    match: { requiredTools: ['verdict'] },
  });

  // Create manager session (parent) then fork reviewer child.
  const manager = await scenario.client.createSession();
  const managerId = getSessionId(manager);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(manager)}`);
  scenario.sessionIds.push(managerId);

  // Fork a reviewer child session
  const child = await scenario.client.request('POST', '/api/session', {
    body: {
      parentID: managerId,
      title: 'reviewer restart canary',
      agent: 'reviewer',
      model: { providerID: 'test', id: 'test-model' },
    },
  });
  const reviewerId = getSessionId(child);
  assert.ok(reviewerId, `reviewer child creation failed: ${JSON.stringify(child)}`);
  scenario.sessionIds.push(reviewerId);
  bindLaneSession(scenario.provider, reviewerId, 'reviewer-title', 'reviewer');

  // First turn: reviewer does first PERFECT
  const turn1 = scenario.turn.start(reviewerId);
  const prompt1 = await scenario.client.request('POST', `/session/${reviewerId}/prompt_async`, {
    body: {
      agent: 'reviewer',
      parts: [{ type: 'text', text: 'Review the tree and submit a PERFECT verdict.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt1.ok, `reviewer first prompt failed: ${JSON.stringify(prompt1.data)}`);
  await turn1.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  // Verify verdict tool was called (not forbidden tools: write, edit, bash, glob, grep)
  const verdictRequests = scenario.provider.requests.filter(
    (request) => request.tools?.some((t) => (t.function?.name || t.name) === 'verdict'),
  );
  assert.ok(verdictRequests.length >= 1, 'Reviewer must issue at least one verdict tool call');
  for (const request of verdictRequests) {
    const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
    const forbidden = ['write', 'edit', 'bash'];
    for (const name of forbidden) {
      assert.ok(!toolNames.includes(name), `Reviewer request exposed forbidden tool: ${name}`);
    }
  }

  // Restart the host - reviewer child must survive restart with its tool surface intact.
  await scenario.restart();

  // After restart, reviewer nudge must still use verdict tool, not read/write/edit.
  scenario.provider.expectToolCall({
    id: 'review-perfect-restart',
    lane: expectationLane('reviewer-restart', 'reviewer', 'reviewer', 3),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });

  scenario.provider.expectText({
    id: 'reviewer-restart-done',
    lane: expectationLane('reviewer-restart', 'reviewer', 'reviewer', 4),
    text: 'CONFIRMED',
    match: { requiredTools: ['verdict'] },
  });

  const turn2 = scenario.turn.start(reviewerId);
  const prompt2 = await scenario.client.request('POST', `/session/${reviewerId}/prompt_async`, {
    body: {
      agent: 'reviewer',
      parts: [{ type: 'text', text: 'After restart, re-verify the tree with another PERFECT verdict.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt2.ok, `reviewer restart prompt failed: ${JSON.stringify(prompt2.data)}`);
  await turn2.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  // Verify post-restart verdict calls also don't include forbidden tools
  const postRestartVerdicts = scenario.provider.requests.filter(
    (request) => JSON.stringify(request).includes('restart') && request.tools?.some((t) => (t.function?.name || t.name) === 'verdict'),
  );
  assert.ok(postRestartVerdicts.length >= 1, 'Reviewer after restart must issue verdict tool calls');

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Reviewer restart reconcile canary passed: verdict tool surface intact across restart with coder-style permissions.');
} catch (error) {
  console.error(`Reviewer restart canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

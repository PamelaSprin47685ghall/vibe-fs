/**
 * companion-canary.mjs — real Manager/Companion projection canary.
 *
 * The scenario deliberately drives the production OpenCode plugin rather than
 * calling Companion directly.  StrictMockProvider keeps the two Manager model
 * turns and their Blogger child turns FIFO/deterministic.
 *
 * Run: node testkit/opencode/tests/companion-canary.mjs
 */

import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const ROLE_PROMPTS = {
  executor: 'Role canary: executor must answer without creating a companion.',
  inspector: 'Role canary: inspector must answer without creating a companion.',
  reviewer: 'Role canary: reviewer must answer without creating a companion.',
};
const BLOGGER_MARKER = 'You are the blogger of a coding agent session.';

function bloggerRequests(provider) {
  return provider.requests.filter((request) => JSON.stringify(request).includes(BLOGGER_MARKER));
}

function sessionCreatedIds(scenario, minSeq = 0) {
  return scenario.events.allEvents
    .filter((event) => event.type === 'session.created' && event.seq > minSeq)
    .map((event) => event.sessionID || event.properties?.sessionID)
    .filter(Boolean);
}

function collectStrings(value, output = []) {
  if (typeof value === 'string') {
    output.push(value);
  } else if (Array.isArray(value)) {
    for (const item of value) collectStrings(item, output);
  } else if (value && typeof value === 'object') {
    for (const item of Object.values(value)) collectStrings(item, output);
  }
  return output;
}

async function assertBloggerTranscript(scenario, childId) {
  const response = await scenario.client.messages(childId);
  assert.ok(response.ok, `failed to read Blogger transcript: ${JSON.stringify(response.data)}`);
  const strings = collectStrings(response.data);
  const exactOutputs = [...new Set(strings.filter((text) => text === 'B1' || text === 'B2'))];
  assert.deepEqual(exactOutputs, ['B1', 'B2'], 'Blogger transcript must contain B1 then B2');
  assert.equal(
    strings.includes('B1\nB2'),
    false,
    'second Blogger output must be B2, not a concatenation containing stale B1',
  );
}

async function runProjectionScenario(scenario) {
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowOutOfOrder();

  // TransformRaw submits Blogger asynchronously and returns the transformed
  // Manager request first; the child request follows it on the same provider.
  scenario.provider.expectText({
    id: 'manager-first',
    text: 'Manager first projection complete.',
    match: {
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'],
    },
  });
  scenario.provider.expectText({
    id: 'blogger-b1',
    text: 'B1',
    match: { containsText: [BLOGGER_MARKER, 'first projection'] },
  });
  scenario.provider.expectText({
    id: 'manager-second',
    text: 'Manager second projection complete.',
    match: {
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'],
    },
  });
  scenario.provider.expectText({
    id: 'blogger-b2',
    text: 'B2',
    match: { containsText: [BLOGGER_MARKER, 'second projection'] },
  });

  const managerResponse = await scenario.client.createSession();
  const managerId = getSessionId(managerResponse);
  assert.ok(managerId, `Manager session creation failed: ${JSON.stringify(managerResponse)}`);
  scenario.sessionIds.push(managerId);

  const firstTurn = scenario.turn.start(managerId);
  const firstPrompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Produce the first projection for Manager X.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(firstPrompt.ok, `first Manager prompt failed: ${JSON.stringify(firstPrompt.data)}`);
  await firstTurn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const firstBlogRequests = bloggerRequests(scenario.provider);
  assert.equal(
    firstBlogRequests.length,
    1,
    'Companion gap: first Manager projection did not emit a real Blogger child request',
  );
  const childIdsAfterFirstProjection = [...new Set(sessionCreatedIds(scenario))].filter((id) => id !== managerId);
  assert.equal(childIdsAfterFirstProjection.length, 1, 'first Manager projection must create exactly one Blogger child session');
  const bloggerId = childIdsAfterFirstProjection[0];
  await scenario.events.awaitEvent(
    (event) => event.type === 'session.idle' && event.sessionID === bloggerId,
    30000,
  );

  const secondTurn = scenario.turn.start(managerId);
  const secondPrompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Produce the second projection for Manager X.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(secondPrompt.ok, `second Manager prompt failed: ${JSON.stringify(secondPrompt.data)}`);
  await secondTurn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const allBlogRequests = bloggerRequests(scenario.provider);
  assert.equal(allBlogRequests.length, 2, 'two Manager projections must produce exactly two Blogger requests');
  const childIdsAfterSecondProjection = [...new Set(sessionCreatedIds(scenario))].filter((id) => id !== managerId);
  assert.deepEqual(
    childIdsAfterSecondProjection,
    [bloggerId],
    'Blogger must be the same child session for both projections',
  );
  await assertBloggerTranscript(scenario, bloggerId);
  scenario.provider.allowOutOfOrder();

  return { managerId, bloggerId };
}

async function assertRoleHasNoSidecar(scenario, role, prompt) {
  const sessionResponse = await scenario.client.request('POST', '/api/session', {
    body: { agent: role, model: { providerID: 'test', id: 'test-model' } },
  });
  const sessionId = getSessionId(sessionResponse);
  assert.ok(sessionId, `${role} session creation failed: ${JSON.stringify(sessionResponse)}`);
  scenario.sessionIds.push(sessionId);

  scenario.provider.expectText({
    id: `role-${role}`,
    text: 'Role complete.',
    match: { containsText: [prompt] },
  });

  const before = scenario.provider.requests.length;
  const bloggerBefore = bloggerRequests(scenario.provider).length;
  const turn = scenario.turn.start(sessionId);
  const response = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: prompt }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(response.ok, `${role} prompt failed: ${JSON.stringify(response.data)}`);
  await turn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const requests = scenario.provider.requests.slice(before);
  assert.ok(requests.length > 0, `${role} produced no provider request`);
  assert.equal(
    bloggerRequests(scenario.provider).length,
    bloggerBefore,
    `${role} must not create a Blogger companion`,
  );

}

function printDiagnostics(scenario) {
  if (!scenario) return;
  console.error('\n── Companion canary provider diagnostics ──');
  console.error(JSON.stringify({ requests: scenario.provider.requests, unexpected: scenario.provider.unexpectedRequests }, null, 2));
  console.error('\n── Companion canary OpenCode events ──');
  console.error(scenario.events.dump(200));
  if (scenario.host?.stdoutLog) console.error(`\n── host stdout ──\n${scenario.host.stdoutLog.slice(-5000)}`);
  if (scenario.host?.stderrLog) console.error(`\n── host stderr ──\n${scenario.host.stderrLog.slice(-5000)}`);
}

const staticResult = runStaticGate([__filename]);
if (!staticResult.passed) {
  console.error('Companion canary static gate failed:', JSON.stringify(staticResult.violations, null, 2));
  process.exit(1);
}

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': '- companion projection canary\n' } },
    strict: true,
    watchdogMs: 30000,
  });

  await runProjectionScenario(scenario);
  for (const [role, prompt] of Object.entries(ROLE_PROMPTS)) {
    await assertRoleHasNoSidecar(scenario, role, prompt);
  }
  scenario.provider.expectSatisfied();
  console.log('Companion projection canary passed: same Blogger child, B1/B2 replacement, and no forbidden role sidecars.');
  await teardownScenario(scenario);
} catch (error) {
  console.error(`Companion projection canary failed: ${error.stack || error.message}`);
  printDiagnostics(scenario);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch (cleanupError) {
      console.error(`Companion canary cleanup failed: ${cleanupError.message}`);
    }
  }
  process.exit(1);
}

import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const BLOGGER_MARKER = 'You are the blogger of a coding agent session.';

function bloggerRequests(provider) {
  return provider.requests.filter((body) => {
    const msgs = body.messages || [];
    return msgs.some((m) => typeof m.content === 'string' && m.content.includes(BLOGGER_MARKER));
  });
}

function sessionCreatedIds(scenario) {
  return scenario.events.allEvents
    .filter((e) => e.type === 'session.created')
    .map((e) => e.sessionID)
    .filter(Boolean);
}

async function assertBloggerTranscript(scenario, bloggerId) {
  const res = await scenario.client.messages(bloggerId);
  assert.ok(res.ok, `failed to fetch Blogger messages: ${JSON.stringify(res.data)}`);
  const transcript = JSON.stringify(res.data);
  assert.ok(transcript.includes('Blogger paragraph.') || transcript.includes('B1'), `Blogger transcript missing paragraph: ${transcript}`);
}

async function runProjectionScenario(scenario) {
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowOutOfOrder();

  scenario.provider.expectText({
    id: 'manager-first',
    text: 'Manager first projection complete.',
    match: {
      containsText: ['first projection'],
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'],
    },
  });
  scenario.provider.expectText({
    id: 'blogger-b1',
    text: 'B1',
    match: { containsText: [BLOGGER_MARKER] },
  });
  scenario.provider.expectText({
    id: 'manager-second',
    text: 'Manager second projection complete.',
    match: {
      containsText: ['second projection'],
      requiredTools: ['fork', 'join', 'list'],
      forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'],
    },
  });
  scenario.provider.expectText({
    id: 'blogger-b2',
    text: 'B2',
    match: { containsText: [BLOGGER_MARKER] },
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
  await firstTurn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const firstBlogRequests = bloggerRequests(scenario.provider);
  assert.ok(
    firstBlogRequests.length >= 1,
    'Companion gap: first Manager projection did not emit a real Blogger child request',
  );
  const childIdsAfterFirstProjection = [...new Set(sessionCreatedIds(scenario))].filter((id) => id !== managerId);
  assert.equal(childIdsAfterFirstProjection.length, 1, 'first Manager projection must create exactly one Blogger child session');
  const bloggerId = childIdsAfterFirstProjection[0];
  await scenario.events.awaitEvent(
    (event) => event.type === 'session.idle' && event.sessionID === bloggerId,
    1000,
  );

  const secondSeqBefore = scenario.events.lastSeq;
  const secondTurn = scenario.turn.start(managerId);
  const secondPrompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Produce the second projection for Manager X.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(secondPrompt.ok, `second Manager prompt failed: ${JSON.stringify(secondPrompt.data)}`);
  await secondTurn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  await scenario.events.awaitEvent(
    (event) => event.seq > secondSeqBefore && event.type === 'session.idle' && event.sessionID === bloggerId,
    1000,
  );

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
  await turn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const requests = scenario.provider.requests.slice(before);
  assert.ok(requests.length > 0, `${role} produced no provider request`);
  assert.equal(
    bloggerRequests(scenario.provider).length,
    bloggerBefore,
    `${role} must not create a Blogger companion`,
  );
}

const ROLE_PROMPTS = {
  executor: 'Role canary: executor must answer without creating a companion.',
  inspector: 'Role canary: inspector must answer without creating a companion.',
  reviewer: 'Role canary: reviewer must answer without creating a companion.',
};

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
    watchdogMs: 1000,
  });

  await runProjectionScenario(scenario);
  for (const [role, prompt] of Object.entries(ROLE_PROMPTS)) {
    await assertRoleHasNoSidecar(scenario, role, prompt);
  }
  scenario.provider.expectSatisfied();
  console.log('Companion projection canary passed: same Blogger child, B1/B2 accumulation, and no forbidden role sidecars.');
  await teardownScenario(scenario);
} catch (error) {
  console.error(`Companion projection canary failed: ${error.stack || error}`);
  printDiagnostics(scenario);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

import assert from 'node:assert/strict';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import { compileScenario } from '../support/scenario-schema.js';
import { ScenarioRuntime } from '../support/scenario-runtime.js';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../support/index.js';
import { WATCHDOG_TIMEOUT_MS } from '../support/time-budget.js';
import { bindLaneSession } from '../support/lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const primaryAgent = 'fast-orchestrator';
const primaryTools = ['fork-manager', 'join'];
const forbiddenPrimaryTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'list', 'verdict'];

function bloggerRequests(provider, bloggerId) {
  return provider.requests.filter((body) => body.sessionID === bloggerId);
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
  const messages = res?.data?.messages ?? res?.data?.data ?? res?.data;
  const list = Array.isArray(messages) ? messages : [];
  // Cycle text lands via blog tool args (not bare assistant prose), so the
  // assertion is the exact args text, never a substring of the whole transcript.
  const blogTexts = [];
  for (const message of list) {
    const parts = message?.parts ?? (Array.isArray(message?.content) ? message.content : []);
    for (const part of parts) {
      if (part?.type !== 'tool' || (part.tool !== 'blog' && part.name !== 'blog')) continue;
      const args = part?.state?.input ?? part?.input ?? part?.args ?? {};
      if (typeof args?.text === 'string') blogTexts.push(args.text);
    }
  }
  assert.ok(
    blogTexts.includes('Blogger paragraph.'),
    `Blogger transcript missing blog tool-call with args.text exactly 'Blogger paragraph.': ${JSON.stringify(res.data).slice(0, 500)}`,
  );
}

async function runProjectionScenario(scenario) {
  const primaryResponse = await scenario.client.request('POST', '/api/session', {
    body: { agent: primaryAgent, model: { providerID: 'test', id: 'test-model' } },
  });
  const primaryId = getSessionId(primaryResponse);
  assert.ok(primaryId, `primary session creation failed: ${JSON.stringify(primaryResponse)}`);
  scenario.sessionIds.push(primaryId);
  bindLaneSession(scenario.provider, primaryId, 'primary-title', 'primary');

  const firstTurn = scenario.turn.start(primaryId);
  const firstPrompt = await scenario.client.request('POST', `/session/${primaryId}/prompt_async`, {
    body: {
      agent: primaryAgent,
      parts: [{ type: 'text', text: 'Produce the first projection for Orchestrator X.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(firstPrompt.ok, `first primary prompt failed: ${JSON.stringify(firstPrompt.data)}`);
  await firstTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const childIdsAfterFirstProjection = [...new Set(sessionCreatedIds(scenario))].filter((id) => id !== primaryId);
  assert.equal(childIdsAfterFirstProjection.length, 1, 'first primary projection must create exactly one Blogger child session');
  const bloggerId = childIdsAfterFirstProjection[0];
  // The Blogger parks after the blog tool-call instead of going idle (a
  // production behaviour; no clause names the park). Do not wait for
  // session.idle on the Blogger — a parked waiter keeps the child non-idle
  // until the next main offer. Provider request count is the observable.
  const deadline1 = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (Date.now() < deadline1 && bloggerRequests(scenario.provider, bloggerId).length < 1) {
    await new Promise((r) => setTimeout(r, 50));
  }
  const firstBlogRequests = bloggerRequests(scenario.provider, bloggerId);
  assert.ok(
    firstBlogRequests.length >= 1,
    'Companion gap: first primary projection did not emit a real Blogger child request',
  );
  scenario.watchdog?.advance({ reason: 'primary-blogger-request-1', lane: 'primary-blogger', blocking: true });

  const secondTurn = scenario.turn.start(primaryId);
  const secondPrompt = await scenario.client.request('POST', `/session/${primaryId}/prompt_async`, {
    body: {
      agent: primaryAgent,
      parts: [{ type: 'text', text: 'Produce the second projection for Orchestrator X.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(secondPrompt.ok, `second primary prompt failed: ${JSON.stringify(secondPrompt.data)}`);
  await secondTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  // Second main offer resumes parked Blogger → second provider request.
  const deadline2 = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (Date.now() < deadline2 && bloggerRequests(scenario.provider, bloggerId).length < 2) {
    await new Promise((r) => setTimeout(r, 50));
  }
  scenario.watchdog?.advance({ reason: 'primary-blogger-request-2', lane: 'primary-blogger', blocking: true });

  const allBlogRequests = bloggerRequests(scenario.provider, bloggerId);
  assert.equal(allBlogRequests.length, 2, 'two primary projections must produce exactly two Blogger requests');
  const childIdsAfterSecondProjection = [...new Set(sessionCreatedIds(scenario))].filter((id) => id !== primaryId);
  assert.deepEqual(
    childIdsAfterSecondProjection,
    [bloggerId],
    'Blogger must be the same child session for both projections',
  );
  await assertBloggerTranscript(scenario, bloggerId);

  const primaryRequests = scenario.provider.requests.filter(
    (request) => request.sessionID === primaryId && Array.isArray(request.tools) && request.tools.length > 0,
  );
  assert.equal(primaryRequests.length, 2, 'two primary projections must produce two tool-bearing requests');
  for (const request of primaryRequests) {
    const tools = request.tools.map((tool) => tool?.function?.name ?? tool?.name);
    assert.ok(primaryTools.every((tool) => tools.includes(tool)), 'primary tool schema must include orchestrator tools');
    assert.ok(forbiddenPrimaryTools.every((tool) => !tools.includes(tool)), 'primary tool schema must exclude forbidden tools');
  }

  return { primaryId, bloggerId };
}

async function assertRoleHasNoSidecar(scenario, bloggerId, role, prompt) {
  const sessionResponse = await scenario.client.request('POST', '/api/session', {
    body: { agent: `fast-${role}`, model: { providerID: 'test', id: 'test-model' } },
  });
  const sessionId = getSessionId(sessionResponse);
  assert.ok(sessionId, `${role} session creation failed: ${JSON.stringify(sessionResponse)}`);
  scenario.sessionIds.push(sessionId);
  bindLaneSession(scenario.provider, sessionId, `role-${role}-title`, `role-${role}`);

  const before = scenario.provider.requests.length;
  const bloggerBefore = bloggerRequests(scenario.provider, bloggerId).length;
  const turn = scenario.turn.start(sessionId);
  const response = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      agent: `fast-${role}`,
      parts: [{ type: 'text', text: prompt }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(response.ok, `${role} prompt failed: ${JSON.stringify(response.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const requests = scenario.provider.requests.slice(before);
  assert.ok(requests.length > 0, `${role} produced no provider request`);
  assert.equal(
    bloggerRequests(scenario.provider, bloggerId).length,
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
  });

  const source = fs.readFileSync(new URL('../scenarios/companion-projection.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'companion-projection.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));
  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);

  const { bloggerId } = await runProjectionScenario(scenario);
  for (const [role, prompt] of Object.entries(ROLE_PROMPTS)) {
    await assertRoleHasNoSidecar(scenario, bloggerId, role, prompt);
  }
  assert.deepEqual(runtime.unanswered(), [], 'all non-internal scenario steps must complete');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');
  assert.equal(scenario.provider.unexpectedRequests.length, 0, 'scenario must not receive unexpected provider requests');
  console.log('Companion projection canary passed: same Blogger child, Blogger accumulation, and no forbidden role sidecars.');
  await teardownScenario(scenario);
} catch (error) {
  console.error(`Companion projection canary failed: ${error.stack || error}`);
  printDiagnostics(scenario);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

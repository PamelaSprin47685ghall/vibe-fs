import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

/**
 * Orchestrator tool-surface canary.
 *
 * Production Orchestrator.fork(manager) runs the full ManagerJob publish chain
 * (worktree → manager → finalize → reverify ×2 before candidate → rebase →
 * reverify ×2 → ff-only). This canary therefore expects the real reviewer
 * rounds, not a fake "manager child completed" text-only path.
 *
 * Full Git assertions live in orchestrator-publish-canary.mjs.
 */
let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('orchestrator canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: {
      files: {
        'AGENTS.md': '- orchestrator role-surface canary\n',
        'README.md': '# orchestrator-canary project\n',
      },
    },
    strict: true,
  });

  scenario.provider.expectTitle({
    id: 'orchestrator-title',
    lane: expectationLane('orchestrator', 'title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'orchestrator-fork-manager',
    lane: expectationLane('orchestrator', 'orchestrator', 'orchestrator', 1),
    tool: 'fork',
    args: { agent: 'manager', prompt: 'Ship role_surface_proof.txt to the target branch.' },
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'list'] },
  });
  scenario.provider.expectToolCall({
    id: 'orchestrator-join-result',
    lane: expectationLane('orchestrator', 'orchestrator', 'orchestrator', 2),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  scenario.provider.expectText({
    id: 'orchestrator-published',
    lane: expectationLane('orchestrator', 'orchestrator', 'orchestrator', 3),
    text: 'Orchestrator joined the Manager child.',
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  scenario.provider.expectText({
    id: 'orchestrator-blogger',
    lane: expectationLane('orchestrator', 'orchestrator-blogger', 'blogger', 1, 'chat', 'orchestrator'),
    blocking: false,
    text: 'Orchestrator background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'] },
  });
  scenario.provider.expectText({
    id: 'orchestrator-blogger-final',
    lane: expectationLane('orchestrator', 'orchestrator-blogger', 'blogger', 2, 'chat', 'orchestrator'),
    neverEnd: true,
    text: 'Orchestrator final background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'] },
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
    lane: expectationLane('orchestrator', 'manager', 'manager', 1, 'chat', 'orchestrator'),
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Write role_surface_proof.txt.' },
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectToolCall({
    id: 'manager-join-coder',
    lane: expectationLane('orchestrator', 'manager', 'manager', 2, 'chat', 'orchestrator'),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectText({
    id: 'manager-terminal',
    lane: expectationLane('orchestrator', 'manager', 'manager', 3, 'chat', 'orchestrator'),
    text: 'Manager finished.',
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('orchestrator', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    neverEnd: true,
    text: 'Manager job background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
  });

  scenario.provider.expectToolCall({
    id: 'coder-write',
    lane: expectationLane('orchestrator', 'coder', 'coder', 1, 'chat', 'manager'),
    tool: 'write',
    args: { filePath: 'role_surface_proof.txt', content: 'Role surface canary\n' },
    match: { requiredTools: ['write'] },
  });
  scenario.provider.expectText({
    id: 'coder-terminal',
    lane: expectationLane('orchestrator', 'coder', 'coder', 2, 'chat', 'manager'),
    text: 'Coder finished.',
  });
  scenario.provider.expectText({
    id: 'coder-blogger',
    lane: expectationLane('orchestrator', 'coder-blogger', 'blogger', 1, 'chat', 'coder'),
    neverEnd: true,
    text: 'Coder background.',
  });

  // JoinPublished runs reverifyTwice once per review phase: pre-rebase
  // (barrier "pre-rebase") and post-rebase (barrier "post-rebase"). Each
  // phase emits ReviewBarrierStarted (resetting the guard) then performs the
  // double-PERFECT check: 2 distinct PERFECT verdicts on the same tree.
  // Total: 2 + 2 = 4 reviewer PERFECT verdicts (no 3rd redundant call).
  for (const [n, label] of [[1, 'one'], [2, 'two'], [3, 'three'], [4, 'four']]) {
    scenario.provider.expectToolCall({
      id: `reviewer-perfect-${n}`,
      lane: expectationLane('orchestrator', 'reviewer', 'reviewer', n * 2 - 1, 'chat', 'orchestrator'),
      tool: 'verdict',
      args: { verdict: 'PERFECT' },
      match: { requiredTools: ['verdict'] },
    });
    scenario.provider.expectText({
      id: `reviewer-terminal-${n}`,
      lane: expectationLane('orchestrator', 'reviewer', 'reviewer', n * 2, 'chat', 'orchestrator'),
      text: `Review round ${label} done.`,
    });
  }

  const orchestrator = await scenario.client.createSession();
  const orchestratorId = getSessionId(orchestrator);
  assert.ok(orchestratorId, `orchestrator session creation failed: ${JSON.stringify(orchestrator)}`);
  scenario.sessionIds.push(orchestratorId);
  bindLaneSession(scenario.provider, orchestratorId, 'title', 'orchestrator');

  const prompt = await scenario.client.request('POST', `/session/${orchestratorId}/prompt_async`, {
    body: {
      agent: 'orchestrator',
      parts: [{ type: 'text', text: 'Ship role_surface_proof.txt to the target branch.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `orchestrator prompt failed: ${JSON.stringify(prompt.data)}`);

  await scenario.provider.waitForExpectation('orchestrator-fork-manager', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('manager-fork-coder', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('coder-write', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('manager-terminal', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('reviewer-perfect-1', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('reviewer-perfect-2', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('reviewer-perfect-3', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('reviewer-perfect-4', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('orchestrator-published', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('orchestrator-blogger-final', WATCHDOG_TIMEOUT_MS);

  const orchestratorRequests = scenario.provider.requests.filter((request) => {
    const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
    return toolNames.length > 0 && !toolNames.includes('list') && toolNames.every((n) => ['fork', 'join'].includes(n));
  });
  assert.ok(orchestratorRequests.length >= 2, 'Orchestrator must issue at least fork and join tool calls');
  for (const request of orchestratorRequests) {
    const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
    for (const name of ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'list']) {
      assert.ok(!toolNames.includes(name), `Orchestrator request exposed forbidden tool: ${name}`);
    }
  }

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Orchestrator tool-surface canary passed: real Host ManagerJob publish chain via fork/join only.');
} catch (error) {
  console.error(`Orchestrator canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

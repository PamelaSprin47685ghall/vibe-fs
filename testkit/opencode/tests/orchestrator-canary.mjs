import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

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
    watchdogMs: 1000,
  });

  scenario.provider.expectTitle({
    id: 'orchestrator-title',
    lane: expectationLane('orchestrator', 'title', 'title', 1, 'title'),
  });

  // Orchestrator must only expose fork/join in its tool surface
  // and must never expose forbidden file/process/verdict tools.
  scenario.provider.expectToolCall({
    id: 'orchestrator-fork-manager',
    lane: expectationLane('orchestrator', 'orchestrator', 'orchestrator', 1),
    tool: 'fork',
    args: { agent: 'manager', prompt: 'Run the isolated Manager child task.' },
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'list'] },
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

  scenario.provider.expectText({
    id: 'manager-job-done',
    lane: expectationLane('orchestrator', 'manager', 'manager', 1, 'chat', 'orchestrator'),
    text: 'Manager child completed.',
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectText({
    id: 'manager-zwsp',
    lane: expectationLane('orchestrator', 'orchestrator-blogger', 'synthetic', 1, 'synthetic'),
    text: 'done',
    match: { containsText: ['\u200B'] },
  });
  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('orchestrator', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    text: 'Manager job background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
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

  const orchestrator = await scenario.client.createSession();
  const orchestratorId = getSessionId(orchestrator);
  assert.ok(orchestratorId, `orchestrator session creation failed: ${JSON.stringify(orchestrator)}`);
  scenario.sessionIds.push(orchestratorId);
  bindLaneSession(scenario.provider, orchestratorId, 'title', 'orchestrator');

  const prompt = await scenario.client.request('POST', `/session/${orchestratorId}/prompt_async`, {
    body: {
      agent: 'orchestrator',
      parts: [{ type: 'text', text: 'Run the role-surface cycle: fork and join a Manager child.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `orchestrator prompt failed: ${JSON.stringify(prompt.data)}`);
  await scenario.provider.waitForExpectation('orchestrator-fork-manager', 1000);
  const managerCreated = await scenario.events.awaitEvent(
    (event) => event.type === 'session.created' && event.sessionID !== orchestratorId,
    1000,
  );
  scenario.watchdog?.advance({
    reason: 'manager-job-session-created',
    lane: `session:${managerCreated.sessionID}`,
    blocking: true,
  });
  await scenario.provider.waitForExpectation('manager-job-done', 1000);
  await scenario.provider.waitForExpectation('orchestrator-published', 1000);
  await scenario.provider.waitForExpectation('orchestrator-blogger-final', 1000);

  const orchestratorRequests = scenario.provider.requests.filter(
    (request) => {
      const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
      return toolNames.length > 0 && !toolNames.includes('list') && toolNames.every((n) => ['fork', 'join'].includes(n));
    },
  );
  assert.ok(orchestratorRequests.length >= 2, 'Orchestrator must issue at least fork and join tool calls');
  for (const request of orchestratorRequests) {
    const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
    const forbidden = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'list'];
    for (const name of forbidden) {
      assert.ok(!toolNames.includes(name), `Orchestrator request exposed forbidden tool: ${name}`);
    }
  }

  // Verify the orchestrator tool surface has exactly fork/join
  const orchestratorToolNames = Object.keys(scenario.provider.toolCalls || {});
  const expectedTools = new Set(['fork', 'join']);
  for (const name of orchestratorToolNames) {
    assert.ok(expectedTools.has(name), `Orchestrator exposed unexpected tool: ${name}`);
  }

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Orchestrator tool-surface canary passed: real Host fork/join delegation only; no Git publish claim.');
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

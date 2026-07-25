import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('orchestrator canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: {
      files: {
        'AGENTS.md': '- orchestrator durable port canary\n',
        'README.md': '# orchestrator-canary project\n',
      },
    },
    strict: true,
    watchdogMs: 30000,
  });

  scenario.provider.allowTitleGeneration();
  scenario.provider.allowOutOfOrder();
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowBloggerRequests();

  // Orchestrator must only expose fork/join/list in its tool surface
  // and must never expose forbidden file/process/verdict tools.
  scenario.provider.expectToolCall({
    id: 'orchestrator-worktree-fork',
    tool: 'fork',
    args: { agent: 'manager', prompt: /deploy|worktree|ManagerJob|Manager/i },
    match: { requiredTools: ['fork', 'join', 'list'], forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'] },
  });

  scenario.provider.expectToolCall({
    id: 'orchestrator-list-runners',
    tool: 'list',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });

  scenario.provider.expectToolCall({
    id: 'orchestrator-join-result',
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });

  scenario.provider.expectText({
    id: 'orchestrator-published',
    text: /published|Published|ff-only|deployed|complete/i,
    match: { requiredTools: ['fork', 'join', 'list'] },
  });

  const orchestrator = await scenario.client.createSession();
  const orchestratorId = getSessionId(orchestrator);
  assert.ok(orchestratorId, `orchestrator session creation failed: ${JSON.stringify(orchestrator)}`);
  scenario.sessionIds.push(orchestratorId);

  const turn = scenario.turn.start(orchestratorId);
  const prompt = await scenario.client.request('POST', `/session/${orchestratorId}/prompt_async`, {
    body: {
      agent: 'orchestrator',
      parts: [{ type: 'text', text: 'Run orchestrator cycle: fork a ManagerJob, join the Manager, and confirm the ff-only publish.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `orchestrator prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const orchestratorRequests = scenario.provider.requests.filter(
    (request) => {
      const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
      return toolNames.some((n) => ['fork', 'join', 'list'].includes(n));
    },
  );
  assert.ok(orchestratorRequests.length >= 2, 'Orchestrator must issue at least fork and join tool calls');
  for (const request of orchestratorRequests) {
    const toolNames = request.tools?.map((t) => t.function?.name || t.name).filter(Boolean) || [];
    const forbidden = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];
    for (const name of forbidden) {
      assert.ok(!toolNames.includes(name), `Orchestrator request exposed forbidden tool: ${name}`);
    }
  }

  // Verify the orchestrator tool surface has exactly fork/join/list
  const orchestratorToolNames = Object.keys(scenario.provider.toolCalls || {});
  const expectedTools = new Set(['fork', 'join', 'list']);
  for (const name of orchestratorToolNames) {
    assert.ok(expectedTools.has(name), `Orchestrator exposed unexpected tool: ${name}`);
  }

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Orchestrator canary passed: Manager worktree fork, join, and publish cycle through real OpenCode Host.');
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

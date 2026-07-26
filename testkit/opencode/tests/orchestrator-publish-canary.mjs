import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('orchestrator publish canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: {
      files: {
        'AGENTS.md': '- orchestrator publish canary\n',
        'README.md': '# orchestrator-publish project\n',
      },
    },
    strict: true,
  });

  scenario.provider.expectTitle({
    id: 'orch-title',
    lane: expectationLane('orch-publish', 'title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'orch-fork-manager',
    lane: expectationLane('orch-publish', 'orchestrator', 'orchestrator', 1),
    tool: 'fork',
    args: { agent: 'manager', prompt: 'Ship publish_proof.txt to the target branch.' },
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  scenario.provider.expectToolCall({
    id: 'orch-join',
    lane: expectationLane('orch-publish', 'orchestrator', 'orchestrator', 2),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  scenario.provider.expectText({
    id: 'orch-final',
    lane: expectationLane('orch-publish', 'orchestrator', 'orchestrator', 3),
    text: 'Publish completed.',
    match: { requiredTools: ['fork', 'join'], forbiddenTools: ['list'] },
  });
  scenario.provider.expectText({
    id: 'orch-blogger',
    lane: expectationLane('orch-publish', 'orchestrator-blogger', 'blogger', 1, 'chat', 'orchestrator'),
    blocking: false,
    text: 'Orchestrator background.',
  });
  scenario.provider.expectText({
    id: 'orch-blogger-final',
    lane: expectationLane('orch-publish', 'orchestrator-blogger', 'blogger', 2, 'chat', 'orchestrator'),
    neverEnd: true,
    text: 'Orchestrator final background.',
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
    lane: expectationLane('orch-publish', 'manager', 'manager', 1, 'chat', 'orchestrator'),
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Write publish_proof.txt.' },
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectToolCall({
    id: 'manager-join-coder',
    lane: expectationLane('orch-publish', 'manager', 'manager', 2, 'chat', 'orchestrator'),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectText({
    id: 'manager-terminal',
    lane: expectationLane('orch-publish', 'manager', 'manager', 3, 'chat', 'orchestrator'),
    text: 'Manager finished.',
    match: { requiredTools: ['fork', 'join', 'list'] },
  });
  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('orch-publish', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    neverEnd: true,
    text: 'Manager background.',
  });

  scenario.provider.expectToolCall({
    id: 'coder-write',
    lane: expectationLane('orch-publish', 'coder', 'coder', 1, 'chat', 'manager'),
    tool: 'write',
    args: { filePath: 'publish_proof.txt', content: 'Published by orchestrator canary\n' },
    match: { requiredTools: ['write'] },
  });
  scenario.provider.expectText({
    id: 'coder-terminal',
    lane: expectationLane('orch-publish', 'coder', 'coder', 2, 'chat', 'manager'),
    text: 'Coder finished.',
  });
  scenario.provider.expectText({
    id: 'coder-blogger',
    lane: expectationLane('orch-publish', 'coder-blogger', 'blogger', 1, 'chat', 'coder'),
    neverEnd: true,
    text: 'Coder background.',
  });

  scenario.provider.expectToolCall({
    id: 'reviewer-perfect-1',
    lane: expectationLane('orch-publish', 'reviewer', 'reviewer', 1, 'chat', 'orchestrator'),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectText({
    id: 'reviewer-terminal-1',
    lane: expectationLane('orch-publish', 'reviewer', 'reviewer', 2, 'chat', 'orchestrator'),
    text: 'Review round one done.',
  });
  scenario.provider.expectToolCall({
    id: 'reviewer-perfect-2',
    lane: expectationLane('orch-publish', 'reviewer', 'reviewer', 3, 'chat', 'orchestrator'),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectText({
    id: 'reviewer-terminal-2',
    lane: expectationLane('orch-publish', 'reviewer', 'reviewer', 4, 'chat', 'orchestrator'),
    text: 'Review round two done.',
  });

  const orchestrator = await scenario.client.createSession();
  const orchestratorId = getSessionId(orchestrator);
  assert.ok(orchestratorId, `orchestrator session creation failed: ${JSON.stringify(orchestrator)}`);
  scenario.sessionIds.push(orchestratorId);
  bindLaneSession(scenario.provider, orchestratorId, 'title', 'orchestrator');

  const prompt = await scenario.client.request('POST', `/session/${orchestratorId}/prompt_async`, {
    body: {
      agent: 'orchestrator',
      parts: [{ type: 'text', text: 'Ship publish_proof.txt to the target branch.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `orchestrator prompt failed: ${JSON.stringify(prompt.data)}`);

  await scenario.provider.waitForExpectation('orch-fork-manager', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('manager-fork-coder', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('coder-write', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('manager-terminal', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('reviewer-perfect-1', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('reviewer-perfect-2', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('orch-final', WATCHDOG_TIMEOUT_MS);
  scenario.provider.expectSatisfied();

  const workDir = scenario.fs.workDir;
  const git = (args) => execFileSync('git', args, { cwd: workDir, encoding: 'utf8' });
  const log = git(['log', '--format=%s', 'HEAD']);
  assert.ok(log.includes('candidate:'), `target branch must contain the candidate commit, got: ${log}`);
  const proof = fs.readFileSync(path.join(workDir, 'publish_proof.txt'), 'utf8');
  assert.equal(proof, 'Published by orchestrator canary\n');
  const worktrees = git(['worktree', 'list', '--porcelain']);
  const extra = worktrees.split('\n').filter((line) => line.startsWith('worktree ') && !line.includes(workDir));
  assert.equal(extra.length, 0, `worktree must be cleaned up after publish, got: ${worktrees}`);
  assert.ok(!git(['status', '--porcelain']).trim(), 'main worktree must be clean after ff-only publish');

  await teardownScenario(scenario);
  console.log('Orchestrator publish canary passed: worktree -> candidate -> double PERFECT -> rebase -> ff-only -> cleanup.');
} catch (error) {
  console.error(`Orchestrator publish canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  const orchReqs = (scenario?.provider?.requests || []).filter((r) => Array.isArray(r.tools) && r.tools.some((t) => (t.function?.name || t.name) === 'fork'));
  for (const r of orchReqs) {
    for (const m of r.messages || []) {
      if (m.role === 'tool') console.error(`TOOL-RESULT: ${JSON.stringify(m).slice(0, 600)}`);
    }
  }
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

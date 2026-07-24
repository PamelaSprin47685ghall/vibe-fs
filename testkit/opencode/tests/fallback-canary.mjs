/**
 * fallback-canary.mjs — A/B Fallback durable failure recording and recovery.
 *
 * Proves:
 * 1. Provider failure (500) on a child session records FallbackFailureRecorded
 *    in the NDJSON journal via HostEventRouter.
 * 2. After host restart, fallback state is recovered from journal (Boot fold).
 * 3. HostForkRuntime switches child prompt model from A to B after failure.
 *
 * Run: node testkit/opencode/tests/fallback-canary.mjs
 */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];

function journalLines(workDir) {
  const runtimeDir = path.join(workDir, '.wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return [];
  const lines = [];
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    for (const line of fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n')) {
      if (line.trim()) lines.push(JSON.parse(line));
    }
  }
  return lines;
}

function hasFallbackFact(workDir) {
  return journalLines(workDir).some((entry) => {
    const fact = entry?.Fact?.Item2 || entry?.Fact;
    if (!fact) return false;
    const tag = Array.isArray(fact) ? fact[0] : fact?.tag || fact?.Case;
    return tag === 'FallbackFailureRecorded' || JSON.stringify(fact).includes('FallbackFailureRecorded');
  });
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true, 'static gate');

  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'fallback canary\n' } },
    strict: true,
    watchdogMs: 60000,
    extraEnv: {
      WANXIANGSHU_MODEL_A: 'test/test-model',
      WANXIANGSHU_MODEL_B: 'test/test-model-b',
    },
  });
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowBloggerRequests();
  scenario.provider.allowOutOfOrder();

  // Phase 1: Manager forks Coder; Coder provider fails with 500.
  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Write fallback-test.txt with exactly fallback-ok.' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  // Coder child's first provider request fails.
  scenario.provider.expectError({
    id: 'coder-first-fail',
    status: 500,
    body: { error: { message: 'mock provider failure', type: 'server_error' } },
    match: { requiredTools: ['write'] },
  });

  // Manager joins and receives the error.
  scenario.provider.expectToolCall({
    id: 'manager-join-error',
    tool: 'join',
    args: {},
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  scenario.provider.expectText({
    id: 'manager-phase1-done',
    text: 'Coder failed, will retry.',
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);

  const turn1 = scenario.turn.start(parentId);
  const prompt1 = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Fork a coder to write fallback-test.txt, then join and report.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt1.ok, `manager prompt failed: ${JSON.stringify(prompt1.data)}`);
  await turn1.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  // Verify journal recorded the fallback failure.
  assert.ok(hasFallbackFact(scenario.host.workDir), 'journal must contain FallbackFailureRecorded after provider 500');

  // Find the child session for phase 2.
  const children = await scenario.client.request('GET', `/session/${parentId}/children`);
  const childList = Array.isArray(children.data) ? children.data : children.data?.data || [];
  const childId = getSessionId(childList[0]);
  assert.ok(childId, `child session not found: ${JSON.stringify(children.data)}`);
  scenario.sessionIds.push(childId);

  // Phase 2: Restart host, then nudge the same Coder child.
  await scenario.restart();

  // After restart, HostForkRuntime restores child from journal.
  // Fallback state (1 failure on SideA) is recovered → model resolves to SideB.
  scenario.provider.expectToolCall({
    id: 'manager-nudge-coder',
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Retry: write fallback-test.txt with exactly fallback-ok.' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  scenario.provider.expectToolCall({
    id: 'coder-write-ok',
    tool: 'write',
    args: { filePath: 'fallback-test.txt', content: 'fallback-ok.' },
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectText({
    id: 'coder-terminal',
    text: 'Coder wrote fallback-test.txt.',
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectToolCall({
    id: 'manager-join-ok',
    tool: 'join',
    args: {},
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  scenario.provider.expectText({
    id: 'manager-phase2-done',
    text: 'Fallback canary complete.',
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const childTurn2 = scenario.turn.start(childId);
  const parentTurn2 = scenario.turn.start(parentId);
  const prompt2 = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Nudge the coder to retry writing fallback-test.txt.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt2.ok, `manager nudge failed: ${JSON.stringify(prompt2.data)}`);

  await Promise.all([
    childTurn2.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true }),
    parentTurn2.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true }),
  ]);

  scenario.fs.expectFileContent('fallback-test.txt', 'fallback-ok.');

  // Verify the Coder child's second request used model-b (fallback switched).
  const coderRequests = scenario.provider.requests.filter(
    (r) => JSON.stringify(r).includes('fallback-test.txt') || JSON.stringify(r).includes('Retry: write'),
  );
  const usedModelB = coderRequests.some((r) => {
    const model = r?.body?.model || r?.model;
    return typeof model === 'string' ? model.includes('test-model-b') : model?.modelID === 'test-model-b';
  });
  assert.ok(usedModelB, `Coder child must use model-b after fallback; requests: ${JSON.stringify(coderRequests.map((r) => r?.body?.model || r?.model))}`);

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Fallback canary passed: provider failure recorded, restart recovered, model switched A→B.');
} catch (error) {
  console.error(`Fallback canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(`unexpected: ${JSON.stringify(scenario.provider.unexpectedRequests)}`);
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];

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

function journalValue(workDir, key) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return null;
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    const fullPath = path.join(runtimeDir, file);
    if (!fs.statSync(fullPath).isFile()) continue;
    const lines = fs.readFileSync(fullPath, 'utf8').split('\n');
    for (const line of lines) {
      if (!line.trim()) continue;
      const found = findValue(JSON.parse(line), key);
      if (found) return found;
    }
  }
  return null;
}

function childrenOf(response) {
  return Array.isArray(response.data) ? response.data : response.data?.data || [];
}

async function nudge(parentId, childId, agentId, marker, managerMarker, scenario) {
  scenario.provider.expectToolCall({
    id: `${marker}-nudge`,
    lane: expectationLane('host-restart', 'manager', 'manager', 3),
    tool: 'fork',
    args: { agent: agentId, prompt: `Report ${marker}.` },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-2',
    lane: expectationLane('host-restart', 'manager-blogger-restarted', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    text: 'Manager restart background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
  });
  scenario.provider.expectText({
    id: `${marker}-child`,
    lane: expectationLane('host-restart', 'coder', 'coder', 2, 'chat', 'manager'),
    text: `${marker}.`,
    match: { containsText: [marker] },
  });
  scenario.provider.expectText({
    id: 'coder-blogger-2',
    lane: expectationLane('host-restart', 'coder-blogger-restarted', 'blogger', 1, 'chat', 'coder'),
    blocking: false,
    text: 'Coder restart background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });
  scenario.provider.expectText({
    id: managerMarker,
    lane: expectationLane('host-restart', 'manager', 'manager', 4),
    text: `${managerMarker} complete.`,
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const parentTurn = scenario.turn.start(parentId);
  const childTurn = scenario.turn.start(childId);
  const response = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: `Nudge ${agentId} for ${marker}.` }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(response.ok, `manager nudge failed: ${JSON.stringify(response.data)}`);
  await parentTurn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: false });
  await childTurn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  const parentTurnFinal = scenario.turn.start(parentId, { afterSeq: parentTurn.terminalSeq });
  await parentTurnFinal.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'restart reconcile canary\n' } }, strict: true, watchdogMs: 1000 });

  scenario.provider.expectTitle({
    id: 'parent-title',
    lane: expectationLane('host-restart', 'parent-title', 'title', 1, 'title'),
    text: 'E2E Test Session',
  });
  scenario.provider.expectText({
    id: 'manager-zwsp-initial',
    lane: expectationLane('host-restart', 'manager-blogger-initial', 'synthetic', 1, 'synthetic'),
    text: 'done',
    match: { containsText: ['\u200B'] },
  });
  scenario.provider.expectText({
    id: 'manager-zwsp-restarted',
    lane: expectationLane('host-restart', 'manager-blogger-restarted', 'synthetic', 1, 'synthetic', 'manager'),
    text: 'done',
    match: { containsText: ['\u200B'] },
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
    lane: expectationLane('host-restart', 'manager', 'manager', 1),
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Report child-a1.' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-1',
    lane: expectationLane('host-restart', 'manager-blogger-initial', 'blogger', 1, 'chat', 'manager'),
    text: 'Manager created background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
  });
  scenario.provider.expectText({
    id: 'child-a1-text',
    lane: expectationLane('host-restart', 'coder', 'coder', 1, 'chat', 'manager'),
    text: 'child-a1.',
    match: { containsText: ['child-a1'] },
  });
  scenario.provider.expectText({
    id: 'coder-blogger-1',
    lane: expectationLane('host-restart', 'coder-blogger-initial', 'blogger', 1, 'chat', 'coder'),
    text: 'Coder created background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });
  scenario.provider.expectText({
    id: 'manager-created',
    lane: expectationLane('host-restart', 'manager', 'manager', 2),
    text: 'manager-created complete.',
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  bindLaneSession(scenario.provider, parentId, 'parent-title', 'manager');
  const firstTurn = scenario.turn.start(parentId);
  const firstPrompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Create the child and report manager-created.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(firstPrompt.ok, `manager create failed: ${JSON.stringify(firstPrompt.data)}`);
  await firstTurn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: false });

  const messages = await scenario.client.messages(parentId);
  const messageJson = JSON.stringify(messages.data);
  const agentId = findValue(messages.data, 'agentId') || messageJson.match(/agentId[^a-z0-9]+([a-z0-9]{6})/i)?.[1];
  assert.ok(agentId, `fork result did not expose agentId: ${messageJson}`);
  const children = await scenario.client.request('GET', `/session/${parentId}/children`);
  const childId = getSessionId(childrenOf(children)[0]) || journalValue(scenario.host.workDir, 'ChildId');
  assert.ok(childId, `child session was not recoverable: ${JSON.stringify(children.data)}`);
  scenario.sessionIds.push(childId);

  await scenario.turn.start(childId, { afterSeq: firstTurn.eventSeqBefore }).awaitTerminal({
    timeoutMs: 1000,
    requireActivity: true,
    requireAssistantTerminal: false,
    requireIdleAfterActivity: true,
  });

  await scenario.turn.start(parentId, { afterSeq: firstTurn.terminalSeq }).awaitTerminal({
    timeoutMs: 1000,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  await Promise.all([
    scenario.provider.waitForExpectation('manager-blogger-1', 1000),
    scenario.provider.waitForExpectation('coder-blogger-1', 1000),
  ]);

  await scenario.restart();
  await nudge(parentId, childId, agentId, 'child-a2', 'manager-a2', scenario);

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Host restart reconcile canary passed: restored child retained coder tools across restart.');
} catch (error) {
  console.error(`Host restart reconcile canary failed: ${error.stack || error}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

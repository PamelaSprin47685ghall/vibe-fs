import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
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
    lane: expectationLane('host-restart', 'manager', 'manager', 7),
    tool: 'fork',
    args: { agent: agentId, prompt: `Report ${marker}.` },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-2',
    lane: expectationLane('host-restart', 'manager-blogger-restarted', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    neverEnd: true,
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
    neverEnd: true,
    text: 'Coder restart background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });
  scenario.provider.expectText({
    id: managerMarker,
    lane: expectationLane('host-restart', 'manager', 'manager', 8),
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
  await parentTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: false });
  await childTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  const parentTurnFinal = scenario.turn.start(parentId, { afterSeq: parentTurn.terminalSeq });
  await parentTurnFinal.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  await Promise.all([
    scenario.provider.waitForExpectation('manager-blogger-2', WATCHDOG_TIMEOUT_MS),
    scenario.provider.waitForExpectation('coder-blogger-2', WATCHDOG_TIMEOUT_MS),
  ]);
  scenario.watchdog?.advance({ reason: 'restarted-blogger-sidecars', lane: 'restart', blocking: true });
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'restart reconcile canary\n' } }, strict: true });

  scenario.provider.expectTitle({
    id: 'parent-title',
    lane: expectationLane('host-restart', 'parent-title', 'title', 1, 'title'),
    text: 'E2E Test Session',
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
    blocking: false,
    neverEnd: true,
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
    blocking: false,
    neverEnd: true,
    text: 'Coder created background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });
  scenario.provider.expectToolCall({
    id: 'manager-join-coder',
    lane: expectationLane('host-restart', 'manager', 'manager', 2),
    tool: 'join',
    args: {},
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-created',
    lane: expectationLane('host-restart', 'manager', 'manager', 3),
    text: 'manager-created complete.',
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  // ReviewGuard nudges the first unconfirmed manager terminal (turn-2). The
  // manager answers by forking a Reviewer; two distinct PERFECTs confirm the
  // tree durably, so every later terminal (including post-restart) evaluates
  // ReviewGuardConfirmed and no further guard fires.
  scenario.provider.expectToolCall({
    id: 'manager-fork-reviewer',
    lane: expectationLane('host-restart', 'manager', 'manager', 4),
    tool: 'fork',
    args: { agent: 'reviewer', prompt: 'Review the current tree.' },
    match: {
      requiredTools: managerTools,
      forbiddenTools: forbiddenManagerTools,
      containsText: ['Review is required before completion.'],
    },
  });
  scenario.provider.expectToolCall({
    id: 'reviewer-perfect-1',
    lane: expectationLane('host-restart', 'reviewer', 'reviewer', 1, 'chat', 'manager'),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectToolCall({
    id: 'reviewer-perfect-2',
    lane: expectationLane('host-restart', 'reviewer', 'reviewer', 2, 'chat', 'manager'),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectText({
    id: 'reviewer-terminal',
    lane: expectationLane('host-restart', 'reviewer', 'reviewer', 3, 'chat', 'manager'),
    text: 'Review confirmed.',
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectToolCall({
    id: 'manager-join-reviewer',
    lane: expectationLane('host-restart', 'manager', 'manager', 5),
    tool: 'join',
    args: {},
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-reviewed',
    lane: expectationLane('host-restart', 'manager', 'manager', 6),
    text: 'Review confirmed before restart.',
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
  await firstTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: false });

  const messages = await scenario.client.messages(parentId);
  const messageJson = JSON.stringify(messages.data);
  const agentId = findValue(messages.data, 'agentId') || messageJson.match(/agentId[^a-z0-9]+([a-z0-9]{6})/i)?.[1];
  assert.ok(agentId, `fork result did not expose agentId: ${messageJson}`);
  const children = await scenario.client.request('GET', `/session/${parentId}/children`);
  // Managers now also own a blogger sidecar child; positional selection is not
  // stable. The coding child is the non-blogger one, and raw children entries
  // carry their session id directly (getSessionId expects API envelopes).
  const coderChild = childrenOf(children).find((child) => (child.agent || child.title) === 'coder');
  const childId = coderChild?.id || journalValue(scenario.host.workDir, 'ChildId');
  assert.ok(childId, `child session was not recoverable: ${JSON.stringify(children.data)}`);
  scenario.sessionIds.push(childId);

  await scenario.turn.start(childId, { afterSeq: firstTurn.eventSeqBefore }).awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: false,
    requireIdleAfterActivity: true,
  });

  await scenario.turn.start(parentId, { afterSeq: firstTurn.terminalSeq }).awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  await Promise.all([
    scenario.provider.waitForExpectation('manager-blogger-1', WATCHDOG_TIMEOUT_MS),
    scenario.provider.waitForExpectation('coder-blogger-1', WATCHDOG_TIMEOUT_MS),
  ]);
  scenario.watchdog?.advance({ reason: 'initial-blogger-sidecars', lane: 'initial', blocking: true });

  await scenario.provider.waitForExpectation('manager-fork-reviewer', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'manager-forked-reviewer', lane: 'manager', blocking: true });
  await scenario.provider.waitForExpectation('reviewer-perfect-1', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'reviewer-perfect-1', lane: 'reviewer', blocking: true });
  await scenario.provider.waitForExpectation('reviewer-perfect-2', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'reviewer-perfect-2', lane: 'reviewer', blocking: true });
  await scenario.provider.waitForExpectation('reviewer-terminal', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'reviewer-terminal', lane: 'reviewer', blocking: true });
  await scenario.provider.waitForExpectation('manager-join-reviewer', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'manager-joined-reviewer', lane: 'manager', blocking: true });
  await scenario.provider.waitForExpectation('manager-reviewed', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'review-confirmed', lane: 'manager', blocking: true });

  await scenario.restart();
  await nudge(parentId, childId, agentId, 'child-a2', 'manager-a2', scenario);

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Host restart reconcile canary passed: restored child retained coder tools across restart.');
} catch (error) {
  console.error(`Host restart reconcile canary failed: ${error.stack || error}`);
  console.error(`sessions: ${JSON.stringify(scenario?.sessionIds || [])}`);
  for (const e of (scenario?.events?.allEvents || []).filter((e) => e.type.startsWith('session') || e.type === 'message.updated').slice(-40)) {
    console.error(`EV seq=${e.seq} ${e.type} session=${e.sessionID || e.properties?.sessionID || '-'} ${e.finishReason || (typeof e.status === 'object' ? e.status?.type : e.status) || ''}`);
  }
  for (const r of scenario?.provider?.requests || []) {
    const msgs = r.body?.messages || r.messages || [];
    const last = msgs.filter((m) => m.role === 'user').pop();
    const text = typeof last?.content === 'string' ? last.content : JSON.stringify(last?.content || '');
    console.error(`REQ session=${(r.sessionID || '').slice(-6)} tools=${(r.body?.tools || r.tools || []).map((t) => t.function?.name || t.name).join(',')} lastUser=${text.replace(/\n/g, ' ').slice(0, 80)}`);
  }
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

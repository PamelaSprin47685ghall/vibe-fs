import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const TREE_FILE = 'review_target.txt';
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];
const bloggerMarker = 'You are the blogger of a coding agent session.';
const reviewerPrompt = `Review ${TREE_FILE} and submit two PERFECT verdicts.`;

function toolNames(request) {
  return request.tools?.map((tool) => tool.function?.name || tool.name).filter(Boolean) || [];
}

function reviewFacts(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return [];
  return fs.readdirSync(runtimeDir)
    .filter((file) => file.endsWith('.ndjson'))
    .flatMap((file) => fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n'))
    .filter(Boolean)
    .map((line) => JSON.parse(line))
    .filter((fact) => JSON.stringify(fact).includes('ReviewVerdictRecorded'));
}

function guardFacts(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return [];
  return fs.readdirSync(runtimeDir)
    .filter((file) => file.endsWith('.ndjson'))
    .flatMap((file) => fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n'))
    .filter(Boolean)
    .map((line) => JSON.parse(line))
    .filter((fact) => JSON.stringify(fact).includes('GuardPromptAccepted'));
}

function valuesOf(value, fieldName, values = []) {
  if (!value || typeof value !== 'object') return values;
  if (Array.isArray(value)) {
    for (const item of value) valuesOf(item, fieldName, values);
    return values;
  }
  for (const [key, child] of Object.entries(value)) {
    if (key.toLowerCase() === fieldName.toLowerCase() && typeof child === 'string') values.push(child);
    valuesOf(child, fieldName, values);
  }
  return values;
}

async function runScenario(scenario) {
  scenario.provider.expectTitle({
    id: 'manager-title',
    lane: expectationLane('reviewer-verdict', 'manager-title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork-reviewer',
    lane: expectationLane('reviewer-verdict', 'manager', 'manager', 1),
    tool: 'fork',
    args: { agent: 'reviewer', prompt: reviewerPrompt },
    match: {
      requiredTools: managerTools,
      forbiddenTools: forbiddenManagerTools,
      containsText: ['Fork a Reviewer'],
    },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-first',
    lane: expectationLane('reviewer-verdict', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    text: 'Manager review background.',
    match: { containsText: [bloggerMarker, '"agent":"manager"'] },
  });
  scenario.provider.expectToolCall({
    id: 'manager-join-reviewer',
    lane: expectationLane('reviewer-verdict', 'manager', 'manager', 2),
    tool: 'join',
    args: {},
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-final',
    lane: expectationLane('reviewer-verdict', 'manager-blogger', 'blogger', 2, 'chat', 'manager'),
    blocking: false,
    neverEnd: true,
    text: 'Manager final review background.',
    match: { containsText: [bloggerMarker, '"agent":"manager"'] },
  });
  scenario.provider.expectToolCall({
    id: 'review-perfect-1',
    lane: expectationLane('reviewer-verdict', 'reviewer', 'reviewer', 1, 'chat', 'manager'),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectToolCall({
    id: 'review-perfect-2',
    lane: expectationLane('reviewer-verdict', 'reviewer', 'reviewer', 2, 'chat', 'manager'),
    tool: 'verdict',
    args: { verdict: 'PERFECT' },
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectText({
    id: 'review-finished',
    lane: expectationLane('reviewer-verdict', 'reviewer', 'reviewer', 3, 'chat', 'manager'),
    text: 'Review confirmed.',
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectText({
    id: 'manager-review-complete',
    lane: expectationLane('reviewer-verdict', 'manager', 'manager', 3),
    text: 'Reviewer joined and confirmed.',
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  const manager = await scenario.client.createSession();
  const managerId = getSessionId(manager);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(manager)}`);
  scenario.sessionIds.push(managerId);
  bindLaneSession(scenario.provider, managerId, 'manager-title', 'manager');

  const turn = scenario.turn.start(managerId);
  const prompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: `Fork a Reviewer for ${TREE_FILE}, join it after two PERFECT verdicts, then report.` }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `manager prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const reviewerRequests = scenario.provider.requests.filter((request) => toolNames(request).includes('verdict'));
  assert.ok(reviewerRequests.length >= 3, 'Reviewer must submit two verdicts then finish');
  assert.ok(reviewerRequests.every((request) => toolNames(request).includes('verdict')), 'Reviewer request omitted verdict tool');

  const facts = reviewFacts(scenario.host.workDir);
  assert.ok(!fs.existsSync(path.join(scenario.host.workDir, '.wanxiangshu-next')), 'Journal must not dirty the workspace');
  assert.equal(facts.length, 2, `two distinct PERFECT verdict facts required: ${JSON.stringify(facts)}`);
  assert.ok(facts.every((fact) => JSON.stringify(fact).includes('Perfect')), 'both persisted facts must be PERFECT');
  assert.equal(new Set(valuesOf(facts, 'ToolCallId')).size, 2, 'two verdict facts require distinct tool call IDs');
  assert.equal(new Set(valuesOf(facts, 'GitTreeHash')).size, 1, 'double PERFECT must bind one tree hash');

  scenario.provider.expectTitle({
    id: 'guard-manager-title',
    lane: expectationLane('reviewer-verdict', 'guard-manager-title', 'title', 1, 'title'),
  });
  scenario.provider.expectText({
    id: 'guard-manager-first',
    lane: expectationLane('reviewer-verdict', 'guard-manager', 'manager', 1),
    text: 'Manager attempted completion without review.',
    match: {
      containsText: ['Attempt completion without review.'],
      requiredTools: managerTools,
      forbiddenTools: forbiddenManagerTools,
    },
  });
  scenario.provider.expectText({
    id: 'guard-manager-blogger',
    lane: expectationLane('reviewer-verdict', 'guard-manager-blogger', 'blogger', 1, 'chat', 'guard-manager'),
    blocking: false,
    neverEnd: true,
    text: 'Manager guard background remains busy.',
    match: { containsText: [bloggerMarker, '"agent":"manager"'] },
  });
  scenario.provider.expectText({
    id: 'guard-manager-nudged',
    lane: expectationLane('reviewer-verdict', 'guard-manager', 'manager', 2),
    blocking: false,
    neverEnd: true,
    text: 'Manager received the review guard.',
    match: {
      containsText: ['Review is required before completion.'],
      requiredTools: managerTools,
      forbiddenTools: forbiddenManagerTools,
    },
  });

  const guardManager = await scenario.client.request('POST', '/api/session', {
    body: { agent: 'manager', model: { providerID: 'test', id: 'test-model' } },
  });
  const guardManagerId = getSessionId(guardManager);
  assert.ok(guardManagerId, `guard manager creation failed: ${JSON.stringify(guardManager)}`);
  scenario.sessionIds.push(guardManagerId);
  bindLaneSession(scenario.provider, guardManagerId, 'guard-manager-title', 'guard-manager');

  const guardTurn = scenario.turn.start(guardManagerId);
  const guardPrompt = await scenario.client.request('POST', `/session/${guardManagerId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Attempt completion without review.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(guardPrompt.ok, `guard manager prompt failed: ${JSON.stringify(guardPrompt.data)}`);
  await guardTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });
  scenario.watchdog?.advance({
    reason: 'manager-review-guard-terminal',
    lane: `session:${guardManagerId}`,
    blocking: true,
  });
  await scenario.provider.waitForExpectation('guard-manager-nudged', WATCHDOG_TIMEOUT_MS);
  await scenario.provider.waitForExpectation('guard-manager-blogger', WATCHDOG_TIMEOUT_MS);
  const guards = guardFacts(scenario.host.workDir);
  assert.equal(guards.length, 1, `missing durable Manager guard acceptance: ${JSON.stringify(guards)}`);

  scenario.provider.expectTitle({
    id: 'reviewer-nudge-title',
    lane: expectationLane('reviewer-verdict', 'reviewer-nudge-title', 'title', 1, 'title'),
  });
  scenario.provider.expectText({
    id: 'reviewer-without-verdict',
    lane: expectationLane('reviewer-verdict', 'reviewer-nudge', 'reviewer', 1),
    text: 'I reviewed the tree but omitted the structured verdict.',
    match: { requiredTools: ['verdict'] },
  });
  scenario.provider.expectText({
    id: 'reviewer-nudged',
    lane: expectationLane('reviewer-verdict', 'reviewer-nudge', 'reviewer', 2),
    neverEnd: true,
    text: 'The structured verdict prompt was received.',
    match: {
      containsText: ['Submit a structured verdict with the verdict tool'],
      requiredTools: ['verdict'],
    },
  });

  const nudgeReviewer = await scenario.client.request('POST', '/api/session', {
    body: { agent: 'reviewer', model: { providerID: 'test', id: 'test-model' } },
  });
  const nudgeReviewerId = getSessionId(nudgeReviewer);
  assert.ok(nudgeReviewerId, `nudge reviewer creation failed: ${JSON.stringify(nudgeReviewer)}`);
  scenario.sessionIds.push(nudgeReviewerId);
  bindLaneSession(scenario.provider, nudgeReviewerId, 'reviewer-nudge-title', 'reviewer-nudge');

  const nudgeTurn = scenario.turn.start(nudgeReviewerId);
  const nudgePrompt = await scenario.client.request('POST', `/session/${nudgeReviewerId}/prompt_async`, {
    body: {
      agent: 'reviewer',
      parts: [{ type: 'text', text: 'Review the tree but do not submit a verdict.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(nudgePrompt.ok, `reviewer nudge prompt failed: ${JSON.stringify(nudgePrompt.data)}`);
  await nudgeTurn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });
  scenario.watchdog?.advance({
    reason: 'reviewer-terminal-without-verdict',
    lane: `session:${nudgeReviewerId}`,
    blocking: true,
  });
  await scenario.provider.waitForExpectation('reviewer-nudged', WATCHDOG_TIMEOUT_MS);
}

if (!runStaticGate([__filename]).passed) {
  throw new Error('Reviewer canary contains a prohibited fixed sleep or polling loop');
}

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { [TREE_FILE]: 'review target\n' } },
    strict: true,

  });
  await runScenario(scenario);
  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Reviewer verdict canary passed: double PERFECT and durable Manager ReviewGuard nudge.');
} catch (error) {
  console.error(`Reviewer verdict canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.events?.allEvents) {
    console.error(JSON.stringify(scenario.events.allEvents.slice(-30).map((event) => ({
      type: event.type,
      sessionID: event.sessionID,
      sessionAgent: event.sessionAgent,
      properties: event.properties,
    })), null, 2));
  }
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

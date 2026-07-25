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
  await turn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const reviewerRequests = scenario.provider.requests.filter((request) => toolNames(request).includes('verdict'));
  assert.ok(reviewerRequests.length >= 3, 'Reviewer must submit two verdicts then finish');
  assert.ok(reviewerRequests.every((request) => toolNames(request).includes('verdict')), 'Reviewer request omitted verdict tool');

  const facts = reviewFacts(scenario.host.workDir);
  assert.ok(!fs.existsSync(path.join(scenario.host.workDir, '.wanxiangshu-next')), 'Journal must not dirty the workspace');
  assert.equal(facts.length, 2, `two distinct PERFECT verdict facts required: ${JSON.stringify(facts)}`);
  assert.ok(facts.every((fact) => JSON.stringify(fact).includes('Perfect')), 'both persisted facts must be PERFECT');
  assert.equal(new Set(valuesOf(facts, 'ToolCallId')).size, 2, 'two verdict facts require distinct tool call IDs');
  assert.equal(new Set(valuesOf(facts, 'GitTreeHash')).size, 1, 'double PERFECT must bind one tree hash');
}

if (!runStaticGate([__filename]).passed) {
  throw new Error('Reviewer canary contains a prohibited fixed sleep or polling loop');
}

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { [TREE_FILE]: 'review target\n' } },
    strict: true,
    watchdogMs: 1000,
  });
  await runScenario(scenario);
  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Reviewer verdict canary passed: Manager fork/join, two persisted PERFECT facts, and one Git tree hash.');
} catch (error) {
  console.error(`Reviewer verdict canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}

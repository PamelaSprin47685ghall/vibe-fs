/**
 * temporal-ownership-unhappy-path — EXEC-017 / HOST-004 / REVIEW-002 one-stroke proof.
 *
 * Given: an active Manager join and one physically incomplete linked child.
 * Trigger: external user_message → fresh join → Esc → replacement child →
 * normal drain → prose-only Reviewer → PERFECT → challenge → PERFECT.
 * Expected observable effect: user_message preserves the first child until Esc;
 * Esc cancels it; join C reports ParentCancelled and join D drains the replacement;
 * one durable Reviewer confirmation follows.
 * Forbidden observable effect: user-message cancellation, stale-message wake,
 * bare "#" repair, duplicate Reviewer continuation, or duplicate confirmation.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { awaitCausalObservation, runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';
import { FORK_COMPLETION_WINDOW_MS } from '../support/time-budget.js';

const __filename = fileURLToPath(import.meta.url);
const WAKE_PROMPT = 'The first join was interrupted; start a fresh join and keep waiting for the same child.';

let releaseHeldChild = null;

const contentText = (content) =>
  Array.isArray(content)
    ? content.map((part) => part?.text ?? '').join('')
    : String(content ?? '');

const lastUserText = (request) => {
  const user = (request?.messages ?? []).filter((message) => message?.role === 'user').at(-1);
  return contentText(user?.content);
};

const requestText = (request) =>
  (request?.messages ?? []).map((message) => contentText(message?.content)).join('\n');

const toolName = (call) => call?.function?.name ?? call?.name;

function responseMessages(response) {
  const payload = response?.data?.data?.data ?? response?.data?.data ?? response?.data;
  if (Array.isArray(payload)) return payload;
  return Array.isArray(payload?.messages) ? payload.messages : [];
}

function toolParts(messages, expectedName) {
  return messages.flatMap((message) =>
    (message?.parts ?? []).filter(
      (part) => part?.type === 'tool' && (part?.tool === expectedName || part?.name === expectedName),
    ),
  );
}

const partStatus = (part) => part?.state?.status ?? part?.status ?? part?.state ?? 'missing';

const partOutput = (part) => {
  const output = part?.state?.output ?? part?.output ?? part?.result;
  return typeof output === 'string' ? output : output == null ? '' : JSON.stringify(output);
};

async function readJoinSnapshot(scenario, sessionId) {
  const response = await scenario.client.request('GET', `/session/${sessionId}/message`);
  assert.equal(response?.ok, true, `session message read failed: ${JSON.stringify(response?.data)}`);
  const joins = toolParts(responseMessages(response), 'join');
  return {
    joins,
    token: joins.map((part) => `${partStatus(part)}:${partOutput(part)}`).join('|'),
  };
}

function installChildHold(scenario) {
  const runtime = scenario.provider._scenario;
  assert.ok(runtime?.scenario?.entries, 'strict scenario entries required');

  const hold = new Promise((resolve) => {
    releaseHeldChild = resolve;
  });
  const child = runtime.scenario.entries.find((entry) => entry.id === 'child.0');
  assert.ok(child, 'child.0 scenario entry required');
  child.respond = { ...child.respond, waitUntil: hold };
}

async function abortFreshJoin(scenario, ctx) {
  const sessionId = ctx.sessionId;
  assert.ok(sessionId, 'Manager session required');

  await awaitCausalObservation({
    scenario,
    id: 'fresh-join-running',
    reason: 'fresh-join-running',
    lane: `session:${sessionId}`,
    timeoutMs: FORK_COMPLETION_WINDOW_MS,
    read: () => readJoinSnapshot(scenario, sessionId),
    token: (snapshot) => snapshot.token,
    ready: (snapshot) => {
      const second = snapshot.joins[1];
      const output = partOutput(second);
      assert.equal(
        output.includes('reason = "user_message"'),
        false,
        'join B inherited join A user_message before Esc',
      );
      return snapshot.joins.length >= 2 && ['pending', 'running'].includes(partStatus(second));
    },
  });

  const aborted = await scenario.client.abort(sessionId);
  assert.equal(aborted?.ok, true, `Esc request failed: ${JSON.stringify(aborted?.data)}`);
}

async function awaitOperatorAbort(scenario, ctx) {
  const sessionId = ctx.sessionId;
  const snapshot = await awaitCausalObservation({
    scenario,
    id: 'fresh-join-operator-abort',
    reason: 'fresh-join-operator-abort',
    lane: `session:${sessionId}`,
    timeoutMs: FORK_COMPLETION_WINDOW_MS,
    read: () => readJoinSnapshot(scenario, sessionId),
    token: (value) => value.token,
    ready: (value) => partOutput(value.joins[1]).includes('reason = "operator_abort"'),
  });

  assert.match(partOutput(snapshot.joins[1]), /status = "interrupted"/);
}

function releaseChild() {
  assert.equal(typeof releaseHeldChild, 'function', 'held child release capability required');
  releaseHeldChild();
  releaseHeldChild = null;
}

function namedToolResults(requests, expectedName) {
  const callIds = new Set();
  for (const request of requests) {
    for (const message of request?.messages ?? []) {
      if (message?.role !== 'assistant' || !Array.isArray(message?.tool_calls)) continue;
      for (const call of message.tool_calls) {
        if (toolName(call) === expectedName && typeof call?.id === 'string') callIds.add(call.id);
      }
    }
  }

  const results = new Map();
  for (const request of requests) {
    for (const message of request?.messages ?? []) {
      if (message?.role !== 'tool' && message?.role !== 'toolResult') continue;
      const callId = message?.tool_call_id ?? message?.toolCallId;
      if (!callIds.has(callId)) continue;
      results.set(callId, contentText(message?.content));
    }
  }
  return [...results.values()];
}

async function assertJoinTrajectory(scenario, ctx) {
  const managerRequests = scenario.provider.requests.filter(
    (request) => request.sessionID === ctx.sessionId,
  );
  const results = namedToolResults(managerRequests, 'join');

  assert.equal(results.length, 4, `exactly four join attempts required: ${JSON.stringify(results)}`);
  assert.match(results[0], /status = "interrupted"[\s\S]*reason = "user_message"/);
  assert.match(results[1], /status = "interrupted"[\s\S]*reason = "operator_abort"/);
  assert.match(results[2], /status = "completed"/);
  assert.match(results[2], /status = "abandoned"[\s\S]*reason = "ParentCancelled"/);
  assert.doesNotMatch(results[2], /CANCELED_CHILD_MUST_NOT_COMPLETE/);
  assert.match(results[3], /status = "completed"[\s\S]*CHILD_DONE/);

  assert.ok(
    managerRequests.some(
      (request) => requestText(request).includes(WAKE_PROMPT)
        && (request?.tools ?? []).some((tool) => toolName(tool) === 'join'),
    ),
    'the user_message that interrupted join A must be consumed by the next Manager turn',
  );
  assert.equal(
    managerRequests.filter((request) => lastUserText(request).trim() === '#').length,
    0,
    'Esc must produce zero bare # repair prompts',
  );
}

function journalLines(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  const directory = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(directory)) return [];
  return fs
    .readdirSync(directory)
    .filter((file) => file.endsWith('.ndjson'))
    .flatMap((file) =>
      fs
        .readFileSync(path.join(directory, file), 'utf8')
        .split('\n')
        .filter(Boolean)
        .map((line) => JSON.parse(line)),
    );
}

function factPayloads(lines, caseName) {
  const payloads = [];
  const visit = (value) => {
    if (Array.isArray(value)) {
      if (value[0] === caseName) payloads.push(value[1]);
      for (const child of value) visit(child);
    } else if (value && typeof value === 'object') {
      for (const child of Object.values(value)) visit(child);
    }
  };
  for (const line of lines) visit(line.Fact);
  return payloads;
}

async function assertReviewerTrajectory(scenario) {
  const reviewerIds = new Set(
    scenario.provider.requests
      .filter((request) => (request?.tools ?? []).some((tool) => toolName(tool) === 'verdict'))
      .map((request) => request.sessionID)
      .filter(Boolean),
  );
  assert.equal(reviewerIds.size, 1, `exactly one Host-owned Reviewer required: ${[...reviewerIds]}`);
  const reviewerId = [...reviewerIds][0];
  const lines = journalLines(scenario.host.workDir);
  const reviewerClaims = factPayloads(lines, 'PluginPromptClaimed').filter((claim) =>
    JSON.stringify(claim).includes(reviewerId),
  );

  assert.equal(
    reviewerClaims.filter((claim) => claim?.ContinuationKind === 'ReviewerGuard').length,
    1,
    'prose-only terminal must create exactly one verdict guard continuation',
  );
  assert.equal(
    reviewerClaims.filter((claim) => claim?.ContinuationKind === 'ReviewConfirmation').length,
    1,
    'first PERFECT must create exactly one skeptical challenge continuation',
  );

  const reviewerVerdicts = factPayloads(lines, 'ReviewVerdictRecorded').filter((verdict) =>
    JSON.stringify(verdict).includes(reviewerId),
  );
  const witnesses = factPayloads(lines, 'ConfirmedReviewWitness').filter((witness) =>
    JSON.stringify(witness).includes(reviewerId),
  );
  assert.equal(reviewerVerdicts.length, 2, 'two distinct PERFECT facts required');
  assert.equal(witnesses.length, 1, 'second PERFECT must confirm exactly once');

  const reviewerRequests = scenario.provider.requests.filter(
    (request) => request.sessionID === reviewerId,
  );
  const verdictResults = namedToolResults(reviewerRequests, 'verdict');
  assert.equal(verdictResults.length, 2, 'exactly two verdict tool results required');
  assert.equal(
    verdictResults.filter((result) => result.includes("# Nope, let's re-evaluate:")).length,
    1,
    'the first PERFECT must return one skeptical challenge',
  );
  assert.equal(
    verdictResults.filter((result) => result.includes('verdict = "PERFECT"')).length,
    1,
    'the second PERFECT must be accepted exactly once',
  );
}

const customs = {
  holdChild: installChildHold,
  abortFreshJoin,
  awaitOperatorAbort,
  releaseChild,
  assertJoinTrajectory,
  assertReviewerTrajectory,
};

if (!runStaticGate([__filename]).passed) {
  throw new Error('temporal-ownership-unhappy-path canary static gate failed');
}
process.exit(await runCanary('temporal-ownership-unhappy-path', { customs }));

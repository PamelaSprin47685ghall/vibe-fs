/**
 * reviewer-verdict — data-driven. Scenario: scenarios/reviewer-verdict.toml
 * 3 sessions via flow createSession; custom oracle for journal assertions.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';

const __filename = fileURLToPath(import.meta.url);
const TREE_FILE = 'review_target.txt';

function valuesOf(value, fieldName, values = []) {
  if (!value || typeof value !== 'object') return values;
  if (Array.isArray(value)) {
    // Fable union: ["Tag", "value"] where the tag matches the requested field
    // (as a prefix: "ProviderRun" matches "ProviderRunIdentity"). Nested one
    // level deep for wrapped identities:
    // ["ProviderRun",["ProviderRunIdentity","msg_..."]] → "msg_...".
    if (value.length === 2 && typeof value[0] === 'string' && value[0].toLowerCase().includes(fieldName.toLowerCase())) {
      if (typeof value[1] === 'string') values.push(value[1]);
      else if (Array.isArray(value[1]) && value[1].length === 2 && typeof value[1][1] === 'string') values.push(value[1][1]);
      return values;
    }
    for (const item of value) valuesOf(item, fieldName, values);
    return values;
  }
  for (const [key, child] of Object.entries(value)) {
    if (key.toLowerCase() === fieldName.toLowerCase() && typeof child === 'string') values.push(child);
    valuesOf(child, fieldName, values);
  }
  return values;
}

function factsIn(workDir, needle) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const dir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir).filter(f => f.endsWith('.ndjson')).flatMap(f =>
    fs.readFileSync(path.join(dir, f), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l))
  ).filter(fact => JSON.stringify(fact).includes(needle));
}

function toolNames(request) {
  return (request.tools ?? []).map((t) => t?.function?.name).filter(Boolean);
}

function lastUserText(request) {
  const users = (request.messages ?? []).filter((m) => m.role === 'user');
  const last = users.at(-1);
  return typeof last?.content === 'string' ? last.content : '';
}

function uniqueVerdictToolResults(requests) {
  // A tool result counts only when its tool_call id belongs to a verdict
  // assistant call. Other tools' results may embed the challenge sentence or the
  // confirmation text in their transcript tail — the join LWR uncompressed tail
  // (EXEC-004/COMPANION-003) and the suicide FinalityBlessed work-log prompt both
  // carry the reviewer session's own wire — and those are not verdict results.
  const verdictCallIds = new Set();
  for (const request of requests) {
    for (const message of request.messages || []) {
      if (message.role !== 'assistant' || !Array.isArray(message.tool_calls)) continue;
      for (const call of message.tool_calls) {
        if ((call?.function?.name ?? call?.name) === 'verdict' && typeof call?.id === 'string') {
          verdictCallIds.add(call.id);
        }
      }
    }
  }
  const results = new Map();
  for (const request of requests) {
    for (const message of request.messages || []) {
      if (message.role !== 'tool' && message.role !== 'toolResult') continue;
      const id = message.tool_call_id || message.toolCallId;
      if (typeof id === 'string' && !verdictCallIds.has(id)) continue;
      const content = typeof message.content === 'string'
        ? message.content
        : JSON.stringify(message.content || '');
      // Belt for a result whose tool_call id is absent from the wire: never count
      // an opaque LWR (EXEC-004/COMPANION-003) as a verdict shape.
      if (content.includes('work_record')) continue;
      if (!content.includes('Nope, let\'s re-evaluate:') && !content.includes('verdict = "PERFECT"')) continue;
      const key = message.tool_call_id || message.toolCallId || content;
      results.set(key, content);
    }
  }
  return [...results.values()];
}

async function oracleCheck(scenario, ctx, step) {
  // Oracle: the real regression is two provider runs under one physical user
  // root. The old canary ended the first turn, so it never exercised the loop
  // that made every subsequent PERFECT return the skeptical sentence.
  // The three REQUIRED verdict-tool requests are the opening prose envelope
  // (finality-reviewer.0), the durable guard continuation that receives the
  // first PERFECT (reviewer-nudged.0), and its prose follow-up
  // (reviewer-nudged.1). The challenge continuation ("Nope, let's re-evaluate")
  // is excluded by the filter and asserted separately below.
  const reviewerReqs = scenario.provider.requests.filter(
    r => toolNames(r).includes('verdict') && !lastUserText(r).includes("Nope, let's re-evaluate"),
  );
  assert.equal(reviewerReqs.length, 3, 'reviewer must make exactly three verdict-tool requests before REVIEW-004: opening prose, guard PERFECT, guard follow-up');
  const rvFacts = factsIn(scenario.host.workDir, 'ReviewVerdictRecorded');
  assert.ok(!fs.existsSync(path.join(scenario.host.workDir, '.wanxiangshu-next')), 'Journal must not dirty workspace');
  assert.equal(rvFacts.length, 2, 'two distinct PERFECT verdict facts required');
  assert.ok(rvFacts.every(f => JSON.stringify(f).includes('Perfect')), 'both persisted facts must be PERFECT');
  assert.equal(new Set(valuesOf(rvFacts, 'ToolCallId')).size, 2, 'two verdict facts require distinct tool call IDs');
  const verdictRuns = [...new Set(valuesOf(rvFacts, 'ProviderRun'))];
  assert.equal(verdictRuns.length, 2, 'two verdict facts require distinct provider runs');
  // HOST-010 observable proxy (docs/what/host.md transform id ≡ ToolContext.messageID is
  // not co-present on the wire). Both sides land in journal for the seal-bound run:
  //   ReviewVerdictRecorded.ProviderRun = ToolContext.messageID (VerdictTool)
  //   ProviderInputSealed.ProviderRun   = same messageID via ReviewSeal.bindToRun
  // First PERFECT has no pending challenge → no seal; second PERFECT binds.
  // Proof: every sealed run appears among verdict runs (same ToolContext.messageID).
  const sealFacts = factsIn(scenario.host.workDir, 'ProviderInputSealed');
  assert.ok(sealFacts.length >= 1, 'HOST-010: dual PERFECT must seal ≥1 ProviderInputSealed');
  const sealedRuns = [...new Set(valuesOf(sealFacts, 'ProviderRun'))];
  assert.ok(sealedRuns.length >= 1, 'HOST-010: ProviderInputSealed.ProviderRun non-empty');
  for (const run of sealedRuns) {
    assert.ok(
      typeof run === 'string' && run.length > 0,
      `HOST-010: sealed ProviderRun must be non-empty string (got ${JSON.stringify(run)})`,
    );
    assert.ok(
      verdictRuns.includes(run),
      `HOST-010: ProviderInputSealed.ProviderRun ${run} must equal a ReviewVerdictRecorded.ProviderRun (verdict=[${verdictRuns.join(', ')}])`,
    );
  }
  // A prose-only first terminal must continue the SAME reviewer session through
  // the durable verdict guard before the skeptical challenge requests the second
  // PERFECT. The two continuation turns intentionally have different user text.
  // A tool-call message stays in every later request's history, so each verdict
  // call is located by the FIRST request carrying its tool_call id.
  const verdictCallSeen = new Set();
  const firstVerdictCallIdx = [];
  scenario.provider.requests.forEach((request, index) => {
    for (const message of request.messages ?? []) {
      if (message.role !== 'assistant' || !Array.isArray(message.tool_calls)) continue;
      for (const call of message.tool_calls) {
        const name = call?.function?.name ?? call?.name;
        const id = call?.id;
        if (name !== 'verdict' || typeof id !== 'string' || verdictCallSeen.has(id)) continue;
        verdictCallSeen.add(id);
        firstVerdictCallIdx.push(index);
      }
    }
  });
  assert.equal(firstVerdictCallIdx.length, 2, 'dual PERFECT requires exactly two verdict tool calls');
  assert.ok(
    lastUserText(scenario.provider.requests[firstVerdictCallIdx[0]]).startsWith('# Your previous response did not submit a verdict.'),
    'first PERFECT must be submitted inside the durable reviewer guard request',
  );
  assert.ok(
    lastUserText(scenario.provider.requests[firstVerdictCallIdx[1]]).startsWith("# Nope, let's re-evaluate:"),
    'second PERFECT must be submitted inside the skeptical challenge request',
  );
  // Turn attribution by PREFIX, not substring: a work-log blog request embeds
  // the reviewer wire (guard and challenge sentences included) in its data body
  // after the instruction header, so a substring test would mistake that
  // request for a guard continuation.
  const guardUserIdxs = scenario.provider.requests
    .map((request, index) => (lastUserText(request).startsWith('# Your previous response did not submit a verdict.') ? index : -1))
    .filter((index) => index >= 0);
  const firstChallengeUserIdx = scenario.provider.requests.findIndex((request) =>
    lastUserText(request).startsWith("# Nope, let's re-evaluate:"));
  assert.ok(guardUserIdxs.length >= 1 && firstChallengeUserIdx >= 0, 'guard and challenge continuations must both appear on the wire');
  assert.equal(firstChallengeUserIdx, guardUserIdxs.at(-1) + 1, 'skeptical challenge must immediately follow the guard request as adjacent steps');
  assert.equal(new Set(valuesOf(rvFacts, 'GitTreeHash')).size, 1, 'double PERFECT must bind one tree hash');
  assert.equal(factsIn(scenario.host.workDir, 'ConfirmedReviewWitness').length, 1, 'dual PERFECT must produce one durable confirmed witness');

  const verdictResults = uniqueVerdictToolResults(scenario.provider.requests);
  const challengeResults = verdictResults.filter(x => x.includes('# Nope, let\'s re-evaluate:'));
  assert.equal(challengeResults.length, 1, `first recovered PERFECT must request re-evaluation exactly once (got ${JSON.stringify(challengeResults)})`);
  // REVIEW-003: only the FIRST PERFECT receives the skeptical challenge; the
  // second PERFECT is confirmed and reports `verdict = "PERFECT"` (VerdictTool.fs
  // Confirmed branch). The REVIEW-010 seal fallback still reports its final
  // decision once — a repeated confirmation report would mean the same verdict
  // was accepted twice.
  const acceptedResults = verdictResults.filter(x => x.includes('verdict = "PERFECT"'));
  assert.equal(acceptedResults.length, 1, `second PERFECT must be accepted exactly once (got ${JSON.stringify(acceptedResults)})`);
}

if (!runStaticGate([__filename]).passed) process.exit(1);
process.exit(await runCanary('reviewer-verdict', { customs: { oracle: oracleCheck } }));

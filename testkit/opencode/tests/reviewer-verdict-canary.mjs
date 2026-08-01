/**
 * reviewer-verdict-canary — data-driven. Scenario: scripts/reviewer-verdict.toml
 * 3 sessions via flow createSession; custom oracle for journal assertions.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';
import { runCanary } from '../canary-driver.mjs';

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
  const results = new Map();
  for (const request of requests) {
    for (const message of request.messages || []) {
      if (message.role !== 'tool' && message.role !== 'toolResult') continue;
      const content = typeof message.content === 'string'
        ? message.content
        : JSON.stringify(message.content || '');
      if (!content.includes('Nope, let\'s re-evaluate:') && !content.includes('PERFECT recorded for the current tree.')) continue;
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
  // A REVIEW-003 challenge nudge (production-composed "Nope, let's re-evaluate"
  // user message) may arrive as a redundant follow-up after the confirmation
  // sealed; the three REQUIRED requests are envelope-1, envelope-2 (second
  // PERFECT) and the terminal prose.
  const reviewerReqs = scenario.provider.requests.filter(
    r => toolNames(r).includes('verdict') && !lastUserText(r).includes("Nope, let's re-evaluate"),
  );
  assert.equal(reviewerReqs.length, 3, 'reviewer must make exactly two verdict calls and one terminal follow-up');
  const rvFacts = factsIn(scenario.host.workDir, 'ReviewVerdictRecorded');
  assert.ok(!fs.existsSync(path.join(scenario.host.workDir, '.wanxiangshu-next')), 'Journal must not dirty workspace');
  assert.equal(rvFacts.length, 2, 'two distinct PERFECT verdict facts required');
  assert.ok(rvFacts.every(f => JSON.stringify(f).includes('Perfect')), 'both persisted facts must be PERFECT');
  assert.equal(new Set(valuesOf(rvFacts, 'ToolCallId')).size, 2, 'two verdict facts require distinct tool call IDs');
  assert.equal(new Set(valuesOf(rvFacts, 'ProviderRun')).size, 2, 'two verdict facts require distinct provider runs');
  // Both PERFECT verdicts are accepted inside the SAME physical user turn
  // (the second request reuses the envelope's user message): the reviewer's
  // verdict-bearing requests all carry the same last user text.
  const verdictReqUsers = scenario.provider.requests
    .filter(r => toolNames(r).includes('verdict'))
    .map(r => lastUserText(r).slice(0, 40));
  assert.equal(new Set(verdictReqUsers.filter(t => !t.includes("Nope, let's re-evaluate"))).size, 1, 'second PERFECT must be accepted in the same physical user turn');
  assert.equal(new Set(valuesOf(rvFacts, 'GitTreeHash')).size, 1, 'double PERFECT must bind one tree hash');
  assert.equal(factsIn(scenario.host.workDir, 'ConfirmedReviewWitness').length, 1, 'dual PERFECT must produce one durable confirmed witness');

  const verdictResults = uniqueVerdictToolResults(scenario.provider.requests);
  assert.equal(verdictResults.filter(x => x.includes('Nope, let\'s re-evaluate:')).length, 1, 'only the first PERFECT may request re-evaluation');
  // The confirmation's report ("PERFECT recorded for the current tree.") may
  // land on a later request when the REVIEW-010 seal fallback re-submits; the
  // durable proof is the ConfirmedReviewWitness asserted above.
  assert.ok(verdictResults.filter(x => x.includes('PERFECT recorded for the current tree.')).length <= 1, 'second PERFECT must be accepted at most once');
}

if (!runStaticGate([__filename]).passed) process.exit(1);
process.exit(await runCanary('reviewer-verdict', { customs: { oracle: oracleCheck } }));

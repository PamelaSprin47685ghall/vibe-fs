/**
 * reviewer-verdict-canary — data-driven. Script: scripts/reviewer-verdict.json
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
  if (Array.isArray(value)) { for (const item of value) valuesOf(item, fieldName, values); return values; }
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
  return request.tools?.map(t => t.function?.name || t.name).filter(Boolean) || [];
}

async function oracleCheck(scenario, ctx, step) {
  // Oracle: review facts
  const reviewerReqs = scenario.provider.requests.filter(r => toolNames(r).includes('verdict'));
  assert.ok(reviewerReqs.length >= 3, 'Reviewer must submit two verdicts then finish');
  const rvFacts = factsIn(scenario.host.workDir, 'ReviewVerdictRecorded');
  assert.ok(!fs.existsSync(path.join(scenario.host.workDir, '.wanxiangshu-next')), 'Journal must not dirty workspace');
  assert.equal(rvFacts.length, 2, 'two distinct PERFECT verdict facts required');
  assert.ok(rvFacts.every(f => JSON.stringify(f).includes('Perfect')), 'both persisted facts must be PERFECT');
  assert.equal(new Set(valuesOf(rvFacts, 'ToolCallId')).size, 2, 'two verdict facts require distinct tool call IDs');
  assert.equal(new Set(valuesOf(rvFacts, 'GitTreeHash')).size, 1, 'double PERFECT must bind one tree hash');

  const guards = factsIn(scenario.host.workDir, 'GuardPromptAccepted');
  assert.equal(guards.length, 1, 'missing durable Manager guard acceptance');
}

if (!runStaticGate([__filename]).passed) process.exit(1);
process.exit(await runCanary('reviewer-verdict.json', { customs: { oracle: oracleCheck } }));

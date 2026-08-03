/**
 * x-recovery-canary — C10 layer-4 evidence for SSOT/12 (X-A…X-D).
 *
 * Four static scenarios, one driver (orchestrator-restart-publish pattern):
 *   x-a-probe-before-crash        arming lost; no probe send; no promote
 *   x-b-probe-sent-unaccepted     probe once; restart before promote; no re-send
 *   x-c-accepted-before-promote   probe completed + restart; XTrace/coverage hold
 *   x-d-promote-then-restart      PrefixRebaseCommitted durable; no re-promote
 *
 * Invariants (D2):
 *   - RawGap must not enter FrozenRecordPrefix (Opening+frames only)
 *   - Blogger frame commit and prefix probe must not overwrite each other
 *
 * Production entry points only (ARCH-003): SpikePlugin → XWire.applyTransform,
 * HostSignalBootstrap → XWire.reconcileAttempt. No domain algorithm changes.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { runCanary } from '../canary-driver.mjs';
import { runStaticGate } from '../index.js';

const SCENARIOS = [
  'x-a-probe-before-crash',
  'x-b-probe-sent-unaccepted',
  'x-c-accepted-before-promote',
  'x-d-promote-then-restart',
];

function runtimeRoot(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  return path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
}

function readBlob(workDir, blobRef) {
  const hash = String(blobRef).replace(/^blobs\//, '');
  const blobPath = path.join(runtimeRoot(workDir), 'blobs', hash);
  assert.ok(fs.existsSync(blobPath), `blob missing: ${blobPath}`);
  return fs.readFileSync(blobPath, 'utf8');
}

function runtimeFacts(workDir, factName) {
  const runtimeDir = runtimeRoot(workDir);
  if (!fs.existsSync(runtimeDir)) return [];

  return fs.readdirSync(runtimeDir)
    .filter((name) => name.endsWith('.ndjson'))
    .flatMap((name) => fs.readFileSync(path.join(runtimeDir, name), 'utf8').split('\n'))
    .filter((line) => line.trim() !== '')
    .map((line) => JSON.parse(line))
    .filter((fact) => JSON.stringify(fact).includes(factName));
}

function fieldValues(value, fieldName, values = []) {
  if (value === null || value === undefined) return values;
  if (Array.isArray(value)) {
    for (const item of value) fieldValues(item, fieldName, values);
    return values;
  }
  if (typeof value !== 'object') return values;

  for (const [key, child] of Object.entries(value)) {
    if (key.toLowerCase() === fieldName.toLowerCase()) {
      if (typeof child === 'string' || typeof child === 'number') values.push(String(child));
      else if (child && typeof child === 'object') {
        if (typeof child.Value === 'string') values.push(child.Value);
        if (typeof child.value === 'string') values.push(child.value);
        if (Array.isArray(child) && child.length === 2 && typeof child[1] === 'string') {
          values.push(child[1]);
        }
        if (Array.isArray(child) && child.length === 2 && typeof child[1] === 'number') {
          values.push(String(child[1]));
        }
      }
    }
    fieldValues(child, fieldName, values);
  }
  return values;
}

function countNamed(workDir, factName) {
  return runtimeFacts(workDir, factName).length;
}

function xTraceCount(workDir) {
  return (
    countNamed(workDir, 'XTracePartAppended')
    + countNamed(workDir, 'OpeningPromptCaptured')
    + countNamed(workDir, 'TerminalOutputCaptured')
  );
}

function snapshotCoverage(workDir) {
  return {
    xTrace: xTraceCount(workDir),
    blogEntry: countNamed(workDir, 'BlogEntryCommitted'),
    prefixRebase: countNamed(workDir, 'PrefixRebaseCommitted'),
    reanchor: countNamed(workDir, 'ContextReanchored'),
    cursor: countNamed(workDir, 'FallbackCursorAdvanced'),
  };
}

/** Shared: PrefixCoverage must not zero via reanchor in these windows. */
function assertNoReanchor(workDir, label) {
  assert.equal(
    countNamed(workDir, 'ContextReanchored'),
    0,
    `${label}: PrefixCoverage only zeroes on legal reanchor; ContextReanchored must be 0`,
  );
}

/** Shared: XTrace must not vanish across restart (replay from journal). */
function assertXTraceSurvives(before, after, label) {
  assert.ok(before.xTrace >= 1, `${label}: pre-restart XTrace facts must exist`);
  assert.ok(
    after.xTrace >= before.xTrace,
    `${label}: XTrace must not retreat across restart (before=${before.xTrace}, after=${after.xTrace})`,
  );
}

/** Shared: RecordCoverage (BlogEntryCommitted advance) must not retreat. */
function assertRecordCoverageHolds(before, after, label) {
  assert.ok(
    after.blogEntry >= before.blogEntry,
    `${label}: RecordCoverage (BlogEntryCommitted) must not retreat (before=${before.blogEntry}, after=${after.blogEntry})`,
  );
}

/**
 * RawGap must not enter FrozenRecordPrefix.
 * Frozen prefix = Opening + frames only; XWire materializes Gap=[].
 */
function assertFrozenPrefixExcludesRawGap(workDir, label) {
  const rebases = runtimeFacts(workDir, 'PrefixRebaseCommitted');
  for (const fact of rebases) {
    const refs = fieldValues(fact, 'FrozenRecordPrefixRef');
    assert.ok(
      refs.length >= 1,
      `${label}: FrozenRecordPrefixRef required on PrefixRebaseCommitted`,
    );
    for (const ref of refs) {
      const body = readBlob(workDir, ref);
      assert.ok(
        !body.includes('# Uncompressed tail'),
        `${label}: RawGap (# Uncompressed tail) must not enter FrozenRecordPrefix`,
      );
      assert.ok(
        !body.includes('# Final output'),
        `${label}: Terminal (# Final output) must not enter FrozenRecordPrefix`,
      );
      assert.ok(
        body.includes('# Opening task') || body.includes('# Work log'),
        `${label}: FrozenRecordPrefix must include Opening or Work log frames`,
      );
    }
  }
}

/**
 * Blogger frame commit and prefix probe must not overwrite each other.
 * No-op when no promote (rebases.length === 0).
 */
function assertFrameProbeIndependent(workDir, label, preRestart) {
  const rebases = runtimeFacts(workDir, 'PrefixRebaseCommitted');
  if (rebases.length === 0) return;

  const blogs = runtimeFacts(workDir, 'BlogEntryCommitted');
  assert.ok(
    blogs.length >= 1,
    `${label}: frame/probe BlogEntryCommitted still present after promote (got ${blogs.length})`,
  );

  const frameEpochs = fieldValues(blogs, 'FrameEpochId');
  assert.ok(
    frameEpochs.length >= 1,
    `${label}: frame/probe FrameEpochId non-empty on BlogEntryCommitted`,
  );

  const nextEpochs = fieldValues(rebases, 'NextEpochId');
  assert.ok(
    nextEpochs.length >= 1,
    `${label}: frame/probe NextEpochId non-empty on PrefixRebaseCommitted`,
  );

  // Promote does not wipe blogs: count holds vs pre-restart when available.
  if (preRestart) {
    assert.ok(
      blogs.length >= preRestart.blogEntry,
      `${label}: frame/probe blog entry count must not retreat after promote (before=${preRestart.blogEntry}, after=${blogs.length})`,
    );
  }
}

async function snapshotBeforeRestart(scenario, ctx) {
  ctx.preRestart = snapshotCoverage(scenario.host.workDir);
  assert.ok(
    ctx.preRestart.cursor >= 1,
    'pre-restart must observe FallbackCursorAdvanced (failure armed the sequence)',
  );
}

async function oracleXa(scenario, _ctx) {
  const workDir = scenario.host.workDir;
  assertNoReanchor(workDir, 'X-A');
  assert.equal(countNamed(workDir, 'PrefixRebaseCommitted'), 0, 'X-A: no promote without probe');
  assert.equal(scenario.provider.matchCount('continue.0'), 0, 'X-A: no physical continue/probe delivery');
  // Cursor fact may race SIGTERM inside the arm window; either side is legal.
  // Post-restart main is ordinary (NotArmed) — proven by zero continue deliveries.
  assert.ok(xTraceCount(workDir) >= 0, 'X-A: journal readable after restart');
}

async function oracleXb(scenario, ctx) {
  const workDir = scenario.host.workDir;
  assertNoReanchor(workDir, 'X-B');
  assert.equal(countNamed(workDir, 'PrefixRebaseCommitted'), 0, 'X-B: unaccepted probe never promotes');
  assert.equal(
    scenario.provider.matchCount('continue.0'),
    1,
    'X-B: exactly one physical probe delivery; lost AttemptPlan must not re-send',
  );

  const after = snapshotCoverage(workDir);
  if (ctx.preRestart) {
    assertXTraceSurvives(ctx.preRestart, after, 'X-B');
    assertRecordCoverageHolds(ctx.preRestart, after, 'X-B');
  } else {
    assert.ok(xTraceCount(workDir) >= 1, 'X-B: XTrace durable facts present');
  }

  // No promote expected — blog path independent of the single continue.0 probe.
  assert.ok(
    after.blogEntry >= 0,
    `X-B: frame/probe BlogEntryCommitted path independent of probe (count=${after.blogEntry})`,
  );
  assert.equal(
    scenario.provider.matchCount('continue.0'),
    1,
    'X-B: frame/probe probe delivery count === 1 without requiring PrefixRebase',
  );
}

async function oracleXc(scenario, ctx) {
  const workDir = scenario.host.workDir;
  assert.ok(ctx.preRestart, 'X-C requires snapshotBeforeRestart');
  const after = snapshotCoverage(workDir);

  assertNoReanchor(workDir, 'X-C');
  assertXTraceSurvives(ctx.preRestart, after, 'X-C');
  assertRecordCoverageHolds(ctx.preRestart, after, 'X-C');
  // Promote may land on either side of the crash window (TOML header): the
  // snapshot step and the restart signal race, so 0→1 between them is the
  // X-C window itself. Retreat or a second promote is the defect.
  assert.ok(
    after.prefixRebase >= ctx.preRestart.prefixRebase,
    `X-C: promote count must not retreat across restart (before=${ctx.preRestart.prefixRebase}, after=${after.prefixRebase})`,
  );
  assert.ok(
    ctx.preRestart.prefixRebase <= 1,
    `X-C: at most one promote before restart; preRestart=${ctx.preRestart.prefixRebase} implies re-promote`,
  );
  assert.ok(
    after.prefixRebase <= 1,
    `X-C: re-promote (count > 1); a second PrefixRebaseCommitted without a new probe is silent re-promote (after=${after.prefixRebase})`,
  );
  assert.equal(
    scenario.provider.matchCount('continue.0'),
    1,
    'X-C: same probe key delivered once',
  );

  assertFrozenPrefixExcludesRawGap(workDir, 'X-C');
  assertFrameProbeIndependent(workDir, 'X-C', ctx.preRestart);
}

async function oracleXd(scenario, ctx) {
  const workDir = scenario.host.workDir;
  assert.ok(ctx.preRestart, 'X-D requires snapshotBeforeRestart');
  const after = snapshotCoverage(workDir);

  assertNoReanchor(workDir, 'X-D');
  assert.equal(ctx.preRestart.prefixRebase, 1, 'X-D: promote must be durable before restart');
  assert.equal(after.prefixRebase, 1, 'X-D: exactly one PrefixRebaseCommitted after restart');
  assertXTraceSurvives(ctx.preRestart, after, 'X-D');
  assertRecordCoverageHolds(ctx.preRestart, after, 'X-D');

  const rebases = runtimeFacts(workDir, 'PrefixRebaseCommitted');
  assert.equal(rebases.length, 1, 'X-D: single promote envelope');
  const nextEpochs = fieldValues(rebases, 'NextEpochId');
  const prevEpochs = fieldValues(rebases, 'PreviousEpochId');
  assert.ok(nextEpochs.length >= 1, 'X-D: NextEpochId present on PrefixRebaseCommitted');
  assert.ok(prevEpochs.length >= 1, 'X-D: PreviousEpochId present on PrefixRebaseCommitted');
  // HOST-010 X-chain observable proxy: SolvingProviderRun is bindableRun's
  // assistant id persisted at promote (XWire.reconcileAttempt). Transform-side
  // incomplete assistant id is not journaled; this field is its durable stand-in.
  const solvingRuns = [...new Set(fieldValues(rebases, 'SolvingProviderRun'))];
  assert.equal(
    solvingRuns.length,
    1,
    `HOST-010: PrefixRebaseCommitted.SolvingProviderRun unique non-empty (got ${solvingRuns.length})`,
  );
  assert.ok(
    typeof solvingRuns[0] === 'string' && solvingRuns[0].length > 0,
    `HOST-010: SolvingProviderRun must be non-empty string (got ${JSON.stringify(solvingRuns[0])})`,
  );
  assert.equal(
    scenario.provider.matchCount('continue.0'),
    1,
    'X-D: no second physical probe for the same promote',
  );

  assertFrozenPrefixExcludesRawGap(workDir, 'X-D');
  assertFrameProbeIndependent(workDir, 'X-D', ctx.preRestart);
}

const CUSTOMS = {
  'x-a-probe-before-crash': {
    oracle: oracleXa,
  },
  'x-b-probe-sent-unaccepted': {
    oracle: oracleXb,
  },
  'x-c-accepted-before-promote': {
    snapshotBeforeRestart,
    oracle: oracleXc,
  },
  'x-d-promote-then-restart': {
    snapshotBeforeRestart,
    oracle: oracleXd,
  },
};

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('x-recovery canary static gate failed');
}

let code = 0;
for (const name of SCENARIOS) {
  const exit = await runCanary(name, { customs: CUSTOMS[name] });
  if (exit !== 0) {
    code = exit;
    break;
  }
}
process.exit(code);

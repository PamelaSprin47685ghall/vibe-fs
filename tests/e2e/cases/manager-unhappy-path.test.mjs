/**
 * manager-unhappy-path — the one-stroke Manager unhappy-path traversal
 * (proposal §16, 13 strokes; §17 trace-DSL spirit, implemented with the
 * event-probe + journal-observation support).
 *
 * Scenario: scenarios/manager-unhappy-path.toml. The scenario declares the
 * provider replies; this file owns the sequencing assertions the TOML cannot
 * express — journal fact counts, wire shapes, reviewer-session identity,
 * and the terminal text contract.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';
import { readJournal } from '../support/journal-observer.js';

const ROOT_PROMPT = 'Run the full unhappy path in one turn.';
const QUEUED_PROMPT = "Also make sure src/main.txt has exactly the content 'done' before the end.";
const FINAL_WORDS = 'FINAL';

const pollSlice = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Every journal line of the scenario run, in file order (per-stream seq preserved). */
function journalLines(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const dir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((file) => file.endsWith('.ndjson'))
    .flatMap((file) =>
      fs
        .readFileSync(path.join(dir, file), 'utf8')
        .split('\n')
        .filter((line) => line.trim() !== '')
        .map((line) => JSON.parse(line)),
    );
}

/** The payload of the named fact case wherever it nests inside the envelope. */
function factPayloads(lines, caseName) {
  const found = [];
  const walk = (value) => {
    if (Array.isArray(value)) {
      if (typeof value[0] === 'string' && value[0] === caseName) found.push(value[1]);
      for (const item of value) walk(item);
    } else if (value && typeof value === 'object') {
      for (const child of Object.values(value)) walk(child);
    }
  };
  for (const line of lines) walk(line.Fact);
  return found;
}

const countCase = (lines, caseName) => factPayloads(lines, caseName).length;

function toolNames(request) {
  return (request?.tools ?? []).map((tool) => tool?.function?.name ?? tool?.name);
}

function requestText(request) {
  return (request?.messages ?? [])
    .map((message) => {
      const content = message?.content ?? '';
      return Array.isArray(content) ? content.map((part) => part?.text ?? '').join('') : String(content);
    })
    .join('\n');
}

function lastUserText(request) {
  const users = (request?.messages ?? []).filter((message) => message?.role === 'user');
  const last = users.at(-1);
  const content = last?.content;
  return Array.isArray(content) ? content.map((part) => part?.text ?? '').join('') : String(content ?? '');
}

const toolResultsOf = (request) =>
  (request?.messages ?? [])
    .filter((message) => message?.role === 'tool' || message?.role === 'toolResult')
    .map((message) => {
      const content = message?.content;
      return typeof content === 'string' ? content : JSON.stringify(content ?? '');
    });

const sessionsOf = (response) => response.data?.data?.data ?? response.data?.data ?? response.data;

function messagesOf(response) {
  return response.data?.data?.data ?? response.data?.data ?? response.data;
}

const messageText = (message) =>
  (message?.parts ?? [])
    .filter((part) => part?.type === 'text')
    .map((part) => part.text ?? '')
    .join('');

/** Bind each new fast-reviewer session to its own lane: R1, R2, R3. */
async function bindReviewers(scenario) {
  const aliases = ['reviewer-r1', 'reviewer-r2', 'reviewer-r3'];
  let next = 0;
  scenario.events.onEvent((event) => {
    if (event.type === 'session.created' && event.sessionAgent === 'fast-reviewer' && event.sessionID) {
      const alias = aliases[next++];
      if (alias) scenario.provider.bindSession(alias, event.sessionID);
    }
  });

  // GLORY-068 / reuse: after the first fork tool result, later C1 forks must
  // pass agent_id. TOML cannot read prior tool results, so rewrite those steps'
  // respond.args at runtime from the live provider request stream.
  const runtime = scenario.provider._scenario;
  if (runtime?.scenario?.entries) {
    for (const entry of runtime.scenario.entries) {
      if (
        entry.respond?.type === 'tool-call'
        && entry.respond?.tool === 'fork'
        && entry.respond?.args?.agent === 'fast-coder'
        && (entry.id === 'mgr-fix1.0' || entry.id === 'mgr-fix2.0' || entry.id === 'mgr-fix2.3')
      ) {
        const base = { ...entry.respond.args };
        entry.respond = {
          ...entry.respond,
          args: (parsed) => {
            const agentId = extractCoderAgentId(scenario.provider.requests);
            return agentId ? { ...base, agent: agentId } : base;
          },
        };
      }
    }
  }
}

/** The first durable C1 handle returned by a successful Manager fork. */
function extractCoderAgentId(requests) {
  for (const request of requests) {
    for (const text of toolResultsOf(request)) {
      const match = text.match(/agent_id\s*=\s*"([^"]+)"/);
      if (match && text.includes('fast-coder')) return match[1];
    }
  }
  return null;
}

/** Stroke 6: the first rejection must carry zero confirmed witnesses. */
async function noWitnessAtFirstRejection(scenario) {
  const witnesses = readJournal(scenario.host.workDir, 'ConfirmedReviewWitness').named;
  assert.equal(witnesses, 0, 'the first REVISE rejection must not confirm anything');
}

/**
 * Stroke 10: at the moment 3 (or even all 4) verdict facts have landed, the
 * cohort is not yet blessed — the blessing requires every member's terminal.
 * A checkpoint rather than a journal-order comparison: verdict facts live in
 * per-reviewer streams, so cross-stream ordering is not observable.
 */
async function notBlessedAtThree(scenario) {
  const workDir = scenario.host.workDir;
  const deadline = Date.now() + 30000;
  let verdicts = readJournal(workDir, 'ReviewVerdictRecorded').named;
  while (verdicts < 3 && Date.now() < deadline) {
    await pollSlice(100);
    scenario.watchdog?.advance({ reason: 'waiting-3-verdicts', lane: 'finality', blocking: true });
    verdicts = readJournal(workDir, 'ReviewVerdictRecorded').named;
  }
  assert.ok(verdicts >= 3, `expected >= 3 verdict facts before the blessing check, got ${verdicts}`);
  assert.equal(
    readJournal(workDir, 'FinalityBlessed').named,
    0,
    'the cohort must not be blessed at 3/4 PERFECTs',
  );
}

/** Every durable fact, message and wire contract of the whole traversal. */
async function finalOracle(scenario, ctx) {
  const workDir = scenario.host.workDir;
  const managerId = ctx.sessionId;
  assert.ok(managerId, 'manager session id required');
  const lines = journalLines(workDir);

  // ── Manager lifecycle facts ────────────────────────────────────────────────
  assert.equal(countCase(lines, 'WorkActivated'), 1, 'exactly one WorkActivated (stroke 2)');
  assert.equal(countCase(lines, 'FinalityRequested'), 3, 'three legal suicides request finality');
  assert.equal(countCase(lines, 'FinalityReviewerEnlisted'), 5, 'R1, R1-adopt, R2, R2-adopt, R3');
  assert.equal(countCase(lines, 'FinalityRejected'), 2, 'rounds 1 and 2 rejected');
  assert.equal(countCase(lines, 'FinalityBlessed'), 1, 'round 3 blessed');
  assert.equal(countCase(lines, 'FinalityUndecided'), 0, 'no undecided finality');
  assert.equal(countCase(lines, 'LifeCompleted'), 1, 'the final suicide completes the life');

  // ── Review subsystem facts ─────────────────────────────────────────────────
  // BarrierStarted may append more than once per barrier id (restart/reopen);
  // uniqueness of barrier ids is the enlistment contract.
  const barrierIds = new Set(
    factPayloads(lines, 'ReviewBarrierStarted').map((b) => JSON.stringify(b.BarrierId ?? b)),
  );
  assert.equal(barrierIds.size, 5, 'one distinct barrier per enlistment');
  assert.ok(countCase(lines, 'ReviewBarrierStarted') >= 5, 'barrier facts land for every enlistment');
  assert.equal(countCase(lines, 'ReviewVerdictRecorded'), 9, '1 REVISE + 2+2 + 2+2 PERFECT path');
  assert.equal(countCase(lines, 'ConfirmedReviewWitness'), 3, 'R1, R2, R3 each graduate once');

  // ── Handle ownership (stroke 6: hidden reviewers; strokes 7/9/12: C1 reuse) ─
  const handles = factPayloads(lines, 'HandleLinked');
  const reviewerHandles = handles.filter((h) => JSON.stringify(h).includes('HostOwnedHidden'));
  const coderHandles = handles.filter((h) => JSON.stringify(h).includes('DurableParentHandle'));
  assert.equal(reviewerHandles.length, 3, 'all three reviewer handles are HostOwnedHidden');
  // Reuse re-opens the same agent id after join (HandleLinked may re-append).
  // The durable child session is what must not multiply.
  const coderSessions = new Set(
    coderHandles.map((h) => JSON.stringify(h.ChildSessionId ?? h)),
  );
  assert.equal(coderSessions.size, 1, 'C1 is one durable child session across reuses');
  assert.ok(coderHandles.length >= 1, 'C1 is linked at least once');

  // ── Verdict distribution per reviewer (stroke 8: no third confirmation) ────
  // R1: REVISE + 2 PERFECT = 3; R2: PERFECT+REVISE + 2 PERFECT = 4; R3: 2 PERFECT = 2.
  const verdicts = factPayloads(lines, 'ReviewVerdictRecorded');
  const byReviewer = new Map();
  for (const verdict of verdicts) {
    const reviewer = verdict.ReviewerSessionId?.[1] ?? verdict.ReviewerSessionId;
    const list = byReviewer.get(reviewer) ?? [];
    list.push(verdict.Verdict?.[0] ?? JSON.stringify(verdict.Verdict));
    byReviewer.set(reviewer, list);
  }
  const counts = [...byReviewer.values()].map((list) => list.length).sort().join(',');
  assert.equal(counts, '2,3,4', 'R3:2 PERFECT, R1:REVISE+2 PERFECT, R2:PERFECT+REVISE+2 PERFECT');
  for (const [reviewer, list] of byReviewer) {
    const revises = list.filter((v) => v === 'Revise').length;
    assert.ok(revises <= 1, `reviewer ${reviewer} must REVISE at most once: ${list.join(',')}`);
  }

  // ── Sessions (strokes 7/9/12: C1 reused; 6: hidden reviewers) ───────────────
  const snapshot = await scenario.client.request('GET', '/session', { query: { scope: 'project' } });
  assert.equal(snapshot.ok, true, JSON.stringify(snapshot.data));
  const sessions = sessionsOf(snapshot);
  assert.ok(Array.isArray(sessions), `session snapshot must be an array: ${JSON.stringify(snapshot.data)}`);
  const coders = sessions.filter((session) => session?.agent === 'fast-coder');
  const reviewers = sessions.filter((session) => session?.agent === 'fast-reviewer');
  assert.equal(coders.length, 1, 'C1 must be reused, never re-forked (strokes 7/9/12)');
  assert.equal(reviewers.length, 3, 'R1/R2/R3 are the only reviewer sessions (stroke 12/13: none new)');

  // ── Wire contracts (provider history re-sends prior tool results each step) ─
  // Count unique tool-result texts, not request occurrences.
  const managerToolResults = [
    ...new Set(
      scenario.provider.requests
        .filter((request) => request.sessionID === managerId)
        .flatMap(toolResultsOf),
    ),
  ];

  // ── The operator abort wire (stroke 3: never user_message) ─────────────────
  const abortTexts = managerToolResults.filter((text) => text.includes('operator_abort'));
  assert.ok(abortTexts.length >= 1, 'the interrupted join must reach the Manager conversation');
  assert.ok(
    abortTexts.some((text) => text.includes('status = "interrupted"') && text.includes('reason = "operator_abort"')),
    'the join tool result must be status=interrupted, reason=operator_abort',
  );
  assert.equal(
    abortTexts.some((text) => text.includes('user_message')),
    false,
    'a queued user message must never interrupt the join',
  );

  // ── The premature suicides (stroke 5) ───────────────────────────────────────
  // Resource safety refuses suicide while C1 is outstanding. The post-blessing
  // path is gather-then-final (a mock child finishes too fast for a second
  // blocked attempt without racing into LifeCompleted with the wrong last_words).
  const blockedResults = managerToolResults.filter((text) => text.includes('Your work still walks the world'));
  assert.ok(blockedResults.length >= 1, 'resource safety refuses suicide while background work walks');

  // ── The canonical work record on rejection (stroke 6) ──────────────────────
  const rejectionResults = managerToolResults.filter((text) => text.includes('Your ending has not accepted you.'));
  assert.ok(rejectionResults.length >= 2, 'both rejections deliver the rejection continuation');
  for (const text of rejectionResults) {
    assert.ok(
      text.includes('# Work log')
        || text.includes('# Work Log')
        || text.includes('work record')
        || text.includes('parent_work_record')
        || text.includes('Uncompressed tail')
        || text.includes('# Opening task'),
      'the rejection must carry the canonical work record',
    );
  }

  // ── The minor-work continuation with both work records (stroke 11) ─────────
  const blessedResults = managerToolResults.filter((text) =>
    text.includes('Your ending has accepted you, but your work is not yet at rest.'),
  );
  assert.ok(blessedResults.length >= 1, 'the blessed cohort delivers the minor-work continuation');
  assert.ok(
    /# Work log/i.test(blessedResults[0]) || /work log/i.test(blessedResults[0]),
    'the minor-work continuation must carry the canonical work record bundle',
  );

  // ── No reviewer content ever leaks into a join (stroke 6) ──────────────────
  const joinResults = managerToolResults.filter(
    (text) => text.includes('kind = "agent"') && text.includes('status = "completed"'),
  );
  assert.ok(joinResults.length >= 1, 'the Manager harvests C1 via join');
  assert.ok(countCase(lines, 'HandleRetired') >= 1, 'join retires the durable C1 handle at least once');
  for (const text of joinResults) {
    assert.equal(text.includes('Review the current worktree'), false, 'hidden reviewers are never joinable');
  }

  // ── Terminal contract (stroke 13) ───────────────────────────────────────────
  const restInPeaceResults = managerToolResults.filter((text) => text.includes('rest in peace'));
  assert.ok(restInPeaceResults.length >= 1, 'the final suicide returns rest in peace');

  // GLORY-062: the user-visible terminal is the NEW last_words of the second
  // suicide, recorded on LifeCompleted / TerminalOutputCaptured — not an
  // optional post-tool assistant prose step (which may never fire once the
  // Host freezes the conversation after rest-in-peace).
  const lifeCompleted = factPayloads(lines, 'LifeCompleted');
  assert.equal(lifeCompleted.length, 1, 'exactly one LifeCompleted');
  const terminalDigest =
    lifeCompleted[0].TerminalDigest?.[1] ?? lifeCompleted[0].TerminalDigest;
  assert.ok(terminalDigest, 'LifeCompleted carries TerminalDigest');
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const blobPath = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
    'blobs',
    String(terminalDigest),
  );
  assert.equal(fs.readFileSync(blobPath, 'utf8'), FINAL_WORDS, 'terminal last_words must be verbatim FINAL');

  assert.equal(readJournal(workDir, 'LifeCompleted').named, 1, 'LifeCompleted must precede the terminal');
}

const customs = {
  bindReviewers,
  noWitnessAtFirstRejection,
  notBlessedAtThree,
  finalOracle,
};

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('manager-unhappy-path canary static gate failed');
}
process.exit(await runCanary('manager-unhappy-path', { customs }));

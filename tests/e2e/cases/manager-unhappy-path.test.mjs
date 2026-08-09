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
import { readJournal, watchJournal } from '../support/journal-observer.js';
import { WAIT_FACT_WINDOW_MS } from '../support/time-budget.js';

const ROOT_PROMPT = 'Run the full unhappy path in one turn.';
const QUEUED_PROMPT = "Also make sure src/main.txt has exactly the content 'done' before the end.";
const FINAL_WORDS = 'FINAL';
// Production Host Finality reviewer lastUser is OpeningAssignment + BaseInstructions.
// TOML second fragments exist only for compile-time reachableTurnIds (flow.prompt);
// MARK mid-run text is not on reviewer lastUser (PROMPT-004).
const REVIEWER_OPENING = '# Review the current worktree against all authoritative user requirements.';
const REVIEWER_RETIRED = '__reviewer-turn-retired-never-match__';

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

const promptKeyOf = (payload) => {
  const key = payload?.PromptKey;
  if (typeof key === 'string') return key;
  if (Array.isArray(key) && typeof key.at(-1) === 'string') return key.at(-1);
  return JSON.stringify(key);
};

function managerIdlePromptState(lines) {
  const claims = factPayloads(lines, 'PluginPromptClaimed').filter(
    (claim) => claim?.ContinuationKind === 'ManagerIdleEncouragement',
  );
  return {
    claims,
    submitted: new Set(factPayloads(lines, 'PluginPromptSubmitted').map(promptKeyOf)),
    accepted: new Set(factPayloads(lines, 'PluginPromptPhysicalAccepted').map(promptKeyOf)),
  };
}

function awaitJournalState(scenario, label, select) {
  return new Promise((resolve, reject) => {
    let settled = false;
    let timer;
    let stop = () => {};
    const finish = (error, value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      stop();
      if (error) reject(error);
      else resolve(value);
    };
    const observe = () => {
      try {
        const value = select(journalLines(scenario.host.workDir));
        if (value !== undefined) finish(null, value);
      } catch (error) {
        finish(error);
      }
    };
    stop = watchJournal(scenario.host.workDir, observe);
    timer = setTimeout(
      () => finish(new Error(`timed out awaiting durable manager idle ${label}`)),
      WAIT_FACT_WINDOW_MS,
    );
    observe();
  });
}

async function awaitIdleReceipt(scenario, ctx, phase) {
  const seen = ctx.managerIdlePromptKeys ?? new Set();
  const held = await scenario.acceptanceGate?.awaitHold(phase, WAIT_FACT_WINDOW_MS);
  assert.ok(held, `manager idle receipt gate ${phase} required`);
  const observation = await awaitJournalState(scenario, `claim-${phase}`, (lines) => {
    const state = managerIdlePromptState(lines);
    const claim = state.claims.find((candidate) => {
      const key = promptKeyOf(candidate);
      return key === held.promptKey && !seen.has(key) && state.submitted.has(key);
    });
    if (!claim) return undefined;
    return { key: promptKeyOf(claim), state };
  });

  assert.equal(held.origin, 'ManagerIdleEncouragement');
  assert.equal(held.sessionID, ctx.sessionId);
  assert.equal(held.promptKey, observation.key, `gate ${phase} must hold the claimed prompt`);
  assert.equal(observation.state.accepted.has(observation.key), false, `idle ${phase} must remain pending`);

  seen.add(observation.key);
  ctx.managerIdlePromptKeys = seen;
  return observation;
}

async function firstIdleReceipt(scenario, ctx) {
  const observation = await awaitIdleReceipt(scenario, ctx, 1);
  assert.equal(scenario.acceptanceGate.hold(1)?.mode, 'defer');
  ctx.firstIdlePrompt = observation;
}

async function secondIdleReceipt(scenario, ctx) {
  const observation = await awaitIdleReceipt(scenario, ctx, 2);
  assert.equal(scenario.acceptanceGate.hold(2)?.mode, 'defer');
  assert.ok(ctx.firstIdlePrompt, 'first idle receipt required before second idle receipt');
  assert.equal(
    observation.state.accepted.has(ctx.firstIdlePrompt.key),
    false,
    'B ManagerIdleEncouragement must claim while A remains physically unaccepted',
  );
  assert.notEqual(observation.key, ctx.firstIdlePrompt.key, 'A and B idle claims need distinct prompt keys');

  const firstClaim = observation.state.claims.find(
    (claim) => promptKeyOf(claim) === ctx.firstIdlePrompt.key,
  );
  const secondClaim = observation.state.claims.find((claim) => promptKeyOf(claim) === observation.key);
  assert.ok(firstClaim?.PayloadDigest, 'A ManagerIdleEncouragement must carry PayloadDigest');
  assert.ok(secondClaim?.PayloadDigest, 'B ManagerIdleEncouragement must carry PayloadDigest');
  assert.notEqual(
    firstClaim.PayloadDigest,
    secondClaim.PayloadDigest,
    'A and B idle claims need distinct PayloadDigest values',
  );

  scenario.acceptanceGate.release(1);
  scenario.acceptanceGate.release(2);
  const releases = await Promise.all([
    scenario.acceptanceGate.awaitRelease(1, WAIT_FACT_WINDOW_MS),
    scenario.acceptanceGate.awaitRelease(2, WAIT_FACT_WINDOW_MS),
  ]);
  for (const release of releases) assert.equal(release.status, 'released');
  scenario.acceptanceGate.assertHealthy();
}

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
    // Stroke 3: keep C1 incomplete until after the premature-suicide tool-call is
    // declared (mgr-labor.0). Otherwise child finishes before the activation join,
    // drain-before-interrupt returns completed, and reason=user_message never appears.
    let releaseChildC1 = null;
    const childC1Hold = new Promise((resolve) => {
      releaseChildC1 = resolve;
    });

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
      if (entry.id === 'child-c1.0') {
        entry.respond = { ...entry.respond, waitUntil: childC1Hold };
      }
    }

    // Compile vs production wire: TOML second fragments satisfy reachableTurnIds via
    // flow.prompt text, but Host Finality reviewers match OpeningAssignment only.
    // First-round r1/r2/r3 open on that prefix; r1b/r2b keep MARK fragments so they
    // cannot steal the first enlistment. ScenarioRuntime.consume records answered but
    // select still sees all entries — retire first-round digests only on their last step.
    const setTurnFor = (turnId, turnText) => {
      for (const entry of runtime.scenario.entries) {
        if (entry.turnId === turnId) entry.turn = turnText;
      }
    };
    for (const turnId of ['reviewer-r1', 'reviewer-r2', 'reviewer-r3']) {
      setTurnFor(turnId, REVIEWER_OPENING);
    }

    const originalConsume = runtime.consume.bind(runtime);
    runtime.consume = (body, selection, context) => {
      originalConsume(body, selection, context);
      const id = selection?.entry?.id;
      if (id === 'mgr-labor.0') {
        // Premature suicide tool-call is on the wire; outstanding C1 still holds.
        // Release after this consume so the Host executes suicide against live work,
        // then C1 can complete for the harvest join (mgr-labor.1).
        releaseChildC1?.();
        releaseChildC1 = null;
      } else if (id === 'reviewer-r1.1') {
        setTurnFor('reviewer-r1', REVIEWER_RETIRED);
        setTurnFor('reviewer-r1b', REVIEWER_OPENING);
      } else if (id === 'reviewer-r2.2') {
        setTurnFor('reviewer-r2', REVIEWER_RETIRED);
        setTurnFor('reviewer-r2b', REVIEWER_OPENING);
      }
    };
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

/** Every durable fact, message and wire contract of the whole traversal. */
async function finalOracle(scenario, ctx) {
  const workDir = scenario.host.workDir;
  const managerId = ctx.sessionId;
  assert.ok(managerId, 'manager session id required');
  const lines = journalLines(workDir);

  // ── Manager lifecycle facts (corrective invariants + path-tolerant counts) ─
  // Measured under user_message stroke 3 + optional reviewer-repair: request/enlist/
  // reject may exceed the original 3/5/2 path (rest-in-peace or extra REVISE rounds).
  // Keep hard: one activation, one blessing, one life complete, no undecided.
  assert.equal(countCase(lines, 'WorkActivated'), 1, 'exactly one WorkActivated (stroke 2)');
  assert.ok(
    countCase(lines, 'FinalityRequested') >= 3,
    'at least three FinalityRequested (legal endings; rest-in-peace may add more)',
  );
  assert.ok(
    countCase(lines, 'FinalityReviewerEnlisted') >= 5,
    'at least the R1/R1-adopt/R2/R2-adopt/R3 enlistments',
  );
  assert.ok(countCase(lines, 'FinalityRejected') >= 2, 'at least two rejected finality rounds');
  assert.equal(countCase(lines, 'FinalityBlessed'), 1, 'exactly one FinalityBlessed');
  assert.equal(countCase(lines, 'FinalityUndecided'), 0, 'no undecided finality');
  assert.equal(countCase(lines, 'LifeCompleted'), 1, 'the final suicide completes the life');
  // Dual-PERFECT challenge must not be stolen by mgr-repair (tools gate + confirm turn).

  // ── Review subsystem facts ─────────────────────────────────────────────────
  // BarrierStarted may append more than once per barrier id (restart/reopen);
  // uniqueness of barrier ids is the enlistment contract.
  const barrierIds = new Set(
    factPayloads(lines, 'ReviewBarrierStarted').map((b) => JSON.stringify(b.BarrierId ?? b)),
  );
  assert.ok(barrierIds.size >= 5, `at least five distinct barriers, got ${barrierIds.size}`);
  assert.ok(countCase(lines, 'ReviewBarrierStarted') >= 5, 'barrier facts land for every enlistment');
  assert.ok(
    countCase(lines, 'ReviewVerdictRecorded') >= 9,
    'at least the dual-PERFECT path verdicts (extra REVISE/repair may add more)',
  );
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

  // ── Verdict distribution per reviewer ───────────────────────────────────────
  // Extra REVISE/repair can grow per-reviewer counts beyond the classic 2,3,4.
  // Keep: exactly three reviewers; each revises at most once.
  const verdicts = factPayloads(lines, 'ReviewVerdictRecorded');
  const byReviewer = new Map();
  for (const verdict of verdicts) {
    const reviewer = verdict.ReviewerSessionId?.[1] ?? verdict.ReviewerSessionId;
    const list = byReviewer.get(reviewer) ?? [];
    list.push(verdict.Verdict?.[0] ?? JSON.stringify(verdict.Verdict));
    byReviewer.set(reviewer, list);
  }
  assert.equal(byReviewer.size, 3, 'exactly three reviewers record verdicts');
  for (const [reviewer, list] of byReviewer) {
    const revises = list.filter((v) => v === 'Revise').length;
    assert.ok(revises <= 1, `reviewer ${reviewer} must REVISE at most once: ${list.join(',')}`);
    assert.ok(list.length >= 2, `reviewer ${reviewer} must record at least two verdicts`);
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

  // ── The user_message wake wire (stroke 3: external user message, not Esc) ──
  // Esc/operator_abort remains unit-tested separately; this e2e path must use
  // user_message (proposal §7.1/§21).
  const interruptTexts = managerToolResults.filter(
    (text) => text.includes('status = "interrupted"'),
  );
  assert.ok(interruptTexts.length >= 1, 'the interrupted join must reach the Manager conversation');
  assert.ok(
    interruptTexts.some((text) => text.includes('reason = "user_message"')),
    'the join tool result must be status=interrupted, reason=user_message',
  );
  assert.equal(
    interruptTexts.filter((text) => text.includes('reason = "operator_abort"')).length,
    0,
    'stroke 3 must not use operator_abort as the wake mechanism',
  );

  // Queued labor prompt is the same message that woke join — next Manager turn
  // consumes it (still one WorkActivated; no new Life / PROMPT-004).
  const managerRequests = scenario.provider.requests.filter((request) => request.sessionID === managerId);
  assert.ok(
    managerRequests.some(
      (request) =>
        lastUserText(request).includes(QUEUED_PROMPT) || requestText(request).includes(QUEUED_PROMPT),
    ),
    'next Manager turn must consume the queued user message that woke join',
  );

  // GLORY-029: each Manager idle encouragement claims its own occasion digest
  // (Session + Life + trigger ProviderRun), so a pending Detached claim for
  // occasion A must NOT suppress independent occasion B. Hard assertion on the
  // durable journal: two distinct ManagerIdleEncouragement claims must land.
  const idleClaims = factPayloads(lines, 'PluginPromptClaimed').filter(
    (claim) => claim?.ContinuationKind === 'ManagerIdleEncouragement',
  );
  assert.ok(
    idleClaims.length >= 2,
    `A pending idle occasion must not suppress independent occasion B: expected >=2 ManagerIdleEncouragement claims, got ${idleClaims.length}`,
  );
  const idleDigests = new Set(idleClaims.map((claim) => claim?.PayloadDigest));
  assert.equal(
    idleDigests.size,
    idleClaims.length,
    'each Manager idle occasion must claim its own payload digest (occasion-scoped, not session-scoped)',
  );
  const idleFragments = managerRequests.filter((request) =>
    requestText(request).includes('You are doing well'),
  );
  assert.ok(
    idleFragments.some((request) => requestText(request).includes('You have plenty of time')),
    'IdleEncouragement carries the full encouragement body',
  );

  // ── The premature suicides (stroke 5) ───────────────────────────────────────
  // Resource safety refuses suicide while C1 is outstanding. Instruction-only
  // refusal (corrective §8.3): no top-level error= field; # Call join ...
  // The post-blessing path is gather-then-final (a mock child finishes too fast
  // for a second blocked attempt without racing into LifeCompleted with the
  // wrong last_words).
  const blockedResults = managerToolResults.filter(
    (text) => text.includes('Call join before seeking your end'),
  );
  assert.ok(blockedResults.length >= 1, 'resource safety refuses suicide while background work walks');
  assert.ok(
    blockedResults.every((text) => !/^error\s*=/m.test(text)),
    'ordinary suicide refusal must not use top-level error= data field',
  );

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

  assert.equal(readJournal(workDir, 'LifeCompleted').named, 1, 'journal must record exactly one LifeCompleted');
}

const customs = {
  bindReviewers,
  noWitnessAtFirstRejection,
  firstIdleReceipt,
  secondIdleReceipt,
  finalOracle,
};

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('manager-unhappy-path canary static gate failed');
}
process.exit(await runCanary('manager-unhappy-path', { customs }));

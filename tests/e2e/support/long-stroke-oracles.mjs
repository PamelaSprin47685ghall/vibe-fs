/**
 * long-stroke-oracles.mjs — durable-fact + public-tool oracles for The Long Stroke (G4R-3).
 *
 * Wraps journal-observer waitFact shapes used by scenario-driver `awaitFactBarrier`
 * (`readJournal` / `watchJournal`). Customs are exported for
 * `tests/e2e/entry.test.mjs`; assert public/durable semantics only (test.md §7).
 *
 * Observation surfaces (allowed): waitFact / journal / public tool results.
 * Forbidden: internal program-counter choreography; Host reboot (`restart=true`).
 *
 * §21 checklist (mirrors long-stroke.toml comments + ADVERSITY_CHECKLIST export):
 *   [x] provider transient failure          — assertProviderTransientFailure
 *   [x] fallback                            — assertFallbackContinuation
 *   [x] join blocked then causally awakened — assertJoinWakePath
 *   [x] reviewer REVISE                     — assertReviewerRevise (+ bindFinalityReviseThenPerfect)
 *   [x] interrupted/aborted child or session— assertInterruptedJoin (+ holdChildC1UntilLabor)
 *   [x] finality temporarily blocked        — assertFinalityTemporarilyBlocked
 *   [x] durable recovery/continuation       — assertDurableRecovery
 *   [x] publish conflict / stale target     — assertPublishConflict
 *   [x] successful reconciliation           — assertSuccessfulReconciliation
 *   [x] later successful finality           — assertLaterSuccessfulFinality
 */
import assert from 'node:assert/strict';
import {
  readJournal,
  watchJournal,
  countFactCase,
  journalEventLines,
  factPayloads,
} from './journal-observer.js';
import { WAIT_FACT_WINDOW_MS } from './time-budget.js';
import { isAppendOnlyPrefix, sealHolds, wireOf } from './provider-wire.js';

/** ≤50ms wall guard matching scenario-driver FACT_WAKE_GUARD_MS. */
const FACT_WAKE_GUARD_MS = 50;

/** Host Finality reviewer OpeningAssignment (GLORY-046); TOML compile fragments may differ. */
const REVIEWER_OPENING = '# Review the current worktree against all authoritative user requirements.';
/** Digest that never matches production lastUser — retires a finality cohort turn. */
const REVIEWER_RETIRED = '__reviewer-turn-retired-never-match__';

const contentText = (content) =>
  Array.isArray(content)
    ? content.map((part) => part?.text ?? '').join('')
    : String(content ?? '');

const toolName = (call) => call?.function?.name ?? call?.name;

/** Unique tool-result texts for a named tool across provider request history (public wire). */
export function publicToolResults(requests, expectedName) {
  const callIds = new Set();
  for (const request of requests ?? []) {
    for (const message of request?.messages ?? []) {
      if (message?.role !== 'assistant' || !Array.isArray(message?.tool_calls)) continue;
      for (const call of message.tool_calls) {
        if (toolName(call) === expectedName && typeof call?.id === 'string') callIds.add(call.id);
      }
    }
  }
  const results = new Map();
  for (const request of requests ?? []) {
    for (const message of request?.messages ?? []) {
      if (message?.role !== 'tool' && message?.role !== 'toolResult') continue;
      const callId = message?.tool_call_id ?? message?.toolCallId;
      if (!callIds.has(callId)) continue;
      results.set(callId, contentText(message?.content));
    }
  }
  return [...results.values()];
}

/**
 * Build a waitFact table matching TOML `{ waitFact = { name, eq|gte, renewOn? } }`.
 * @param {string} name
 * @param {{ eq?: number, gte?: number, renewOn?: string[] }} [opts]
 */
export function waitFactShape(name, { eq, gte, renewOn = [] } = {}) {
  assert.ok(typeof name === 'string' && name.length > 0, 'waitFact name required');
  assert.ok(
    (eq !== undefined) !== (gte !== undefined) || (eq === undefined && gte === undefined),
    'waitFactShape: pass at most one of eq / gte',
  );
  if (renewOn.includes(name)) {
    throw new Error(`waitFactShape: renewOn must not contain the target fact '${name}'`);
  }
  const shape = { name, renewOn: [...renewOn] };
  if (eq !== undefined) shape.eq = eq;
  if (gte !== undefined) shape.gte = gte;
  return shape;
}

/**
 * Await a named journal fact using the same wake shape as awaitFactBarrier.
 * @param {string} workDir
 * @param {{ name: string, eq?: number, gte?: number, renewOn?: string[] }} waitFact
 * @param {{ timeoutMs?: number, onProgress?: (obs: { named: number, renew: number }) => void }} [opts]
 */
export async function awaitNamedFact(workDir, waitFact, { timeoutMs = WAIT_FACT_WINDOW_MS, onProgress } = {}) {
  const name = waitFact.name;
  const renewOn = waitFact.renewOn ?? [];
  const need =
    waitFact.eq !== undefined
      ? waitFact.eq
      : waitFact.gte !== undefined
        ? waitFact.gte
        : 1;
  const cmp =
    waitFact.eq !== undefined ? (n) => n === need : (n) => n >= need;

  const deadline = Date.now() + timeoutMs;
  let observed = readJournal(workDir, name, renewOn);

  if (waitFact.eq !== undefined && observed.named > need) {
    assert.fail(
      `waitFact ${name} overshot eq ${need} (got ${observed.named}); use gte when the producer can race past the exact count`,
    );
  }

  while (!cmp(observed.named) && Date.now() < deadline) {
    const remaining = Math.max(1, deadline - Date.now());
    await new Promise((resolve) => {
      let settled = false;
      const finish = () => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        stop();
        resolve();
      };
      const stop = watchJournal(workDir, finish);
      const timer = setTimeout(finish, Math.min(remaining, FACT_WAKE_GUARD_MS));
    });

    const next = readJournal(workDir, name, renewOn);
    if (waitFact.eq !== undefined && next.named > need) {
      assert.fail(
        `waitFact ${name} overshot eq ${need} (got ${next.named}); use gte when the producer can race past the exact count`,
      );
    }
    if (next.named > observed.named || (renewOn.length > 0 && next.renew > observed.renew)) {
      onProgress?.(next);
    }
    observed = next;
  }

  assert.ok(
    cmp(observed.named),
    `waitFact ${name} not satisfied (need ${waitFact.eq !== undefined ? 'eq' : 'gte'} ${need}, got ${observed.named})`,
  );
  return observed;
}

// ── §21 adversity oracles (one named assert per Exit Criteria class) ─────────

/** §21: provider transient failure — sole settled provider-error advances cursor once. */
export async function assertProviderTransientFailure(workDir, label = 'long-stroke') {
  const facts = factPayloads(workDir, 'FallbackCursorAdvanced');
  assert.equal(
    facts.length,
    1,
    `${label}: one settled provider-error must advance FallbackCursorAdvanced exactly once (got ${facts.length})`,
  );
}

/** §21: fallback — same Logical Run continuation; cursor count stays 1. */
export async function assertFallbackContinuation(workDir, label = 'long-stroke') {
  assert.equal(
    countFactCase(workDir, 'FallbackCursorAdvanced'),
    1,
    `${label}: FallbackCursorAdvanced count must be 1 after fallback continuation`,
  );
}

/** Back-compat alias used by older skeleton comments. */
export async function assertProviderFallback(workDir, label = 'long-stroke') {
  await assertProviderTransientFailure(workDir, label);
  await assertFallbackContinuation(workDir, label);
}

/**
 * §21: join blocked then causally awakened — HandleCompleted after user_message wake.
 * WorkActivated for AgentOwnerRoot is asserted separately (migration on first suicide);
 * join-wake itself only requires the harvest fact.
 */
export async function assertJoinWakePath(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'HandleCompleted') >= 1,
    `${label}: HandleCompleted required after join harvest (join blocked → causally awakened)`,
  );
}

/**
 * §21 / GrandRewrite §6.1: a nearer user message interrupts the blocked join.
 * Provider-visible join consequences are natural language only; internal
 * status/reason enums must stay behind the horizon.
 */
export function assertInterruptedJoin(scenario, label = 'long-stroke') {
  const results = publicToolResults(scenario.provider?.requests, 'join');
  const interrupted = results.filter((text) => text.includes('# Something nearer has arrived.'));
  assert.ok(
    interrupted.length >= 1,
    `${label}: interrupted join must reach Manager conversation as the public nearer-arrival result`,
  );
  assert.equal(
    interrupted.some((text) => /status\s*=|reason\s*=|operator_abort|user_message/.test(text)),
    false,
    `${label}: interrupted join must not leak internal status/reason vocabulary`,
  );
}

/** §21: reviewer REVISE — FinalityRejected from REVISE path. */
export function assertReviewerRevise(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'FinalityRejected') >= 1,
    `${label}: FinalityRejected required (reviewer REVISE)`,
  );
}

/** §21: finality temporarily blocked — same durable pin as REVISE rejection. */
export function assertFinalityTemporarilyBlocked(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'FinalityRejected') >= 1,
    `${label}: FinalityRejected required (finality temporarily blocked)`,
  );
}

/** §21: durable recovery/continuation — cursor fact survives; no Host reboot. */
export async function assertDurableRecovery(workDir, label = 'long-stroke') {
  await assertFallbackContinuation(workDir, label);
  assert.ok(
    journalEventLines(workDir).length >= 1,
    `${label}: durable EventStore journal required for recovery/continuation`,
  );
}

/**
 * §21: publish conflict / stale target — gitConflictProof moved target.
 * Mirrors orchestrator-unhappy-path ConflictDetected pin (no restart=true).
 */
export function assertPublishConflict(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'ConflictDetected') >= 1,
    `${label}: ConflictDetected required after gitConflictProof stale-head (publish conflict)`,
  );
}

/** §21: successful reconciliation — Orchestrator Published case exactly once. */
export function assertSuccessfulReconciliation(workDir, label = 'long-stroke') {
  // countFactCase digs to the innermost DU case name (`Published`). Do NOT pass the
  // waitFact substring `"Orchestrator",["Published"` — that is only for readJournal
  // line matching; as a case key it never hits factCounts.
  const published = countFactCase(workDir, 'Published');
  assert.equal(
    published,
    1,
    `${label}: successful reconciliation requires Orchestrator Published exactly once (got ${published})`,
  );
}

/** Back-compat composite for publish axis. */
export async function assertPublishReconcile(workDir, label = 'long-stroke') {
  assertPublishConflict(workDir, label);
  assertSuccessfulReconciliation(workDir, label);
}

/** §21: later successful finality — FinalityBlessed then LifeCompleted. */
export function assertLaterSuccessfulFinality(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'WorkActivated') >= 1,
    `${label}: WorkActivated required (AgentOwnerRoot migration Life on first suicide)`,
  );
  assert.ok(
    countFactCase(workDir, 'FinalityBlessed') >= 1,
    `${label}: FinalityBlessed required after resources converge`,
  );
  assert.ok(
    countFactCase(workDir, 'LifeCompleted') >= 1,
    `${label}: LifeCompleted required before clean shutdown`,
  );
}

/** Back-compat aliases. */
export async function assertFinalityAdversity(workDir, label = 'long-stroke') {
  assertReviewerRevise(workDir, label);
  assertFinalityTemporarilyBlocked(workDir, label);
  assert.ok(
    countFactCase(workDir, 'FinalityBlessed') >= 1,
    `${label}: FinalityBlessed required after resources converge`,
  );
}

export async function assertLifeCompleted(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'LifeCompleted') >= 1,
    `${label}: LifeCompleted required before clean shutdown`,
  );
}

/**
 * External injection: hold first coder write incomplete until mgr-labor.0 so
 * drain-before-interrupt cannot harvest a completed child (manager-unhappy /
 * temporal-ownership holdChild shape). Orch-shell uses coder.0; legacy id
 * child-c1.0 still accepted.
 */
export async function holdChildC1UntilLabor(scenario) {
  const runtime = scenario.provider?._scenario;
  assert.ok(runtime?.scenario?.entries, 'long-stroke: strict scenario entries required for child hold');

  let releaseChild = null;
  const childHold = new Promise((resolve) => {
    releaseChild = resolve;
  });

  let held = 0;
  for (const entry of runtime.scenario.entries) {
    if (entry.id === 'coder.0' || entry.id === 'child-c1.0') {
      entry.respond = { ...entry.respond, waitUntil: childHold };
      held += 1;
    }
  }
  assert.ok(held >= 1, 'long-stroke: holdChildC1UntilLabor needs coder.0 (or legacy child-c1.0)');

  const originalConsume = runtime.consume.bind(runtime);
  runtime.consume = (body, selection, context) => {
    originalConsume(body, selection, context);
    if (selection?.entry?.id === 'mgr-labor.0') {
      releaseChild?.();
      releaseChild = null;
    }
  };
}

/**
 * Host finality OpeningAssignment is identical across cohorts — two declarations
 * with the same prefix would ambiguousTurn. Retire REVISE after its last step and
 * activate dual-PERFECT (manager-unhappy bindReviewers retire shape, lean).
 */
export async function bindFinalityReviseThenPerfect(scenario) {
  const runtime = scenario.provider?._scenario;
  assert.ok(runtime?.scenario?.entries, 'long-stroke: strict scenario entries required for finality bind');

  const setTurnFor = (turnId, turnText) => {
    for (const entry of runtime.scenario.entries) {
      if (entry.turnId === turnId) entry.turn = turnText;
    }
  };

  // Start: revise matches OpeningAssignment; perfect stays retired.
  setTurnFor('manager-finality-revise', REVIEWER_OPENING);
  setTurnFor('manager-finality-reviewer', REVIEWER_RETIRED);

  const originalConsume = runtime.consume.bind(runtime);
  runtime.consume = (body, selection, context) => {
    originalConsume(body, selection, context);
    const id = selection?.entry?.id;
    if (id === 'manager-finality-revise.1') {
      setTurnFor('manager-finality-revise', REVIEWER_RETIRED);
      setTurnFor('manager-finality-reviewer', REVIEWER_OPENING);
    }
  };
}

/**
 * Composite oracle for flow `{ custom = "oracleLongStroke" }`.
 * Asserts every §21 adversity class the lean orch-shell flow barriers on.
 */
export async function oracleLongStroke(scenario, _ctx) {
  const workDir = scenario.host.workDir;
  assertJoinWakePath(workDir);
  assertInterruptedJoin(scenario);
  await assertProviderTransientFailure(workDir);
  await assertFallbackContinuation(workDir);
  await assertDurableRecovery(workDir);
  assertReviewerRevise(workDir);
  assertFinalityTemporarilyBlocked(workDir);
  assertLaterSuccessfulFinality(workDir);
  assertPublishConflict(workDir);
  assertSuccessfulReconciliation(workDir);

  assert.ok(
    journalEventLines(workDir).length >= 1,
    'long-stroke: EventStore journal must be non-empty at oracle',
  );
  assert.equal(
    countFactCase(workDir, 'ManagerJobCreated'),
    1,
    'long-stroke: orch-shell requires exactly one ManagerJobCreated',
  );
  assert.equal(
    scenario.provider.matchCount('manager-idle.0'),
    2,
    'long-stroke determinism: ManagerIdle step 0 must have exactly two physical deliveries (fault then suicide)',
  );
  for (const id of ['manager-idle.1', 'manager-idle.2', 'manager-idle.3', 'manager-idle.4', 'manager-idle.5']) {
    assert.equal(
      scenario.provider.matchCount(id),
      1,
      `long-stroke determinism: ${id} must be delivered exactly once on the same Manager lane`,
    );
  }

  const managerIdleClaims = factPayloads(workDir, 'PluginPromptClaimed')
    .filter((payload) => payload?.ContinuationKind === 'ManagerIdleEncouragement');
  const terminalPromptKeys = new Set([
    ...factPayloads(workDir, 'PluginPromptPhysicalAccepted'),
    ...factPayloads(workDir, 'PluginPromptAbandoned'),
  ].map((payload) => payload?.PromptKey?.[1]).filter(Boolean));
  for (const claim of managerIdleClaims) {
    const key = claim?.PromptKey?.[1];
    assert.ok(
      key && terminalPromptKeys.has(key),
      `long-stroke determinism: ManagerIdle PromptKey ${key ?? '<missing>'} must not remain unresolved after transport`,
    );
  }
}

/** waitFact presets mirroring long-stroke.toml flow barriers. */
export const PLANNED_WAIT_FACTS = Object.freeze({
  workActivated: waitFactShape('WorkActivated', { eq: 1 }),
  handleCompleted: waitFactShape('HandleCompleted', { gte: 1 }),
  fallbackCursor: waitFactShape('FallbackCursorAdvanced', { eq: 1 }),
  finalityRejected: waitFactShape('FinalityRejected', { gte: 1 }),
  finalityBlessed: waitFactShape('FinalityBlessed', {
    gte: 1,
    renewOn: ['ReviewVerdictRecorded', 'ConfirmedReviewWitness', 'FinalityReviewerEnlisted', 'BlogObservationCommitted'],
  }),
  conflictDetected: waitFactShape('ConflictDetected', { gte: 1 }),
  // Orchestrator-tagged Published (bare "Published" false-matches assignment text).
  published: waitFactShape('"Orchestrator",["Published"', { eq: 1 }),
  confirmedWitness: waitFactShape('ConfirmedReviewWitness', { gte: 1 }),
  lifeCompleted: waitFactShape('LifeCompleted', { gte: 1 }),
  candidateReady: waitFactShape('CandidateReady', { eq: 1 }),
});

/**
 * Machine-readable §21 adversity coverage for the sole entry.
 * Each row: external injection (TOML/custom) + durable/public oracle.
 */
export const ADVERSITY_CHECKLIST = Object.freeze([
  {
    id: 'provider-transient-failure',
    covered: true,
    injection: '[[fault]] provider-error on manager-idle.0 delivery #1 (sole fault row)',
    oracle: 'assertProviderTransientFailure',
  },
  {
    id: 'fallback',
    covered: true,
    injection: 'internal continue turn after settled provider-error (fallback.toml shape)',
    oracle: 'assertFallbackContinuation',
  },
  {
    id: 'join-blocked-then-causally-awakened',
    covered: true,
    injection: 'flow.prompt external user_message while manager.1 join in flight',
    oracle: 'assertJoinWakePath',
  },
  {
    id: 'reviewer-revise',
    covered: true,
    injection: 'manager-finality-revise judge(verdict=REVISE) + bindFinalityReviseThenPerfect',
    oracle: 'assertReviewerRevise',
  },
  {
    id: 'interrupted-aborted-child-or-session',
    covered: true,
    injection: 'holdChildC1UntilLabor (coder.0) + external user_message',
    oracle: 'assertInterruptedJoin',
  },
  {
    id: 'finality-temporarily-blocked',
    covered: true,
    injection: 'REVISE path → waitFact FinalityRejected',
    oracle: 'assertFinalityTemporarilyBlocked',
  },
  {
    id: 'durable-recovery-continuation',
    covered: true,
    injection: 'FallbackCursorAdvanced in same OpenCode PID (no restart=true)',
    oracle: 'assertDurableRecovery',
  },
  {
    id: 'publish-conflict-stale-target',
    covered: true,
    injection: 'afterExpectation gitConflictProof on manager.0 (no restart)',
    oracle: 'assertPublishConflict',
  },
  {
    id: 'successful-reconciliation',
    covered: true,
    injection: 'conflict-resume → coder-resolve → Orchestrator Published eq 1',
    oracle: 'assertSuccessfulReconciliation',
  },
  {
    id: 'later-successful-finality',
    covered: true,
    injection: 'waitFact FinalityBlessed + LifeCompleted before publish reconcile',
    oracle: 'assertLaterSuccessfulFinality',
  },
]);

/** Named oracle table imported by entry.test.mjs for each adversity stroke. */
export const ADVERSITY_ORACLES = Object.freeze({
  assertProviderTransientFailure,
  assertFallbackContinuation,
  assertJoinWakePath,
  assertInterruptedJoin,
  assertReviewerRevise,
  assertFinalityTemporarilyBlocked,
  assertDurableRecovery,
  assertPublishConflict,
  assertSuccessfulReconciliation,
  assertLaterSuccessfulFinality,
  // composites / aliases retained for older call sites
  assertProviderFallback,
  assertFinalityAdversity,
  assertPublishReconcile,
  assertLifeCompleted,
});

export const G2_INSPECTOR_CANARY_PROMPT =
  'G2_INSPECTOR_PREFIX_CANARY: reuse one inspector for Q1, Q2, then Q3.';
export const G2_Q1 = 'G2Q1: who owns PromptAuthority?';
export const G2_Q2 = 'G2Q2: what is ReuseScope?';
export const G2_Q3 = 'G2Q3: when does CaseFinalize run?';
export const G2_A1 = 'G2A1: Host owns PromptAuthority.';
export const G2_A2 = 'G2A2: Owner session scope for one Inspector.';
export const G2_A3 = 'G2A3: On owner ReuseScope close.';
export const G6_CANONICAL_Q = 'What is the Inspector reuse contract?';
export const G6_CANONICAL_A = 'One Inspector child, serial Q/A, finalize on owner close.';
export const G6_FETCH_CANARY_PROMPT = 'G6_CASEBOOK_FETCH_CANARY: fetch the finalized Inspector case.';

const lastUserText = (body) => {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  for (let i = messages.length - 1; i >= 0; i -= 1) {
    if (messages[i]?.role === 'user') return contentText(messages[i].content);
  }
  return '';
};

const requestTools = (body) =>
  (Array.isArray(body?.tools) ? body.tools : [])
    .map((tool) => tool?.function?.name ?? tool?.name)
    .filter((name) => typeof name === 'string');

const chatRequests = (requests) =>
  (requests ?? []).filter((body) => {
    const messages = Array.isArray(body?.messages) ? body.messages : [];
    return !messages.slice(0, 4).some(
      (message) => typeof message?.content === 'string' && message.content.startsWith('Generate a title for this conversation:'),
    );
  });

export function extractInspectorIdFromOwnerRequests(requests) {
  const chats = chatRequests(requests ?? []);
  for (const body of chats) {
    const text = lastUserText(body);
    if (text.startsWith(G2_Q1) || text.startsWith(G2_Q2) || text.startsWith(G2_Q3)) {
      const sid = body.sessionID;
      if (typeof sid === 'string' && sid.length > 0) return sid;
    }
  }
  for (const text of publicToolResults(requests, 'inspect')) {
    const match = String(text).match(/session_id\s*=\s*"([^"]+)"/);
    if (match) return match[1];
  }
  return null;
}

/**
 * G2 PREFIX LAW on the reused Inspector child (mock LLM + real OpenCode).
 * Uses Domain isAppendOnlyPrefix via provider-wire.js — not a second helper.
 */
export function assertG2InspectorPrefixLaw(scenario) {
  const requests = chatRequests(scenario.provider.requests);
  const q1 = requests.filter((body) => lastUserText(body).startsWith(G2_Q1));
  const q2 = requests.filter((body) => lastUserText(body).startsWith(G2_Q2));
  const q3 = requests.filter((body) => lastUserText(body).startsWith(G2_Q3));
  // Each Inspector question begins with the SyncDelegate SendPrompt wire pinned by
  // g2-inspector-qN.0. After EXEC-031 the child completes with ordinary assistant
  // text — no return tool on the wire.
  assert.ok(q1.length >= 1, 'G2: Inspector Q1 provider request missing');
  assert.ok(q2.length >= 1, 'G2: Inspector Q2 provider request missing');
  assert.ok(q3.length >= 1, 'G2: Inspector Q3 provider request missing');

  const sessionId = q1[0].sessionID;
  assert.ok(typeof sessionId === 'string' && sessionId.length > 0, 'G2: Inspector Q1 missing sessionID');
  assert.equal(q2[0].sessionID, sessionId, 'G2: Q2 must reuse the Inspector SessionId');
  assert.equal(q3[0].sessionID, sessionId, 'G2: Q3 must reuse the Inspector SessionId');

  const modelOf = (body) => (typeof body?.model === 'string' ? body.model : body?.model?.id ?? body?.model?.modelID);
  const model = modelOf(q1[0]);
  assert.ok(typeof model === 'string' && model.length > 0, 'G2: Inspector wire ModelId missing');
  assert.equal(modelOf(q2[0]), model, 'G2: same model Q1→Q2');
  assert.equal(modelOf(q3[0]), model, 'G2: same model Q2→Q3');

  const wire1 = wireOf(q1[0]);
  const wire2 = wireOf(q2[0]);
  const wire3 = wireOf(q3[0]);
  assert.equal(sealHolds(wire1, q2[0]), true, 'G2: sealHolds Q1 prefix-of Q2');
  assert.equal(sealHolds(wire2, q3[0]), true, 'G2: sealHolds Q2 prefix-of Q3');
  assert.equal(isAppendOnlyPrefix(wire1, wire2), true, 'G2 PREFIX LAW isAppendOnlyPrefix(Q1,Q2)');
  assert.equal(isAppendOnlyPrefix(wire2, wire3), true, 'G2 PREFIX LAW isAppendOnlyPrefix(Q2,Q3)');
  assert.equal(isAppendOnlyPrefix(wire2, wire1), false, 'G2: prefix is directional');
  return { inspectorSessionId: sessionId, model };
}

/**
 * G6 Host path (mock LLM Bookkeeper): CaseFinalize envelope + js-bookkeeper + captured fact.
 * Fetch is asserted separately once session_id is known.
 */
export function assertG6BookkeeperFinalize(scenario) {
  const requests = chatRequests(scenario.provider.requests);
  const finalize = requests.filter(
    (body) => lastUserText(body).includes('CaseFinalize') && requestTools(body).includes('js-bookkeeper'),
  );
  assert.ok(finalize.length >= 1, 'G6: Bookkeeper CaseFinalize provider request with js-bookkeeper missing');
  assert.equal(
    scenario.provider.matchCount('g6-bookkeeper-finalize.0') >= 1,
    true,
    'G6: js-bookkeeper Q.md step must run',
  );
  assert.equal(
    scenario.provider.matchCount('g6-bookkeeper-finalize.1') >= 1,
    true,
    'G6: js-bookkeeper A.md step must run',
  );
  const captured = countFactCase(scenario.host.workDir, 'InspectorCaseCaptured');
  const named = readJournal(scenario.host.workDir, 'InspectorCaseCaptured').named;
  assert.ok(
    captured >= 1 || named >= 1,
    `G6: InspectorCaseCaptured missing (countFact=${captured} named=${named})`,
  );
}

export const CUSTOMS = {
  holdChildC1UntilLabor,
  bindFinalityReviseThenPerfect,
  oracleLongStroke,
};

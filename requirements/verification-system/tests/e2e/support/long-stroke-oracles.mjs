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
 *   [x] non-10 assessment assigns work      — assertAssessmentAssignsWork
 *   [x] interrupted/aborted child or session— assertInterruptedJoin (+ holdChildC1UntilLabor)
 *   [x] retirement needs successor          — assertRetirementNeedsSuccessor
 *   [x] durable recovery/continuation       — assertDurableRecovery
 *   [x] publish conflict / stale target     — assertPublishConflict
 *   [x] successful reconciliation           — assertSuccessfulReconciliation
 *   [x] later successful retirement         — assertRetirementCommitted
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
import { awaitSessionSettled } from './session-quiescence.js';

/** ≤50ms wall guard matching scenario-driver FACT_WAKE_GUARD_MS. */
const FACT_WAKE_GUARD_MS = 50;

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

/**
 * §21: join blocked then causally awakened — HandleCompleted after user_message wake.
 * The join-wake itself only requires the harvest fact; the full agent lifecycle is
 * proven later by RetirementCommitted (assertRetirementCommitted).
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
  // Synthetic TOML puts the public consequence in the leading instruction.
  // A later successful join may legitimately carry an LWR that quotes the
  // earlier interrupted result; that historical quotation is not a second
  // interrupted join consequence and must not be classified as one.
  const interrupted = results.filter((text) => text.startsWith('# Something nearer has arrived.\n'));
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

/** §21: non-10 assessment assigns work (AssessmentCommitted). */
export function assertAssessmentAssignsWork(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'AssessmentCommitted') >= 1,
    `${label}: AssessmentCommitted required (relay non-10 assessment assigns work)`,
  );
}

/** §21: retirement needs successor — non-10 assessment blocks publication and requires a successor. */
export function assertRetirementNeedsSuccessor(workDir, label = 'long-stroke') {
  const transactions = factPayloads(workDir, 'TransactionCommitted');
  const hasSuccessorRequest = transactions.some((tx) => JSON.stringify(tx).includes('SuccessorRequested'));
  assert.ok(
    hasSuccessorRequest,
    `${label}: SuccessorRequested required (finality temporarily blocked, successor needed)`,
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

/** §21 / MANAGED-SESSION-020: subagent reuse — same child session reused across distinct task runs. */
export function assertSubagentReuse(workDir, label = 'long-stroke') {
  const linked = factPayloads(workDir, 'HandleLinked');
  const proofFinisherLinks = linked.filter((p) => p.Byname === 'Proof Writer' || p.Byname === 'Proof Finisher');
  assert.ok(
    proofFinisherLinks.length >= 2,
    `${label}: Proof Writer must be linked at least twice for subagent reuse (got ${proofFinisherLinks.length})`,
  );
  const firstChild = proofFinisherLinks[0].ChildSessionId;
  const secondChild = proofFinisherLinks[1].ChildSessionId;
  assert.deepEqual(
    firstChild,
    secondChild,
    `${label}: subagent reuse must preserve the same physical child session (first=${firstChild}, second=${secondChild})`,
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

/** §21: later successful retirement — RetirementCommitted after resources converge. */
export function assertRetirementCommitted(workDir, label = 'long-stroke') {
  assert.ok(
    countFactCase(workDir, 'RetirementCommitted') >= 1,
    `${label}: RetirementCommitted required after resources converge`,
  );
}

/**
 * Hold first coder write incomplete until the active user message is admitted,
 * so drain-before-interrupt cannot harvest a completed child (manager-unhappy /
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

  scenario.releaseHeldChild = () => {
    releaseChild?.();
    releaseChild = null;
  };
}

/**
 * Script the repeated fixed successor prompt without inventing a Reviewer identity.
 * Delivery #1 independently certifies the pre-rebase snapshot. Delivery #2 sees
 * the machine conflict, deliberately leaves one quality dimension open, and gains
 * WorkOwned so it can reuse Proof Writer for repair. Delivery #3 independently
 * certifies the repaired snapshot. Every logical step still crosses the real
 * review/suicide/fork tools and durable Relay facts.
 */
export async function bindRelaySuccessorSequence(scenario) {
  const runtime = scenario.provider?._scenario;
  assert.ok(runtime?.scenario?.entries, 'long-stroke: strict scenario entries required for Relay successor bind');

  const auditEntry = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'successor' && entry.step === 0,
  );
  const actionEntry = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'successor' && entry.step === 1,
  );
  assert.ok(auditEntry, 'long-stroke: successor audit entry is required');
  assert.ok(actionEntry, 'long-stroke: successor action entry is required');

  const scores = (completeness) => ({
    language_algorithms: 10,
    simplicity: 10,
    structure: 10,
    granularity: 10,
    tests_evidence: 10,
    logic_reliability_boundaries: 10,
    caller_ergonomics: 10,
    completeness,
  });
  const perfectAudit = () => ({
    type: 'tool-call',
    tool: 'review',
    prefixText: 'Independent audit of this successor snapshot finds every required quality dimension complete and supported by the current workspace evidence.',
    args: scores(10),
  });
  const repairAudit = () => ({
    type: 'tool-call',
    tool: 'review',
    prefixText: 'Independent audit finds the rebase conflict still requires owned repair work, so completeness remains open on this snapshot.',
    args: scores(9),
  });
  const retire = () => ({ type: 'tool-call', tool: 'suicide', args: {} });
  const repair = () => ({
    type: 'tool-call',
    tool: 'fork',
    args: {
      name: 'Proof Writer',
      charge: 'Resolve the conflicted publish_proof.txt so it contains exactly: Published by long-stroke canary',
    },
  });
  auditEntry.respond = perfectAudit();
  actionEntry.respond = retire();

  let successorActions = 0;
  const consume = runtime.consume;
  const originalConsume = (body, selection, context) => consume.call(runtime, body, selection, context);
  runtime.consume = (body, selection, context) => {
    originalConsume(body, selection, context);
    if (selection?.entry?.id === 'successor.1') {
      successorActions += 1;
      queueMicrotask(() => {
        if (successorActions === 1) {
          auditEntry.respond = repairAudit();
          actionEntry.respond = repair();
        } else if (successorActions === 2) {
          auditEntry.respond = perfectAudit();
          actionEntry.respond = retire();
        }
      });
    }
  };
}

export function assertNativeReadProbeTimeline(scenario) {
  const probeUpdates = (scenario.events?.allEvents ?? []).filter((event) => {
    const part = event?.properties?.part;
    return event?.type === 'message.part.updated'
      && part?.type === 'tool'
      && part?.tool === 'read'
      && part?.state?.input?.filePath === 'read_probe.txt';
  });
  assert.ok(probeUpdates.length >= 1, 'long-stroke read probe: message.part.updated missing');

  const probePart = probeUpdates[0].properties.part;
  const callID = probePart.callID ?? probePart.callId;
  const partID = probePart.id;
  const sessionID = probePart.sessionID ?? probePart.sessionId ?? probeUpdates[0].sessionID;
  assert.ok(callID, 'long-stroke read probe: callID missing');
  assert.ok(partID, 'long-stroke read probe: partID missing');
  assert.ok(sessionID, 'long-stroke read probe: sessionID missing');

  const samePartUpdates = (scenario.events?.allEvents ?? []).filter((event) =>
    event?.type === 'message.part.updated'
    && event?.properties?.part?.id === partID,
  );
  const terminal = samePartUpdates.find((event) =>
    ['completed', 'error'].includes(event?.properties?.part?.state?.status),
  );
  assert.ok(terminal, 'long-stroke read probe: no completed/error ToolPart state observed');

  const start = samePartUpdates[0]?.time ?? terminal.time;
  const timeline = samePartUpdates.map((event) => {
    const state = event.properties.part.state ?? {};
    return {
      dtMs: event.time - start,
      seq: event.seq,
      status: state.status ?? null,
      error: state.error ?? state.errorText ?? null,
      interrupted: state.metadata?.interrupted ?? null,
    };
  });
  const nativeToolTerminals = (scenario.events?.allEvents ?? [])
    .filter((event) => {
      const part = event?.properties?.part;
      return event?.type === 'message.part.updated'
        && ['read', 'glob', 'grep'].includes(part?.tool)
        && ['completed', 'error'].includes(part?.state?.status);
    })
    .map((event) => {
      const part = event.properties.part;
      return {
        seq: event.seq,
        sessionID: part.sessionID ?? part.sessionId ?? event.sessionID,
        tool: part.tool,
        callID: part.callID ?? part.callId,
        status: part.state.status,
        error: part.state.error ?? part.state.errorText ?? null,
        interrupted: part.state.metadata?.interrupted ?? null,
      };
    });
  console.log(
    `[read-probe] session=${sessionID} call=${callID} part=${partID} timeline=${JSON.stringify(timeline)} nativeTerminals=${JSON.stringify(nativeToolTerminals)}`,
  );
  return { sessionID, callID, partID, timeline, nativeToolTerminals };
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
  assertAssessmentAssignsWork(workDir);
  assertRetirementNeedsSuccessor(workDir);
  assertRetirementCommitted(workDir);
  assertPublishConflict(workDir);
  assertSubagentReuse(workDir);
  assertSuccessfulReconciliation(workDir);
  assertNativeReadProbeTimeline(scenario);

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
    scenario.provider.matchCount('manager.0'),
    1,
    'long-stroke determinism: the initial Manager step is delivered once',
  );
  assert.equal(
    scenario.provider.matchCount('continue.1'),
    1,
    'long-stroke determinism: the interrupted join closes the superseded provider turn exactly once',
  );
  assert.equal(
    scenario.provider.matchCount('manager.1'),
    1,
    'long-stroke determinism: the active Manager join owns the sole non-retryable provider fault',
  );
  assert.equal(
    scenario.provider.matchCount('continue.0'),
    1,
    'long-stroke determinism: the confirmed failure advances to exactly one fallback step',
  );
  assert.equal(
    scenario.provider.matchCount('manager-resume.0'),
    1,
    'long-stroke determinism: manager-resume.0 must be delivered exactly once',
  );
  const guardedSuffix = Array.from({ length: 10 }, (_, index) => `manager-join-guard.${index}`);
  for (const id of ['successor.0', 'successor.1', 'successor.2', 'successor.3']) {
    assert.ok(
      scenario.provider.matchCount(id) >= 1,
      `long-stroke determinism: ${id} must be delivered across relay incumbencies`,
    );
  }
  const guardedDeliveries = guardedSuffix.reduce(
    (total, id) => total + scenario.provider.matchCount(id),
    0,
  );
  assert.ok(
    guardedDeliveries === 0 || guardedDeliveries === guardedSuffix.length,
    'long-stroke determinism: the Manager suffix cannot switch turn identity after selection',
  );

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
  handleCompleted: waitFactShape('HandleCompleted', { gte: 1 }),
  fallbackCursor: waitFactShape('FallbackCursorAdvanced', { eq: 1 }),
  assessmentCommitted: waitFactShape('AssessmentCommitted', { gte: 1 }),
  retirementCommitted: waitFactShape('RetirementCommitted', { gte: 1 }),
  successorActivated: waitFactShape('SuccessorActivated', { gte: 1 }),
  conflictDetected: waitFactShape('ConflictDetected', { gte: 1 }),
  rebasedCandidateReady: waitFactShape('RebasedCandidateReady', { gte: 1 }),
  // Orchestrator-tagged Published (bare "Published" false-matches assignment text).
  published: waitFactShape('"Orchestrator",["Published"', { eq: 1 }),
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
    injection: '[[fault]] retryable provider-error on g2-inspector-q1.0 delivery #1',
    oracle: 'assertProviderTransientFailure',
  },
  {
    id: 'fallback',
    covered: true,
    injection: 'non-retryable provider-error on manager.1 followed by continue.0',
    oracle: 'assertFallbackContinuation',
  },
  {
    id: 'join-blocked-then-causally-awakened',
    covered: true,
    injection: 'flow.prompt external user_message while manager.1 join in flight',
    oracle: 'assertJoinWakePath',
  },
  {
    id: 'non10-assessment-assigns-work',
    covered: true,
    injection: 'manager-audit non-10 completeness=9 review → WorkOwned',
    oracle: 'assertAssessmentAssignsWork',
  },
  {
    id: 'interrupted-aborted-child-or-session',
    covered: true,
    injection: 'holdChildC1UntilLabor (coder.0) + external user_message',
    oracle: 'assertInterruptedJoin',
  },
  {
    id: 'retirement-needs-successor',
    covered: true,
    injection: 'non-10 assessment blocks publication → successor required',
    oracle: 'assertRetirementNeedsSuccessor',
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
    id: 'subagent-session-reuse',
    covered: true,
    injection: 'successor-2 reuses Proof Writer on same child session',
    oracle: 'assertSubagentReuse',
  },
  {
    id: 'successful-reconciliation',
    covered: true,
    injection: 'conflict resolve → rebase candidate → Orchestrator Published eq 1',
    oracle: 'assertSuccessfulReconciliation',
  },
  {
    id: 'later-successful-retirement',
    covered: true,
    injection: 'waitFact RetirementCommitted after resources converge before publish reconcile',
    oracle: 'assertRetirementCommitted',
  },
]);

/** Named oracle table imported by entry.test.mjs for each adversity stroke. */
export const ADVERSITY_ORACLES = Object.freeze({
  assertProviderTransientFailure,
  assertFallbackContinuation,
  assertJoinWakePath,
  assertInterruptedJoin,
  assertAssessmentAssignsWork,
  assertRetirementNeedsSuccessor,
  assertDurableRecovery,
  assertPublishConflict,
  assertSubagentReuse,
  assertSuccessfulReconciliation,
  assertRetirementCommitted,
});

export const G2_INSPECTOR_CANARY_PROMPT =
  'G2_INSPECTOR_PREFIX_CANARY: reuse one inspector for Q1, Q2, then Q3.';
export const G2_Q1 = 'G2Q1: who owns PromptAuthority?';
export const G2_Q2 = 'G2Q2: what is ReuseScope?';
export const G2_Q3 = 'G2Q3: when does CaseFinalize run?';
const G2_Q1_WIRE = '# G2Q1: who owns PromptAuthority?';
const G2_Q2_WIRE = '# G2Q2: what is ReuseScope?';
const G2_Q3_WIRE = '# G2Q3: when does CaseFinalize run?';
export const G2_A1 = 'G2A1: Host owns PromptAuthority.';
export const G2_A2 = 'G2A2: Owner session scope for one Inspector.';
export const G2_A3 = 'G2A3: On owner ReuseScope close.';
export const G2_BATCH_Q1 = 'G2B1: establish the first repository fact.';
export const G2_BATCH_Q2 = 'G2B2: establish the second repository fact.';
export const G2_BATCH_Q3 = 'G2B3: establish the third repository fact.';
export const G2_BATCH_A = 'G2B: all three repository facts were established together.';
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
    if (text.startsWith(G2_Q1_WIRE) || text.startsWith(G2_Q2_WIRE) || text.startsWith(G2_Q3_WIRE)) {
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

const taggedValue = (value) => (Array.isArray(value) ? value.at(-1) : value);

export async function retireCompanionForDeletion(scenario, ownerSessionId) {
  const findBloggerSessionId = () =>
    factPayloads(scenario.host.workDir, 'CompanionBloggerLinked')
      .filter((payload) => taggedValue(payload?.SessionId) === ownerSessionId)
      .map((payload) => taggedValue(payload?.BloggerSessionId))
      .find((sessionId) => typeof sessionId === 'string' && sessionId.length > 0) ?? null;

  let bloggerSessionId = findBloggerSessionId();
  if (bloggerSessionId === null) {
    bloggerSessionId = await new Promise((resolve, reject) => {
      let stop = () => {};
      const timer = setTimeout(() => {
        stop();
        reject(new Error(`Companion Blogger was not linked for owner ${ownerSessionId}`));
      }, WAIT_FACT_WINDOW_MS);
      stop = watchJournal(scenario.host.workDir, () => {
        scenario.eventCeilings?.checkJournal?.();
        const found = findBloggerSessionId();
        if (found === null) return;
        clearTimeout(timer);
        stop();
        resolve(found);
      });
    });
  }

  const aborted = await scenario.client.abort(bloggerSessionId);
  assert.equal(aborted.ok, true, `Companion Blogger ${bloggerSessionId} abort failed`);
  const settled = await awaitSessionSettled(scenario, bloggerSessionId, WAIT_FACT_WINDOW_MS);
  assert.equal(settled, true, `Companion Blogger ${bloggerSessionId} did not settle`);
  return bloggerSessionId;
}

/**
 * G2 PREFIX LAW on the reused Inspector child (mock LLM + real OpenCode).
 * Uses Domain isAppendOnlyPrefix via provider-wire.js — not a second helper.
 */
export function assertG2InspectorBatchCoalescing(scenario, expectedInspectorSessionId) {
  const requests = chatRequests(scenario.provider.requests);
  const batches = requests.filter((body) => {
    if (body?.sessionID !== expectedInspectorSessionId) return false;
    const text = lastUserText(body);
    return [G2_BATCH_Q1, G2_BATCH_Q2, G2_BATCH_Q3].every((question) => text.includes(question));
  });
  assert.equal(batches.length, 1, 'G2 batch: three simultaneous inspect calls must become one Inspector provider request');
  assert.equal(
    batches[0].sessionID,
    expectedInspectorSessionId,
    'G2 batch: simultaneous inspect batch must reuse the dedicated Inspector session',
  );
  assert.equal(
    scenario.provider.matchCount('g2-inspector-batch.0'),
    1,
    'G2 batch: combined Inspector provider request must be delivered exactly once',
  );

  const failures = publicToolResults(scenario.provider.requests, 'inspect')
    .filter((text) => /could not complete|未能完成/i.test(String(text)));
  assert.deepEqual(failures, [], 'G2 batch: sibling inspect calls must not fail while the canonical call remains in flight');
}

export function assertG2InspectorPrefixLaw(scenario) {
  const requests = chatRequests(scenario.provider.requests);
  const q1 = requests.filter((body) => lastUserText(body).startsWith(G2_Q1_WIRE));
  const q2 = requests.filter((body) => lastUserText(body).startsWith(G2_Q2_WIRE));
  const q3 = requests.filter((body) => lastUserText(body).startsWith(G2_Q3_WIRE));
  // Each Inspector question begins with the SyncDelegate SendPrompt wire pinned by
  // g2-inspector-qN.0. After EXEC-031 the child completes with ordinary assistant
  // text — no return tool on the wire.
  assert.ok(q1.length >= 2, 'G2: Inspector Q1 must record both faulted attempt and retry');
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

  const wire1 = wireOf(q1[q1.length - 1]);
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
    'G6: one atomic js-bookkeeper program must reshape the staged Case',
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
  bindRelaySuccessorSequence,
  oracleLongStroke,
};

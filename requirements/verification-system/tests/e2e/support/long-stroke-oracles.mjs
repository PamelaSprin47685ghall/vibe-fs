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
 * Pure manager loop: every iteration starts from the same typed authority user
 * messages and current workspace for independent assessment. A Continue
 * retirement is followed by another ordinary IncumbencyOpened event and a
 * physically observed provider request on the same SessionId/LogicalRun;
 * Accepted exits. The internal wake is stripped from the provider projection,
 * which carries the authoritative user messages with the current iteration.
 *
 * §21 checklist (mirrors long-stroke.toml comments + ADVERSITY_CHECKLIST export):
 *   [x] provider transient failure          — assertProviderTransientFailure
 *   [x] provider failure continuation       — assertProviderFailureContinuation
 *   [x] join blocked then causally awakened — assertJoinWakePath
 *   [x] non-10 assessment assigns work      — assertAssessmentAssignsWork
 *   [x] interrupted/aborted child or session— assertInterruptedJoin (+ holdChildC1UntilLabor)
 *   [x] retirement needs next iteration    — assertRetirementNeedsIteration (Continue → fresh IncumbencyOpened)
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

/** §21: consecutive provider failures advance two exact recovery episodes. */
export async function assertProviderTransientFailure(workDir, label = 'long-stroke') {
  const facts = factPayloads(workDir, 'FailureRecorded');
  assert.equal(
    facts.length,
    2,
    `${label}: two settled provider-errors must advance distinct recovery episodes (got ${facts.length})`,
  );
}

/** §21: provider failure continuation — same Logical Run; both failed ProviderRuns are accounted. */
export async function assertProviderFailureContinuation(workDir, label = 'long-stroke') {
  assert.equal(
    countFactCase(workDir, 'FailureRecorded'),
    2,
    `${label}: FailureRecorded count must be 2 after consecutive recovery`,
  );
}

function assertConsecutiveRecoveryEpisodes(scenario, ctx, lines) {
  const claims = factPayloads(lines, 'PluginPromptClaimed').filter(
    (payload) => payload?.ContinuationKind === 'ProviderRetryAttempt',
  );
  assert.equal(claims.length, 2, 'long-stroke: each failed ProviderRun must claim one recovery continuation');

  const payloadDigests = claims.map((payload) => payload?.PayloadDigest);
  assert.ok(payloadDigests.every((digest) => typeof digest === 'string' && digest.startsWith('provider-recovery:')));
  assert.equal(new Set(payloadDigests).size, 2, 'long-stroke: distinct failed ProviderRuns must have distinct recovery identities');

  const promptKeys = claims.map((payload) => JSON.stringify(payload?.PromptKey));
  assert.equal(new Set(promptKeys).size, 2, 'long-stroke: each recovery episode must own a distinct durable prompt claim');

  const physical = factPayloads(lines, 'PluginPromptPhysicalAccepted');
  for (const promptKey of promptKeys) {
    assert.equal(
      physical.filter((payload) => JSON.stringify(payload?.PromptKey) === promptKey).length,
      1,
      'long-stroke: each recovery claim must cross physical acceptance exactly once',
    );
  }

  assert.equal(
    factPayloads(lines, 'RetryExhausted').length,
    0,
    'long-stroke: no terminal exhaustion may race either admitted recovery',
  );

  const rawProviderErrors = (scenario.events?.allEvents ?? []).filter(
    (event) => event?.type === 'session.error' && event?.properties?.sessionID === ctx.childId,
  );
  assert.equal(rawProviderErrors.length, 2, 'long-stroke: the Host must expose exactly the two injected raw provider failures');
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
    `${label}: AssessmentCommitted required (non-10 assessment assigns work)`,
  );
}

/**
 * §21: retirement needs next iteration — a non-10 assessment blocks publication
 * and the incumbency retires with Outcome Continue, followed by a fresh
 * IncumbencyOpened on the same authority. Detected via stringified case names
 * (Continue vs Accepted) so no Fable DU field layout is pinned.
 */
export function assertRetirementNeedsIteration(workDir, label = 'long-stroke') {
  const retirements = factPayloads(workDir, 'RetirementCommitted');
  const hasContinue = retirements.some((summary) => JSON.stringify(summary ?? {}).includes('Continue'));
  assert.ok(
    hasContinue,
    `${label}: RetirementCommitted with Outcome Continue required (finality temporarily blocked, next iteration needed)`,
  );
  assert.ok(
    countFactCase(workDir, 'IncumbencyOpened') >= 2,
    `${label}: fresh IncumbencyOpened required after the Continue retirement (same authority, new iteration)`,
  );
}

/** §21: durable recovery/continuation — provider failure fact survives; no Host reboot. */
export async function assertDurableRecovery(workDir, label = 'long-stroke') {
  await assertProviderFailureContinuation(workDir, label);
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

/** §21 / MANAGED-SESSION-020: serial Inspector work reuses one physical child session. */
export function assertSubagentReuse(scenario) {
  const { inspectorSessionId } = assertG2InspectorPrefixLaw(scenario);
  assert.ok(inspectorSessionId, 'long-stroke: Inspector reuse must preserve one physical child session');
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
 * Script the owner-driven Manager incarnations without inventing a Reviewer identity.
 * The initial authority prompt and later manager-assess resource keep separate
 * declarations; the reopened owner resource serves review or suicide closes
 * by delivery: initial-low→work, candidate-perfect→finish, conflict-low→repair,
 * repaired-perfect→finish, rebased-perfect→finish. HumanRoot:
 * low→Continue, perfect→Accepted.
 * Every logical step still crosses the real review/suicide/resume tools and durable
 * IncumbencyOpened/RetirementCommitted facts. Iterations are never distinguished
 * by prompt text.
 */
export async function bindManagerLoopSequence(scenario) {
  const runtime = scenario.provider?._scenario;
  assert.ok(runtime?.scenario?.entries, 'long-stroke: strict scenario entries required for loop iteration bind');

  const loopAudit = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'manager-loop' && entry.step === 0,
  );
  const loopAction = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'manager-loop' && entry.step === 1,
  );
  const loopJoin = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'manager-loop' && entry.step === 2,
  );
  assert.ok(loopAudit, 'long-stroke: manager-loop audit entry is required');
  assert.ok(loopAction, 'long-stroke: manager-loop action entry is required');
  assert.ok(loopJoin, 'long-stroke: manager-loop join entry is required');
  const initialLoopAction = loopAction.respond;
  const initialLoopJoin = loopJoin.respond;
  const currentActionTodo = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'manager-current-action' && entry.step === 0,
  );
  assert.ok(currentActionTodo, 'long-stroke: Manager current-action todowrite is required');
  const currentActionTodoResponse = currentActionTodo.respond;
  const humanAudit = runtime.scenario.entries.find(
    (entry) => entry.turnId === 'humanroot-loop' && entry.step === 0,
  );
  assert.ok(humanAudit, 'long-stroke: humanroot-loop audit entry is required');

  const scores = (completeness) => ({
    language_algorithms: 'PERFECT',
    simplicity: 'PERFECT',
    structure: 'PERFECT',
    granularity: 'PERFECT',
    tests_evidence: 'PERFECT',
    logic_reliability_boundaries: 'PERFECT',
    caller_ergonomics: 'PERFECT',
    completeness,
  });
  const candidatePerfect = () => ({
    type: 'tool-call',
    tool: 'review',
    prefixText: 'Independent audit of this iteration snapshot finds every required quality dimension complete and supported by the current workspace evidence.',
    args: scores('PERFECT'),
  });
  const repairAudit = () => ({
    type: 'tool-call',
    tool: 'review',
    prefixText: 'Independent audit finds the rebase conflict still requires owned repair work, so completeness remains open on this snapshot.',
    args: scores('REVISE'),
  });
  const humanPerfect = () => ({
    type: 'tool-call',
    tool: 'review',
    prefixText: 'HumanRoot loop next iteration independently audits the current snapshot as complete.',
    args: scores('PERFECT'),
  });
  const retire = () => ({ type: 'tool-call', tool: 'suicide', args: {} });
  const joinOwnedWork = () => ({ type: 'tool-call', tool: 'join', args: {} });
  // Initial deliveries stay as declared (low audit + work fork; HumanRoot low).
  // Later responses are selected by the new incarnation's audit delivery count.
  let latestManagerAuditAttempt = 0;
  let managerTodoDelivered = false;
  let initialWorkJoined = false;
  let repairWorkJoined = false;
  const consume = runtime.consume;
  const originalConsume = (body, selection, context) => consume.call(runtime, body, selection, context);
  runtime.consume = (body, selection, context) => {
    const { entry, attempt } = selection ?? {};
    if (entry?.id === 'manager-loop.0') {
      latestManagerAuditAttempt = Math.max(latestManagerAuditAttempt, attempt);
      if (attempt > 1) entry.respond = attempt === 3 ? repairAudit() : candidatePerfect();
    } else if (entry?.turnId === 'manager-reopened-loop' && (entry.step === 0 || entry.id === 'manager-reopened-loop.0')) {
      latestManagerAuditAttempt = Math.max(latestManagerAuditAttempt + 1, attempt ?? 1);
      const n = latestManagerAuditAttempt;
      if (n > 1) {
        entry.respond = n === 3 ? repairAudit() : candidatePerfect();
      }
    } else if (entry?.id === 'manager-loop.1') {
      entry.respond = latestManagerAuditAttempt === 1
        ? initialLoopAction
        : latestManagerAuditAttempt === 3
          ? {
              type: 'tool-call',
              tool: 'fork',
              args: {
                calling: 'coder',
                name: 'Conflict Resolver',
                charge: 'Resolve the conflicted publish_proof.txt so it contains exactly: Published by long-stroke canary',
              },
            }
          : retire();
    } else if (entry?.id === 'manager-loop.2') {
      entry.respond = latestManagerAuditAttempt === 1
        ? initialLoopJoin
        : latestManagerAuditAttempt === 3
          ? { type: 'text', text: 'Repair dispatched; await the owner work resource.' }
          : retire();
    } else if (entry?.id === 'manager-t1-commitment.0') {
      managerTodoDelivered = true;
    } else if (entry?.turnId === 'manager-current-action') {
      if (!managerTodoDelivered) {
        entry.respond = currentActionTodoResponse;
        managerTodoDelivered = true;
      } else if (latestManagerAuditAttempt === 1 && !initialWorkJoined) {
        entry.respond = joinOwnedWork();
        initialWorkJoined = true;
      } else if (latestManagerAuditAttempt === 3 && !repairWorkJoined) {
        entry.respond = joinOwnedWork();
        repairWorkJoined = true;
      } else {
        entry.respond = retire();
      }
    } else if (entry?.id === 'humanroot-loop.0' && attempt > 1) {
      entry.respond = humanPerfect();
    }
    originalConsume(body, selection, context);
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
export async function oracleLongStroke(scenario, ctx) {
  const workDir = scenario.host.workDir;
  const journalLines = journalEventLines(workDir);
  assertJoinWakePath(workDir);
  assertInterruptedJoin(scenario);
  await assertProviderTransientFailure(workDir);
  await assertProviderFailureContinuation(workDir);
  await assertDurableRecovery(workDir);
  assertAssessmentAssignsWork(workDir);
  assertRetirementNeedsIteration(workDir);
  assertRetirementCommitted(workDir);
  assertPublishConflict(workDir);
  assertSubagentReuse(scenario);
  assertSuccessfulReconciliation(workDir);
  assertNativeReadProbeTimeline(scenario);

  // HumanRoot preflow baseline (2 assessments / 2 retirements / 2 openings) is
  // already proven exact before the main spine; global gte checks above would
  // pass on preflow alone. Preserve their main-spine meaning by requiring the
  // current loop itself to own every expected iteration and outcome, not merely
  // the global journal.
  const currentLoopId = ctx?.childId ?? null;
  if (typeof currentLoopId === 'string' && currentLoopId.length > 0) {
    const loopTransactions = factPayloads(workDir, 'TransactionCommitted')
      .filter((payload) => payload?.RoadId?.[1] === currentLoopId);
    const loopCases = loopTransactions.flatMap((payload) => payload?.Transaction?.[1] ?? []);
    const logicalLoopCases = [...new Map(
      loopCases.map((event) => [JSON.stringify(event), event]),
    ).values()];
    const retirements = logicalLoopCases
      .filter((event) => event?.[0] === 'RetirementCommitted')
      .map((event) => event[1]);
    const openings = logicalLoopCases.filter((event) => event?.[0] === 'IncumbencyOpened');
    assert.equal(
      openings.length,
      5,
      'long-stroke: exact replay may repeat an envelope, but current loop must own five logical iterations',
    );
    assert.equal(retirements.length, 5, 'long-stroke: every current-loop iteration must retire');
    assert.equal(
      retirements.filter(isContinueOutcome).length,
      2,
      'long-stroke: work and conflict repair must retire with Continue',
    );
    assert.equal(
      retirements.filter(isAcceptedOutcome).length,
      3,
      'long-stroke: candidate, repaired, and rebased snapshots must retire with Accepted certificates',
    );
  }

  // Pure-loop removals: an event-only fake continuation (IncumbencyOpened without
  // a physically observed provider request) must not satisfy managed admission.
  // The delivery counts below prove every new iteration crossed the provider.
  assertManagerLoopAuthorityPreserved(scenario, ctx?.childId ?? null);

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
    scenario.provider.matchCount('manager-loop.0'),
    5,
    'long-stroke determinism: initial, candidate, repair, repaired, and rebased snapshots each receive one authority audit',
  );
  const linkedByname = factPayloads(workDir, 'HandleLinked').map((payload) => payload?.Byname);
  assert.equal(
    linkedByname.filter((name) => name === 'Proof Writer').length,
    1,
    'long-stroke: initial implementation must create one exact Proof Writer handle',
  );
  assert.equal(
    linkedByname.filter((name) => name === 'Conflict Resolver').length,
    1,
    'long-stroke: conflict repair must create one exact Conflict Resolver handle',
  );
  assert.equal(
    scenario.provider.matchCount('continue.1'),
    1,
    'long-stroke determinism: the interrupted join closes the superseded provider turn exactly once',
  );
  assert.equal(
    scenario.provider.matchCount('continue.0'),
    2,
    'long-stroke determinism: the first recovery fails and the second physical delivery succeeds',
  );
  assertConsecutiveRecoveryEpisodes(scenario, ctx, journalLines);

  const managerJoinResults = publicToolResults(scenario.provider?.requests, 'join');
  assert.equal(
    managerJoinResults.filter((text) => text.startsWith('# Something nearer has arrived.\n')).length,
    1,
    'long-stroke: provider recovery must not manufacture another interrupted or terminal join result',
  );
  const guardedSuffix = Array.from({ length: 10 }, (_, index) => `manager-join-guard.${index}`);
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
  providerFailure: waitFactShape('FailureRecorded', { eq: 2 }),
  assessmentCommitted: waitFactShape('AssessmentCommitted', { gte: 1 }),
  retirementCommitted: waitFactShape('RetirementCommitted', { gte: 1 }),
  incumbencyOpened: waitFactShape('IncumbencyOpened', { gte: 1 }),
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
    id: 'provider-failure-continuation',
    covered: true,
    injection: 'non-retryable provider-error on manager.1 followed by continue.0',
    oracle: 'assertProviderFailureContinuation',
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
    injection: 'manager-audit non-10 completeness=9 review → work assigned',
    oracle: 'assertAssessmentAssignsWork',
  },
  {
    id: 'interrupted-aborted-child-or-session',
    covered: true,
    injection: 'holdChildC1UntilLabor (coder.0) + external user_message',
    oracle: 'assertInterruptedJoin',
  },
  {
    id: 'retirement-needs-iteration',
    covered: true,
    injection: 'non-10 assessment blocks publication → Continue retirement → fresh IncumbencyOpened',
    oracle: 'assertRetirementNeedsIteration',
  },
  {
    id: 'durable-recovery-continuation',
    covered: true,
    injection: 'FailureRecorded in same OpenCode PID (no restart=true)',
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
    injection: 'G2 Q1→Q2→Q3 and simultaneous batch reuse one Inspector child session',
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
  assertProviderFailureContinuation,
  assertJoinWakePath,
  assertInterruptedJoin,
  assertAssessmentAssignsWork,
  assertRetirementNeedsIteration,
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

export const HUMANROOT_MANAGER_LOOP_CANARY_PROMPT =
  'HUMANROOT_MANAGER_LOOP_CANARY: run the independent HumanRoot manager assessment check.';

export const HUMANROOT_CANARY_DELTAS = Object.freeze({
  assessments: 2,
  retirements: 2,
  incumbencyOpenings: 2,
});

// ── pure-loop helpers ─────────────────
//
// RetirementOutcome is detected via stringified case names (Continue vs
// Accepted) so no Fable DU field layout is pinned. Incumbency identity is
// collected as every distinct `incumbency:`-prefixed string inside
// IncumbencyOpened payloads, tolerating tuple-vs-record JSON shapes.
const isContinueOutcome = (summary) => summary?.Outcome === 'Continue';

const isAcceptedOutcome = (summary) =>
  Array.isArray(summary?.Outcome)
  && summary.Outcome[0] === 'Accepted'
  && Array.isArray(summary.Outcome[1])
  && summary.Outcome[1][0] === 'QualityCertificateId';

const incumbencyIdsIn = (payloads) => {
  const ids = new Set();
  const walk = (value) => {
    if (typeof value === 'string') {
      for (const match of value.matchAll(/incumbency:[0-9a-f]+/g)) ids.add(match[0]);
      return;
    }
    if (Array.isArray(value)) {
      for (const item of value) walk(item);
      return;
    }
    if (value && typeof value === 'object') {
      for (const child of Object.values(value)) walk(child);
    }
  };
  for (const payload of payloads ?? []) walk(payload);
  return [...ids];
};

const messageTextsByRole = (request, role) =>
  (request?.messages ?? [])
    .filter((message) => message?.role === role)
    .map((message) => contentText(message?.content));

const hasAssistantOrToolMessages = (request) =>
  (request?.messages ?? []).some((message) =>
    message?.role === 'assistant' || message?.role === 'tool' || message?.role === 'toolResult',
  );

const normalizeHostModelBanner = (text) => text.replace(
  /You are powered by the model named [^\n]*?\. The exact model ID is [^\n]+/g,
  '<host-model>',
);

const providerPlanOf = (request) => ({
  tools: (Array.isArray(request?.tools) ? request.tools : [])
    .map((tool) => tool?.function?.name ?? tool?.name)
    .filter((name) => typeof name === 'string')
    .sort(),
  system: messageTextsByRole(request, 'system').map(normalizeHostModelBanner),
});

/**
 * Assert every fresh iteration preserves the provider-visible authority prefix:
 * same normalized system/tools plan and same typed authority users in order.
 * Later incarnations may append exactly the owner-controlled assessment resource.
 * Compares message structure only.
 */
export function assertManagerLoopAuthorityPreserved(scenario, sessionId) {
  assert.ok(typeof sessionId === 'string' && sessionId.length > 0, 'manager-loop: session id required');
  const requests = canaryRequestsFor(scenario, sessionId);
  assert.ok(requests.length >= 2, `manager-loop: expected initial + next iteration requests (got ${requests.length})`);
  const firsts = requests.filter((request) => !hasAssistantOrToolMessages(request));
  assert.ok(firsts.length >= 2, `manager-loop: expected at least two iteration-first requests carrying only the current iteration (got ${firsts.length})`);
  const baselinePlan = providerPlanOf(firsts[0]);
  const baselineUsers = messageTextsByRole(firsts[0], 'user');
  assert.ok(baselineUsers.length >= 1, 'manager-loop: initial iteration must carry typed authority user messages');
  const assessmentResource = '# Establish read-only evidence about the current delivery through the entitled offices.';
  for (const [index, request] of firsts.entries()) {
    assert.deepEqual(
      providerPlanOf(request),
      baselinePlan,
      `manager-loop: iteration #${index + 1} must keep the same normalized system/tools plan as the initial iteration`,
    );
    const users = messageTextsByRole(request, 'user');
    assert.deepEqual(
      users.slice(0, baselineUsers.length),
      baselineUsers,
      `manager-loop: iteration #${index + 1} must preserve the typed authority user prefix`,
    );
    assert.ok(
      users.length === baselineUsers.length
        || (users.length === baselineUsers.length + 1 && users.at(-1)?.startsWith(assessmentResource)),
      `manager-loop: iteration #${index + 1} may append only the exact assessment resource`,
    );
  }
}

const canaryRequestsFor = (scenario, sessionId) =>
  (scenario.provider?.requests ?? []).filter((request) => {
    if ((request?.sessionID ?? request?.sessionId ?? null) !== sessionId) return false;
    const messages = Array.isArray(request?.messages) ? request.messages : [];
    return !messages.slice(0, 4).some(
      (message) => typeof message?.content === 'string' && message.content.startsWith('Generate a title for this conversation:'),
    );
  });

const assistantToolCallIds = (requests, tool) => {
  const ids = [];
  for (const request of requests) {
    for (const message of request?.messages ?? []) {
      if (message?.role !== 'assistant' || !Array.isArray(message?.tool_calls)) continue;
      for (const call of message.tool_calls) {
        if ((call?.function?.name ?? call?.name) === tool && typeof call?.id === 'string') {
          ids.push(call.id);
        }
      }
    }
  }
  return ids;
};

/**
 * HumanRoot manager loop canary oracle (preFlow, sole serve).
 *
 * Pure manager loop: a Continue retirement is followed by another ordinary
 * IncumbencyOpened event and a physically observed provider request on the same
 * SessionId/LogicalRun with the same typed authority user messages; Accepted
 * exits. An IncumbencyOpened fact without a matching provider request is the
 * exact event-only fake shape and fails here as missing managed admission.
 * Message-structure and durable-event behavior only, carrying the authoritative
 * user messages with the current iteration for independent assessment.
 *
 * Surfaces only: strict provider wire (managed admission), durable journal facts,
 * and causal session idle. No wall delays, no retries, no time-budget growth.
 */
export async function assertHumanRootManagerLoop(scenario, sessionId, label = 'humanroot-loop') {
  assert.ok(typeof sessionId === 'string' && sessionId.length > 0, `${label}: canary session id required`);
  const workDir = scenario.host.workDir;

  await awaitNamedFact(workDir, waitFactShape('AssessmentCommitted', { eq: 2 }), { timeoutMs: WAIT_FACT_WINDOW_MS });
  await awaitNamedFact(workDir, waitFactShape('RetirementCommitted', { eq: 2 }), { timeoutMs: WAIT_FACT_WINDOW_MS });
  await awaitNamedFact(workDir, waitFactShape('IncumbencyOpened', { eq: 2 }), { timeoutMs: WAIT_FACT_WINDOW_MS });

  // ONE reusable humanroot-loop family: each step delivered twice (initial +
  // next iteration). An IncumbencyOpened fact alone is an event-only fake;
  // physically observed deliveries under the same LogicalRun prove the loop.
  assert.equal(
    scenario.provider.matchCount('humanroot-loop.0'),
    2,
    `${label}: reusable humanroot-loop audit must be delivered twice (low then perfect)`,
  );
  assert.equal(
    scenario.provider.matchCount('humanroot-loop.1'),
    2,
    `${label}: reusable humanroot-loop close must be delivered twice (Continue then Accepted)`,
  );

  const requests = canaryRequestsFor(scenario, sessionId);
  assert.equal(
    requests.length,
    4,
    `${label}: expected exactly 4 chat requests on the canary session (got ${requests.length})`,
  );
  // Same physical SessionId on every request: continuations extend the
  // LogicalRun, they never create a new one. A new session here would be a
  // cold-boundary violation, not a loop iteration.
  for (const request of requests) {
    assert.equal(
      request?.sessionID ?? request?.sessionId ?? null,
      sessionId,
      `${label}: every iteration must stay on the same physical SessionId/LogicalRun`,
    );
  }

  // Pure-loop authority: the next iteration carries the same system/provider
  // plan and the same typed authority user sequence as the initial iteration.
  // Each iteration-first carries only the current iteration, with the internal
  // wake stripped (next first has no assistant/tool at all). ONE reusable
  // humanroot-loop family covers both iterations, so iterations are never
  // distinguished by prompt text; the assertion below is on provider-visible
  // message structure only.
  assertManagerLoopAuthorityPreserved(scenario, sessionId);
  assert.equal(
    hasAssistantOrToolMessages(requests[2]),
    false,
    `${label}: next iteration-first must carry only the current iteration with the wake stripped`,
  );

  const firstIterationReviewIds = assistantToolCallIds([requests[1]], 'review');
  assert.equal(
    firstIterationReviewIds.length,
    1,
    `${label}: first-iteration close must carry exactly the first-iteration review call (got ${firstIterationReviewIds.length})`,
  );
  for (const request of [requests[2], requests[3]]) {
    assert.equal(
      request.messages.some((message) =>
        message.tool_call_id === firstIterationReviewIds[0]
        || message.tool_calls?.some((call) => call.id === firstIterationReviewIds[0])),
      false,
      `${label}: next iteration must exclude the first-iteration review call/result`,
    );
  }

  // Durable loop behavior: two openings (initial + one after Continue), one
  // Continue retirement followed by one Accepted; positive counts prove the loop.
  const openings = factPayloads(workDir, 'IncumbencyOpened');
  assert.equal(openings.length, 2, `${label}: canary road must open exactly two iterations (got ${openings.length})`);
  const openedIds = incumbencyIdsIn(openings);
  assert.equal(openedIds.length, 2, `${label}: iterations must carry distinct incumbencies (got ${JSON.stringify(openedIds)})`);
  const canaryRetirements = factPayloads(workDir, 'RetirementCommitted');
  assert.equal(
    canaryRetirements.length,
    2,
    `${label}: canary road must own exactly two retirements (got ${canaryRetirements.length})`,
  );
  assert.equal(
    canaryRetirements.filter(isContinueOutcome).length,
    1,
    `${label}: first-iteration retirement must be Outcome Continue`,
  );
  assert.equal(
    canaryRetirements.filter(isAcceptedOutcome).length,
    1,
    `${label}: next retirement must be Outcome Accepted with a certificate`,
  );
  assert.equal(
    countFactCase(workDir, 'AssessmentCommitted'),
    HUMANROOT_CANARY_DELTAS.assessments,
    `${label}: preflow must contribute exactly ${HUMANROOT_CANARY_DELTAS.assessments} AssessmentCommitted before the main spine`,
  );
  assert.equal(
    countFactCase(workDir, 'RetirementCommitted'),
    HUMANROOT_CANARY_DELTAS.retirements,
    `${label}: preflow must contribute exactly ${HUMANROOT_CANARY_DELTAS.retirements} RetirementCommitted before the main spine`,
  );
  assert.equal(
    countFactCase(workDir, 'IncumbencyOpened'),
    HUMANROOT_CANARY_DELTAS.incumbencyOpenings,
    `${label}: preflow must contribute exactly ${HUMANROOT_CANARY_DELTAS.incumbencyOpenings} IncumbencyOpened before the main spine`,
  );
  assert.equal(
    countFactCase(workDir, 'ManagerJobCreated'),
    0,
    `${label}: direct HumanRoot canary must not mint a ManagerJob (main spine owns the single ManagerJobCreated)`,
  );

  const settled = await awaitSessionSettled(scenario, sessionId, WAIT_FACT_WINDOW_MS);
  assert.equal(settled, true, `${label}: canary session must settle to idle via causal host events`);
}

export const CUSTOMS = {
  holdChildC1UntilLabor,
  bindManagerLoopSequence,
  oracleLongStroke,
};

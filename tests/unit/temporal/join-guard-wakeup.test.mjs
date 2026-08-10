// tests/unit/temporal/join-guard-wakeup.test.mjs — G4R-2 Race Extraction.
//
// Target: "join blocked then causally awakened" (changes/active/test.md §21 /
// G4R adversity class). Long Stroke still E2E-represents the class via
// assertJoinWakePath (WorkActivated + HandleCompleted). This file strengthens
// the algebra with explicit traces on production HandleProjection / Fold /
// JoinDrain facts — no OpenCode, no second SM.
//
// Algebra (EXEC-009 / EXEC-016 / EXEC-018):
//   HandleLinked → Active          → join blocked (joinable=[], activeHandles>0)
//   HandleCompleted                → CompletedAwaitingJoin → causally awakenable
//                                    (joinable>0; JoinDrain.orderedCandidates)
//   HandleRetired                  → harvested; joinable cleared
//   WorkActivated                  → Manager Activation durable half of the
//                                    Long Stroke wake path (oracle companion)
//
// Still E2E-only (Host / JoinAttempt / user_message wire):
//   • EXEC-017 JoinInterruptReason.UserMessageArrived wire shape
//   • JoinAttemptRegistry fan-out / zero-active drop
//   • JoinGuard Continuation nudge text delivery (HostJoinGuard.nudge)
//
// Traces are enumerated via DeterministicEventQueue. No wall clock is authority.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  completionKind,
  envelope,
  fact,
  fold,
  handleId,
  handleOwnership,
  handleProjection,
  joinDrain,
  managerLifeId,
  managerLifecycleFact,
  physicalUser,
  promptKey,
  roles,
  sessionId,
  stream,
} from '../support/domain.mjs'
import { DeterministicEventQueue } from './harness.mjs'

// ── identities ──────────────────────────────────────────────────────────────

const OWNER = 'ses_mgr'
const CHILD = 'ses_child'
const OWNER_S = sessionId(OWNER)
const CHILD_S = sessionId(CHILD)
const HANDLE = handleId.agent('c1')
const LIFE = managerLifeId('life-join-wake')
const BLOB = blobRef('blob-join-wake')
const DIGEST = blobDigest('d-join-wake')

const env = (seq, f) => envelope({ seq, stream: stream.session(OWNER_S), fact: f })

const views = (handles) => ({
  listable: handleProjection.listable(handles).map((r) => handleProjection.read(r).handle).sort(),
  joinable: handleProjection.joinable(handles).map((r) => handleProjection.read(r).handle).sort(),
  active: handleProjection.activeHandles(handles).map((r) => handleProjection.read(r).handle).sort(),
  abandoned: handleProjection
    .reportableAbandoned(handles)
    .map((r) => handleProjection.read(r).handle)
    .sort(),
})

const candidateHandles = (handles) =>
  joinDrain.orderedCandidates(handles).map((r) => handleProjection.read(r).handle)

const lifeOpened = () =>
  managerLifecycleFact('LifeOpened', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    OpeningUserMessageId: physicalUser('msg-open-join-wake'),
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 1n,
  })

const workActivated = () =>
  managerLifecycleFact('WorkActivated', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    ActivationPromptKey: promptKey('act-join-wake'),
    ProtectedPrefixEndSequence: 7n,
  })

const handleLinked = () =>
  fact('HandleLinked', {
    ParentSessionId: OWNER_S,
    ChildSessionId: CHILD_S,
    Handle: HANDLE,
    TargetAgent: 'fast-coder',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  })

const handleCompleted = () =>
  fact('HandleCompleted', {
    ParentSessionId: OWNER_S,
    Handle: HANDLE,
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  })

const handleRetired = () => fact('HandleRetired', { ParentSessionId: OWNER_S, Handle: HANDLE })

const handlesOf = (projection) => fold.session(projection, OWNER).Handles

// ── Theorem 1: Active = join blocked ────────────────────────────────────────
//
// Long Stroke story beat: child still in flight; Manager joins / waits.
// Durable half: HandleLinked alone → Active → not joinable; JoinDrain has no
// harvest candidate for that handle.

test('THEOREM_join_blocked_while_handle_active', () => {
  let projection = handleProjection.empty
  const linked = handleProjection.link(
    HANDLE,
    CHILD_S,
    'fast-coder',
    roles.of('Coder'),
    projection,
    handleOwnership.durableParentHandle(),
  )
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
  projection = linked.value

  assert.deepEqual(views(projection), {
    listable: ['agent:c1'],
    joinable: [],
    active: ['agent:c1'],
    abandoned: [],
  })
  assert.deepEqual(
    candidateHandles(projection),
    [],
    'JoinDrain.orderedCandidates must not harvest an Active handle (join blocked)',
  )
})

// ── Theorem 2: HandleCompleted causally awakens joinable ────────────────────
//
// Causal awaken = durable completion cell lands → CompletedAwaitingJoin enters
// joinable + orderedCandidates. No scheduler; the fact itself is the wake.

test('THEOREM_handle_completed_causally_awakens_joinable', () => {
  let projection = handleProjection.empty
  projection = handleProjection.link(
    HANDLE,
    CHILD_S,
    'fast-coder',
    roles.of('Coder'),
    projection,
    handleOwnership.durableParentHandle(),
  ).value

  assert.equal(views(projection).joinable.length, 0, 'precondition: still blocked')

  const completed = handleProjection.complete(
    HANDLE,
    handleProjection.completionOf('Terminal'),
    projection,
  )
  assert.equal(completed.ok, true)
  projection = completed.value

  assert.deepEqual(views(projection), {
    listable: ['agent:c1'],
    joinable: ['agent:c1'],
    active: [],
    abandoned: [],
  })
  assert.deepEqual(
    candidateHandles(projection),
    ['agent:c1'],
    'HandleCompleted must place the handle on JoinDrain.orderedCandidates (causally awakened)',
  )

  const retired = handleProjection.retire(HANDLE, projection)
  assert.equal(retired.ok, true)
  assert.deepEqual(views(retired.value), {
    listable: [],
    joinable: [],
    active: [],
    abandoned: [],
  })
  assert.deepEqual(candidateHandles(retired.value), [])
})

// ── Theorem 3: Long Stroke wake-path fold (WorkActivated → HandleCompleted) ─
//
// Mirrors tests/e2e/support/long-stroke-oracles.mjs assertJoinWakePath:
//   WorkActivated ≥ 1 ∧ HandleCompleted ≥ 1
// Explicit blocked→awakened stages on one durable fold trail.

test('THEOREM_join_wake_path_trace_WorkActivated_then_HandleCompleted', () => {
  // Stage A — Activation without child: no join candidates.
  const afterActivation = fold.apply(fold.empty, [env(1, lifeOpened()), env(2, workActivated())])
  assert.equal(afterActivation.ok, true, afterActivation.ok ? '' : JSON.stringify(afterActivation.error))
  assert.equal(
    Number(fold.session(afterActivation.value, OWNER).ManagerLife.CurrentLife.ProtectedPrefixEnd.Sequence),
    7,
  )
  assert.equal(fold.session(afterActivation.value, OWNER).Handles, undefined)

  // Stage B — child linked while Life active: join blocked.
  const afterLink = fold.apply(afterActivation.value, [env(3, handleLinked())])
  assert.equal(afterLink.ok, true, afterLink.ok ? '' : JSON.stringify(afterLink.error))
  const blocked = handlesOf(afterLink.value)
  assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, blocked)).lifecycle, 'Active')
  assert.deepEqual(views(blocked).joinable, [])
  assert.deepEqual(views(blocked).active, ['agent:c1'])
  assert.deepEqual(candidateHandles(blocked), [])

  // Stage C — HandleCompleted: causally awakened for join harvest.
  const afterComplete = fold.apply(afterLink.value, [env(4, handleCompleted())])
  assert.equal(afterComplete.ok, true, afterComplete.ok ? '' : JSON.stringify(afterComplete.error))
  const awakened = handlesOf(afterComplete.value)
  assert.equal(
    handleProjection.read(handleProjection.tryFind(HANDLE, awakened)).lifecycle,
    'CompletedAwaitingJoin',
  )
  assert.deepEqual(views(awakened).joinable, ['agent:c1'])
  assert.deepEqual(candidateHandles(awakened), ['agent:c1'])

  // Stage D — retire (join consume): candidates cleared; Life still open.
  const afterRetire = fold.apply(afterComplete.value, [env(5, handleRetired())])
  assert.equal(afterRetire.ok, true, afterRetire.ok ? '' : JSON.stringify(afterRetire.error))
  const harvested = handlesOf(afterRetire.value)
  assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, harvested)).lifecycle, 'Retired')
  assert.deepEqual(views(harvested).joinable, [])
  assert.deepEqual(candidateHandles(harvested), [])
  assert.ok(fold.session(afterRetire.value, OWNER).ManagerLife.CurrentLife)
})

// ── Theorem 4: WorkActivated ⟂ handle-link race is confluent ────────────────
//
// Activation and HandleLinked are independent axes of the wake story. Any
// interleaving after LifeOpened must leave the same join-blocked projection
// (Active, not joinable). Race is algebra, not scheduler lottery.

test('THEOREM_WorkActivated_and_HandleLinked_interleavings_stay_blocked', () => {
  const prefix = [env(1, lifeOpened())]
  const activation = [env(2, workActivated())]
  const link = [env(3, handleLinked())]

  const traces = DeterministicEventQueue.interleavings(activation, link)
  assert.equal(traces.length, 2, 'explicit traces: Activate;Link and Link;Activate')

  let reference
  for (const mid of traces) {
    const folded = fold.apply(fold.empty, [...prefix, ...mid])
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const handles = handlesOf(folded.value)
    const snap = {
      lifecycle: handleProjection.read(handleProjection.tryFind(HANDLE, handles)).lifecycle,
      views: views(handles),
      candidates: candidateHandles(handles),
      prefixEnd: Number(
        fold.session(folded.value, OWNER).ManagerLife.CurrentLife.ProtectedPrefixEnd.Sequence,
      ),
    }
    assert.equal(snap.lifecycle, 'Active')
    assert.deepEqual(snap.views.joinable, [])
    assert.deepEqual(snap.views.active, ['agent:c1'])
    assert.deepEqual(snap.candidates, [])
    assert.equal(snap.prefixEnd, 7)
    if (!reference) reference = snap
    else assert.deepEqual(snap, reference)
  }
})

// ── Theorem 5: blocked→awakened permutations after shared prefix ────────────
//
// After LifeOpened + WorkActivated + HandleLinked (blocked), the wake step is
// HandleCompleted. Duplicate complete absorbs; complete;retire and
// complete;complete;retire end Retired with empty candidates.

test('THEOREM_blocked_to_awakened_fold_trails_confluent_after_retire', () => {
  const blockedPrefix = [
    env(1, lifeOpened()),
    env(2, workActivated()),
    env(3, handleLinked()),
  ]

  const trails = [
    [env(4, handleCompleted()), env(5, handleRetired())],
    [env(4, handleCompleted()), env(5, handleCompleted()), env(6, handleRetired())],
    [env(4, handleCompleted()), env(5, handleRetired()), env(6, handleRetired())],
  ]

  for (const trail of trails) {
    const folded = fold.apply(fold.empty, [...blockedPrefix, ...trail])
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const handles = handlesOf(folded.value)
    assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, handles)).lifecycle, 'Retired')
    assert.deepEqual(views(handles).joinable, [])
    assert.deepEqual(candidateHandles(handles), [])
  }

  // Explicit race after first complete: duplicate-complete vs retire.
  const afterComplete = fold.apply(fold.empty, [...blockedPrefix, env(4, handleCompleted())])
  assert.equal(afterComplete.ok, true)
  assert.deepEqual(views(handlesOf(afterComplete.value)).joinable, ['agent:c1'])

  for (const interleaving of DeterministicEventQueue.interleavings(
    [env(5, handleCompleted())],
    [env(6, handleRetired())],
  )) {
    const folded = fold.apply(afterComplete.value, interleaving)
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const lifecycle = handleProjection.read(
      handleProjection.tryFind(HANDLE, handlesOf(folded.value)),
    ).lifecycle
    assert.equal(lifecycle, 'Retired')
    assert.deepEqual(candidateHandles(handlesOf(folded.value)), [])
  }
})

// ── Theorem 6: projection transition enumerates blocked → awakened ───────────
//
// Same algebra without Fold envelopes — pure HandleProjection steps matching
// JoinDrain candidate reads. Pins the production read path HostForkRuntime
// uses (joinable + orderedCandidates), not a test-only tryJoin.

test('THEOREM_projection_steps_enumerate_blocked_then_awakened_then_clear', () => {
  const steps = []
  let projection = handleProjection.empty
  steps.push({ stage: 'empty', views: views(projection), candidates: candidateHandles(projection) })

  projection = handleProjection.link(
    HANDLE,
    CHILD_S,
    'fast-coder',
    roles.of('Coder'),
    projection,
    handleOwnership.durableParentHandle(),
  ).value
  steps.push({ stage: 'blocked', views: views(projection), candidates: candidateHandles(projection) })

  projection = handleProjection.complete(
    HANDLE,
    handleProjection.completionOf('Terminal'),
    projection,
  ).value
  steps.push({ stage: 'awakened', views: views(projection), candidates: candidateHandles(projection) })

  projection = handleProjection.retire(HANDLE, projection).value
  steps.push({ stage: 'cleared', views: views(projection), candidates: candidateHandles(projection) })

  assert.deepEqual(
    steps.map((s) => ({
      stage: s.stage,
      joinable: s.views.joinable,
      active: s.views.active,
      candidates: s.candidates,
    })),
    [
      { stage: 'empty', joinable: [], active: [], candidates: [] },
      { stage: 'blocked', joinable: [], active: ['agent:c1'], candidates: [] },
      { stage: 'awakened', joinable: ['agent:c1'], active: [], candidates: ['agent:c1'] },
      { stage: 'cleared', joinable: [], active: [], candidates: [] },
    ],
  )
})

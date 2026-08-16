// Moved from tests/unit/temporal/manager-unhappy-exactly-once.test.mjs (cutover Wave 2a); owner: effect-accounting
// tests/unit/temporal/manager-unhappy-exactly-once.test.mjs — G4R-2 Race Extraction.
//
// Target: manager-unhappy-path historical owner-failure vs blogger-interruption
// double-count / join-guard issues (changes/active/test.md G4R-2 example).
//
// Historical defect (docs/what/fallback.md FALLBACK-013):
//   One owner provider failure was observed twice — by the owner's confirmed-
//   failure path AND by Host abort cleanup marking the Companion blogger's
//   in-flight blog tool interrupted. Different Session / ProviderRun identities
//   meant FALLBACK-003 could not dedupe → the same logical failure was charged
//   twice and AABB trajectory became append-order race.
//   Law: AbortResidue must NOT call FallbackController.recordConfirmedFailure
//   (LOOP-006 / ENFORCER-068). Repair may still inject; cursor must not move.
//
// No OpenCode. No second SM. Every assertion folds through production
// FallbackProjection / Fold / HandleProjection / ManagerLifecycleFact.
// Races are enumerated by DeterministicEventQueue (harness), not wall clocks.
//
// ── Coverage map vs tests/e2e/cases/manager-unhappy-path (+ .toml) ──────────
//
// Theorem-covered HERE (algebra / durable facts):
//   • Owner failure × blogger interrupt-residue interleavings → owner cursor
//     records that logical failure at most once (FALLBACK-013 shape).
//   • Counterfactual: a mistaken owner-stream advance with a distinct
//     blogger-interrupt ProviderRun WOULD double-count (why Host must omit it).
//   • Join-guard durable half (EXEC-009 / mgr-join-guard background):
//     HandleCompleted / HandleRetired are exactly-once under fold replay and
//     projection transitions (AlreadyCompleted / HandleIsRetired).
//   • ManagerLifecycle: WorkActivated identity-idempotent; one LifeCompleted
//     archives exactly one Life (finalOracle LifeCompleted=1 / stroke 13).
//
// Still E2E-only (needs Host / OpenCode / provider wire / acceptance gate):
//   • Stroke 3 user_message wake wire (status=interrupted, reason=user_message)
//   • Stroke 5 premature suicide instruction refusal while C1 outstanding
//   • Stroke 6 hidden reviewers + ConfirmedReviewWitness=0 at first rejection
//   • Strokes 7/9/12 C1 session-snapshot reuse; reviewer cohort dual-PERFECT
//   • GLORY-029 dual ManagerIdleEncouragement (firstIdleReceipt / secondIdle)
//   • Stroke 13 terminal last_words = FINAL via LifeCompleted blob / digest
//   • Acceptance-gate defer/release choreography; TOML provider replies

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  authorityRoot,
  blobDigest,
  blobRef,
  caseOf,
  completionKind,
  cursor,
  envelope,
  fact,
  fallbackProjection,
  finalityRequestId,
  fold,
  gitTreeHash,
  handleId,
  handleOwnership,
  handleProjection,
  listItems,
  logicalRunId,
  managerLifeId,
  managerLifecycleFact,
  physicalUser,
  promptKey,
  providerRun,
  reviewBarrierId,
  roles,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'
import { DeterministicEventQueue } from '../../verification-system/tests/support/temporal-harness.mjs'

// ── identities ──────────────────────────────────────────────────────────────

const OWNER = 'ses_mgr'
const BLOGGER = 'ses_blog'
const CHILD = 'ses_child'
const OWNER_S = sessionId(OWNER)
const BLOGGER_S = sessionId(BLOGGER)
const CHILD_S = sessionId(CHILD)
const RUN_L = logicalRunId('run_L')
const ROOT = authorityRoot('msg_u1')
const HANDLE = handleId.agent('c1')

const fallbackOf = (projection, sessionIdStr) => {
  const sess = fold.session(projection, sessionIdStr)
  if (!sess?.Fallback) return undefined
  return fallbackProjection.read(sess.Fallback)
}

const rootFact = (session = OWNER_S) =>
  fact('AuthorityRootAccepted', {
    SessionId: session,
    LogicalRunId: RUN_L,
    AuthorityRootUserMessageId: ROOT,
    AuthorityKind: 'HumanRoot',
    SelectedAgent: 'fast-manager',
    PeerAgent: 'deep-manager',
    CanonicalRole: 'manager',
    SelectedTier: 'fast',
  })

const advanceFact = (run, previous, next, count, reason = 'provider_error') =>
  fact('FallbackCursorAdvanced', {
    SessionId: OWNER_S,
    LogicalRunId: RUN_L,
    AuthorityRootUserMessageId: ROOT,
    ProviderRun: providerRun(run),
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: reason,
  })

const companionLinked = () =>
  fact('CompanionBloggerLinked', {
    SessionId: OWNER_S,
    BloggerSessionId: BLOGGER_S,
    BloggerAgent: 'fast-blogger',
  })

const companionClosed = () =>
  fact('CompanionBloggerClosed', {
    SessionId: OWNER_S,
    BloggerSessionId: BLOGGER_S,
  })

const env = (seq, f, sid = OWNER_S) => envelope({ seq, stream: stream.session(sid), fact: f })

// ── Theorem 1: owner failure × blogger interrupt residue — at most once ─────
//
// Correct Host behaviour: AbortResidue on the blogger cycle emits Companion
// link/close (and may inject repair) but NEVER FallbackCursorAdvanced on the
// owner stream. Enumerate all interleavings of the owner's confirmed failure
// with those residue facts — owner.failures must stay 1.

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_blogger_interrupt_interleavings_at_most_once', () => {
  const ownerSeq = [
    env(10, rootFact()),
    env(11, advanceFact('run_owner', 0, 1, 1, 'owner_provider_failure')),
  ]
  // Interrupt residue as durable companion facts only — no owner cursor advance.
  const bloggerResidueSeq = [env(20, companionLinked()), env(21, companionClosed())]

  const reference = fold.apply(fold.empty, [...ownerSeq, ...bloggerResidueSeq])
  assert.equal(reference.ok, true, reference.ok ? '' : JSON.stringify(reference.error))
  assert.equal(fallbackOf(reference.value, OWNER).failures, 1)
  assert.equal(fallbackOf(reference.value, OWNER).dedupeKeys, 1)

  const interleavings = DeterministicEventQueue.interleavings(ownerSeq, bloggerResidueSeq)
  assert.ok(interleavings.length > 2, 'must enumerate real interleavings')

  for (const interleaving of interleavings) {
    const folded = fold.apply(fold.empty, interleaving)
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const cursorState = fallbackOf(folded.value, OWNER)
    assert.deepEqual(
      { offset: cursorState.offset, failures: cursorState.failures, dedupeKeys: cursorState.dedupeKeys },
      { offset: 1, failures: 1, dedupeKeys: 1 },
      'AbortResidue must not double-charge the owner AABB cursor',
    )
  }
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_alone_still_exactly_once_under_duplicate_observation', () => {
  // Same ProviderRun observed twice (idle reconcile + retry signal) — FALLBACK-003.
  const seq = [
    env(1, rootFact()),
    env(2, advanceFact('run_owner', 0, 1, 1)),
    env(3, advanceFact('run_owner', 1, 2, 2)), // duplicate identity → absorbed
  ]
  const folded = fold.apply(fold.empty, seq)
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(fallbackOf(folded.value, OWNER).failures, 1)
})

// ── Theorem 2: counterfactual — why FALLBACK-013 must suppress blogger advance

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_counterfactual_blogger_advance_on_owner_would_double_count', () => {
  // Historical bug shape: Host wrote a second FallbackCursorAdvanced on the
  // OWNER stream using the blogger interrupt's distinct ProviderRun. FALLBACK-003
  // dedupe keys on ProviderRunIdentity, so the second advance lands → failures=2.
  const mistaken = [
    env(1, rootFact()),
    env(2, advanceFact('run_owner', 0, 1, 1, 'owner_provider_failure')),
    env(3, advanceFact('run_blog_interrupt', 1, 2, 2, 'blog_abort_residue')),
  ]
  const folded = fold.apply(fold.empty, mistaken)
  assert.equal(folded.ok, true)
  assert.equal(
    fallbackOf(folded.value, OWNER).failures,
    2,
    'distinct ProviderRuns on one cursor are two units of budget — Host must not emit the blogger one',
  )

  const start = fallbackProjection.forAuthority(RUN_L, ROOT)
  const attemptOwner = cursor.attemptIdentity(OWNER_S, RUN_L, ROOT, providerRun('run_owner'))
  const attemptBlog = cursor.attemptIdentity(OWNER_S, RUN_L, ROOT, providerRun('run_blog_interrupt'))
  const first = fallbackProjection.applyAdvance(attemptOwner, 0, 1, 1, start)
  assert.equal(first.ok, true)
  const second = fallbackProjection.applyAdvance(attemptBlog, 1, 2, 2, first.value)
  assert.equal(second.ok, true, 'different ProviderRun is a new failure unit')
  assert.equal(fallbackProjection.read(second.value).failures, 2)
})

// ── Theorem 3: join-guard durable exactly-once (HandleProjection + Fold) ────
//
// mgr-join-guard / EXEC-016: while background handles remain, join must collect
// them. The durable half reachable without OpenCode: complete and retire each
// handle at most once — duplicates absorb; reverse double-apply refuses.

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_join_guard_handle_complete_retire_exactly_once_projection', () => {
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

  assert.equal(handleProjection.listable(projection).length, 1, 'outstanding until join')

  const once = handleProjection.complete(HANDLE, handleProjection.completionOf('Terminal'), projection)
  assert.equal(once.ok, true)
  const twice = handleProjection.complete(HANDLE, handleProjection.completionOf('Terminal'), once.value)
  assert.deepEqual(twice, { ok: false, error: 'AlreadyCompleted' })

  const retired = handleProjection.retire(HANDLE, once.value)
  assert.equal(retired.ok, true)
  assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, retired.value)).lifecycle, 'Retired')
  const retiredAgain = handleProjection.retire(HANDLE, retired.value)
  assert.deepEqual(retiredAgain, { ok: false, error: 'HandleIsRetired' })

  assert.equal(handleProjection.listable(retired.value).length, 0)
  assert.equal(handleProjection.joinable(retired.value).length, 0)
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_join_guard_fold_absorbs_duplicate_complete_and_retire', () => {
  const linked = fact('HandleLinked', {
    ParentSessionId: OWNER_S,
    ChildSessionId: CHILD_S,
    Handle: HANDLE,
    TargetAgent: 'fast-coder',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  })
  const completed = fact('HandleCompleted', {
    ParentSessionId: OWNER_S,
    Handle: HANDLE,
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  })
  const retired = fact('HandleRetired', { ParentSessionId: OWNER_S, Handle: HANDLE })

  // Journal shapes that survive restart: duplicates of complete/retire after link.
  const trails = [
    [linked, completed, retired],
    [linked, completed, completed, retired],
    [linked, completed, retired, retired],
    [linked, completed, completed, retired, retired],
  ]

  for (const trail of trails) {
    const folded = fold.apply(
      fold.empty,
      trail.map((f, i) => env(i + 1, f)),
    )
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const handles = fold.session(folded.value, OWNER).Handles
    assert.equal(handleProjection.read(handleProjection.tryFind(HANDLE, handles)).lifecycle, 'Retired')
    assert.equal(handleProjection.listable(handles).length, 0)
  }

  // Interleave an extra completed with the retire step after the first complete.
  const afterComplete = [env(1, linked), env(2, completed)]
  for (const interleaving of DeterministicEventQueue.interleavings([env(3, completed)], [env(4, retired)])) {
    // Both orders: complete-dup then retire, or retire then complete-dup.
    // Production fold absorbs both (PERSIST-010); lifecycle ends Retired.
    const folded = fold.apply(fold.empty, [...afterComplete, ...interleaving])
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const lifecycle = handleProjection.read(
      handleProjection.tryFind(HANDLE, fold.session(folded.value, OWNER).Handles),
    ).lifecycle
    assert.equal(lifecycle, 'Retired')
  }
})

// ── Theorem 4: ManagerLifecycle activation / completion exactly-once ────────
//
// finalOracle hard count LifeCompleted=1 (stroke 13). Production fold:
// WorkActivated is identity-idempotent; one LifeCompleted archives exactly one
// Life. (Duplicate FinalityRejected after Resolution=Rejected is fail-closed,
// not soft-absorb — journal writers must not emit that shape.)

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_manager_lifecycle_activation_and_life_completed_exactly_once', () => {
  const LIFE = managerLifeId('life-1')
  const REQ = finalityRequestId('req-1')
  const REVIEWER = sessionId('ses_rev')
  const BARRIER = reviewBarrierId('bar-1')
  const TREE = gitTreeHash('tree-1')
  const BLOB = blobRef('blob-1')
  const DIGEST = blobDigest('d-1')

  const opened = managerLifecycleFact('LifeOpened', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    OpeningUserMessageId: physicalUser('msg-open'),
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 1n,
  })
  const activated = managerLifecycleFact('WorkActivated', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    ActivationPromptKey: promptKey('act-1'),
    ProtectedPrefixEndSequence: 42n,
  })
  const requested = managerLifecycleFact('FinalityRequested', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    RequestId: REQ,
    GitTreeHash: TREE,
    LastWordsRef: BLOB,
    LastWordsDigest: DIGEST,
    ProviderRun: providerRun('run_finality'),
    ToolCallId: toolCallId('call_1'),
  })
  const enlisted = managerLifecycleFact('FinalityReviewerEnlisted', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    RequestId: REQ,
    ReviewerSessionId: REVIEWER,
    ReviewerOrdinal: 1,
    BarrierId: BARRIER,
    GitTreeHash: TREE,
    IsNewReviewer: true,
  })
  const blessed = managerLifecycleFact('FinalityBlessed', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    RequestId: REQ,
    GitTreeHash: TREE,
    WorkRecordBundleRef: BLOB,
    WorkRecordBundleDigest: DIGEST,
  })
  const completed = managerLifecycleFact('LifeCompleted', {
    SessionId: OWNER_S,
    LifeId: LIFE,
    RequestId: REQ,
    TerminalRef: BLOB,
    TerminalDigest: DIGEST,
  })

  const activatedTwice = fold.apply(fold.empty, [env(1, opened), env(2, activated), env(3, activated)])
  assert.equal(activatedTwice.ok, true, activatedTwice.ok ? '' : JSON.stringify(activatedTwice.error))
  assert.equal(
    Number(fold.session(activatedTwice.value, OWNER).ManagerLife.CurrentLife.ProtectedPrefixEnd.Sequence),
    42,
  )

  const ending = [opened, activated, requested, enlisted, blessed, completed].map((f, i) => env(i + 1, f))
  const finished = fold.apply(fold.empty, ending)
  assert.equal(finished.ok, true, finished.ok ? '' : JSON.stringify(finished.error))
  const archived = fold.session(finished.value, OWNER).ManagerLife
  assert.equal(archived.CurrentLife, undefined)
  assert.equal(listItems(archived.CompletedLives).length, 1)
  assert.equal(listItems(archived.CompletedLives)[0].Completed, true)
})

// ── Theorem 5: permutations of independent owner-advance vs residue commute ─

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_advance_and_blogger_residue_permutations_confluent', () => {
  // Three atomic steps after root: link, owner-advance, close.
  // All 6 permutations of {link, advance, close} after a fixed root must yield
  // the same owner cursor (failures=1) — race is algebra, not scheduler lottery.
  const root = env(1, rootFact())
  const steps = [
    env(2, companionLinked()),
    env(3, advanceFact('run_owner', 0, 1, 1, 'owner_provider_failure')),
    env(4, companionClosed()),
  ]

  const perms = DeterministicEventQueue.permutations(steps)
  assert.equal(perms.length, 6)

  let reference
  for (const perm of perms) {
    const folded = fold.apply(fold.empty, [root, ...perm])
    assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
    const cursorState = fallbackOf(folded.value, OWNER)
    assert.equal(cursorState.failures, 1)
    assert.equal(cursorState.offset, 1)
    if (!reference) reference = cursorState
    else {
      assert.deepEqual(
        { offset: cursorState.offset, failures: cursorState.failures, dedupeKeys: cursorState.dedupeKeys },
        { offset: reference.offset, failures: reference.failures, dedupeKeys: reference.dedupeKeys },
      )
    }
  }
})

// tests/unit/Fallback/cursor.test.mjs — FALLBACK-001/002/004/005/006/007/010/011/012.
//
// Two quantities that were once one field, and everything that follows from
// separating them:
//
//   Offset                   unbounded A/A/B/B cycle, never dies
//   ConsecutiveFailureCount  bounded automatic-recovery budget, does die
//
// VERIFY-006 lists both collapse directions as No-Go: "the 4th failure kills the
// run" and "retry forever". So the tests below are mostly about which of the two
// a given operation is allowed to touch.
//
// FALLBACK-011 and FALLBACK-012 (slot arming, maintenance sub-requests) are
// covered by `Context/recovery-slot.test.mjs` — they are decisions about a slot,
// not about the cursor. What is here is the cursor and its durable projection.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFactCaseNames,
  authorityRoot,
  cursor,
  envelope,
  fact,
  fallbackAttemptIdentity,
  fallbackProjection,
  fold,
  idValue,
  logicalRunId,
  providerRun,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'

const SESSION = sessionId('ses_a')
const RUN = logicalRunId('run_L')
const ROOT = authorityRoot('msg_u1')
const PAIR = { SelectedAgent: 'fast-coder', PeerAgent: 'deep-coder' }

const identityFor = (run, { logical = RUN, root = ROOT } = {}) =>
  cursor.attemptIdentity(SESSION, logical, root, providerRun(run))

// ── PERSIST-001 fixtures for the fold-level tests ────────────────────────────

const rootFact = ({ kind = 'HumanRoot', logical = RUN, root = ROOT } = {}) =>
  fact('AuthorityRootAccepted', {
    SessionId: SESSION,
    LogicalRunId: logical,
    AuthorityRootUserMessageId: root,
    AuthorityKind: kind,
    SelectedAgent: 'fast-coder',
    PeerAgent: 'deep-coder',
    CanonicalRole: 'coder',
    SelectedTier: 'fast',
  })

const advanceFact = ({ run, previous, next, count, logical = RUN, root = ROOT, reason = 'provider_error' }) =>
  fact('FallbackCursorAdvanced', {
    SessionId: SESSION,
    LogicalRunId: logical,
    AuthorityRootUserMessageId: root,
    ProviderRun: providerRun(run),
    PreviousOffset: previous,
    NextOffset: next,
    ConsecutiveFailureCount: count,
    Reason: reason,
  })

const exhaustedFact = ({ count, offset }) =>
  fact('FallbackExhausted', {
    SessionId: SESSION,
    LogicalRunId: RUN,
    AuthorityRootUserMessageId: ROOT,
    FinalConsecutiveFailureCount: count,
    FinalOffset: offset,
  })

/** Fold a sequence of facts, numbering LocalSeq from 1. */
const foldFacts = (facts) =>
  fold.apply(
    fold.empty,
    facts.map((value, index) => envelope({ seq: index + 1, stream: stream.session(SESSION), fact: value })),
  )

const fallbackOf = (projection) => fallbackProjection.read(fold.session(projection, 'ses_a').Fallback)

// ── FALLBACK-002: the two quantities, and what each operation moves ──────────

test('FALLBACK_002_a_fresh_cursor_starts_at_offset_zero_with_no_budget_spent', () => {
  assert.deepEqual(cursor.read(cursor.initial), { offset: 0, failures: 0 })

  // FALLBACK-001 expresses "a new root starts fresh" as a constructor, not a
  // `reset` — there is no operation that rewinds an existing cursor.
  assert.deepEqual(fallbackProjection.read(fallbackProjection.forAuthority(RUN, ROOT)), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    offset: 0,
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})

test('FALLBACK_002_offset_is_modulo_four_and_never_stops_advancing', () => {
  assert.deepEqual([0, 1, 2, 3].map(cursor.advance), [1, 2, 3, 0])

  // Twelve failures walk the cycle three times. The cycle itself has no terminal
  // state — only the budget does.
  let value = cursor.initial
  const walked = []
  for (let i = 0; i < 12; i += 1) {
    value = cursor.recordFailure(value)
    walked.push(cursor.read(value).offset)
  }

  assert.deepEqual(walked, [1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0])
  assert.equal(value.ConsecutiveFailureCount, 12)
})

test('FALLBACK_002_each_offset_maps_to_a_fixed_side_and_a_fixed_agent', () => {
  assert.deepEqual([0, 1, 2, 3].map(cursor.side), ['SideA', 'SideA', 'SideB', 'SideB'])

  // A/A/B/B: the agent is a function of the offset alone, which is what makes
  // "which model is this attempt on" answerable without any history.
  assert.deepEqual(
    [0, 1, 2, 3].map((offset) => cursor.effectiveAgent(PAIR, cursor.atOffset(offset))),
    ['fast-coder', 'fast-coder', 'deep-coder', 'deep-coder'],
  )
})

test('FALLBACK_002_an_offset_outside_zero_to_three_is_not_a_cursor_position', () => {
  // Fail loudly rather than defaulting to SideA. A cursor holding 4 came from a
  // corrupt journal line, and answering "SideA" would send the attempt to a model
  // chosen by an arithmetic accident.
  for (const invalid of [4, 5, 255]) {
    assert.throws(() => cursor.side(invalid), /0\.\.3/)
  }
})

test('FALLBACK_006_the_side_sequence_table_is_unbounded_by_construction', () => {
  // The clause states the table for the first attempts; the property is that it
  // repeats forever rather than stopping at the fourth.
  assert.deepEqual(cursor.sideSequence(0), [])
  assert.deepEqual(cursor.sideSequence(4), ['SideA', 'SideA', 'SideB', 'SideB'])
  assert.deepEqual(cursor.sideSequence(6), ['SideA', 'SideA', 'SideB', 'SideB', 'SideA', 'SideA'])

  const long = cursor.sideSequence(100)
  assert.equal(long.length, 100)
  assert.equal(long[96], 'SideA')
  assert.equal(long[99], 'SideB')

  assert.throws(() => cursor.sideSequence(-1), /non-negative/)
})

// ── FALLBACK-004: success clears the budget and leaves the offset alone ──────

test('FALLBACK_004_failure_advances_the_offset_and_spends_one_unit_of_budget', () => {
  assert.deepEqual(cursor.read(cursor.recordFailure({ Offset: 0, ConsecutiveFailureCount: 0 })), {
    offset: 1,
    failures: 1,
  })

  // Wrapping from the last position spends budget like any other failure: the
  // cycle returning to 0 is not a reset.
  assert.deepEqual(cursor.read(cursor.recordFailure({ Offset: 3, ConsecutiveFailureCount: 7 })), {
    offset: 0,
    failures: 8,
  })
})

test('FALLBACK_004_success_resets_the_budget_but_NOT_the_offset', () => {
  // The clause's own example: fail once, succeed once, and the cursor parks at
  // offset 1. Resetting it would send the next failure back to the side that
  // already failed — VERIFY-006 names this exact regression.
  const afterFailure = cursor.recordFailure(cursor.initial)
  const afterSuccess = cursor.recordSuccess(afterFailure)

  assert.deepEqual(cursor.read(afterSuccess), { offset: 1, failures: 0 })

  // And the next failure continues from there rather than from zero.
  assert.deepEqual(cursor.read(cursor.recordFailure(afterSuccess)), { offset: 2, failures: 1 })
})

test('FALLBACK_004_success_leaves_a_parked_odd_offset_in_place', () => {
  // Consequence worth pinning separately, because FALLBACK-012 depends on it: a
  // parked cursor can sit on an odd offset, which is why arming may not be
  // derived from parity alone.
  for (const offset of [1, 3]) {
    const parked = cursor.recordSuccess({ Offset: offset, ConsecutiveFailureCount: 5 })
    assert.deepEqual(cursor.read(parked), { offset, failures: 0 })
    assert.equal(cursor.isRecoverySlot(cursor.read(parked).offset), true)
  }
})

// ── FALLBACK-005: the budget is finite, and judged after the failure ─────────

test('FALLBACK_005_the_default_automatic_recovery_budget_is_twelve', () => {
  // Readable from a layer 1 test on purpose: a clause constant no test can assert
  // is a clause with no gate. (`[<Literal>]` would be inlined by Fable and
  // exported nowhere.)
  assert.equal(cursor.defaultBudget, 12)
})

test('FALLBACK_005_the_verdict_is_taken_after_the_failure_so_the_twelfth_is_final', () => {
  // The clause's sequence: the 12th failure lands on offset 3, advances to 0, and
  // is immediately final. There is no automatic 13th attempt.
  let value = cursor.initial
  const verdicts = []

  for (let attempt = 1; attempt <= 12; attempt += 1) {
    value = cursor.recordFailure(value)
    verdicts.push(cursor.recoveryVerdict(12, value))
  }

  assert.deepEqual(cursor.read(value), { offset: 0, failures: 12 })
  assert.deepEqual(verdicts.slice(0, 11), Array(11).fill('MayContinue'))
  assert.equal(verdicts[11], 'Exhausted')
})

test('FALLBACK_005_a_configured_budget_is_honoured_and_never_infinite', () => {
  const at = (count) => ({ Offset: 0, ConsecutiveFailureCount: count })

  assert.deepEqual(
    [1, 2, 3].map((budget) => cursor.recoveryVerdict(budget, at(2))),
    ['Exhausted', 'Exhausted', 'MayContinue'],
  )

  // A budget of zero exhausts before any automatic attempt, which is a legal
  // administrative setting: "no automatic recovery at all".
  assert.equal(cursor.recoveryVerdict(0, cursor.initial), 'Exhausted')
})

// ── FALLBACK-007: the fold validates every advance ──────────────────────────

test('FALLBACK_007_a_valid_advance_moves_the_durable_cursor', () => {
  const before = fallbackProjection.forAuthority(RUN, ROOT)
  const applied = fallbackProjection.applyAdvance(identityFor('run_1'), 0, 1, 1, before)

  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.deepEqual(fallbackProjection.read(applied.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    offset: 1,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
})

test('FALLBACK_007_the_next_offset_must_be_the_modulo_four_successor', () => {
  assert.deepEqual(
    [
      [0, 1],
      [1, 2],
      [2, 3],
      [3, 0],
    ].map(([previous, next]) => cursor.isValidAdvance(previous, next, 0, 1)),
    [true, true, true, true],
  )

  // Skipping a position, standing still, or going backwards are all corrupt.
  for (const [previous, next] of [
    [0, 2],
    [0, 0],
    [1, 0],
    [3, 1],
  ]) {
    assert.equal(cursor.isValidAdvance(previous, next, 0, 1), false, `${previous}→${next} must be refused`)
  }
})

test('FALLBACK_007_the_count_must_advance_by_exactly_one_or_restart_at_one_after_success', () => {
  // Continuing streak: exactly +1
  assert.equal(cursor.isValidAdvance(0, 1, 4, 5), true)
  assert.equal(cursor.isValidAdvance(1, 2, 0, 1), true)
  assert.equal(cursor.isValidAdvance(2, 3, 2, 3), true)

  // Intervening success reset the count to 0 in memory; next failure restarts count at 1
  assert.equal(cursor.isValidAdvance(2, 3, 2, 1), true)
  assert.equal(cursor.isValidAdvance(3, 0, 5, 1), true)

  for (const [previousCount, nextCount] of [
    [4, 4],
    [4, 6],
    [4, 3],
    [4, 0],
    [4, 2],
  ]) {
    assert.equal(cursor.isValidAdvance(0, 1, previousCount, nextCount), false, `${previousCount}→${nextCount} refused`)
  }
})

test('FALLBACK_007_each_rejection_names_a_different_cause', () => {
  const base = fallbackProjection.applyAdvance(identityFor('run_1'), 0, 1, 1, fallbackProjection.forAuthority(RUN, ROOT))
  assert.equal(base.ok, true)
  const current = base.value

  // Four distinguishable answers. A single boolean, or returning the unchanged
  // projection on every refusal, would make "duplicate" and "corrupt" the same
  // observation — and only one of them is safe to absorb.
  assert.deepEqual(
    {
      duplicate: fallbackProjection.applyAdvance(identityFor('run_1'), 1, 2, 2, current).error,
      badSuccessor: fallbackProjection.applyAdvance(identityFor('run_2'), 1, 3, 2, current).error,
      badCount: fallbackProjection.applyAdvance(identityFor('run_3'), 1, 2, 4, current).error,
      otherRun: fallbackProjection.applyAdvance(identityFor('run_4', { logical: logicalRunId('run_other') }), 1, 2, 2, current)
        .error,
      afterExhausted: fallbackProjection.applyAdvance(
        identityFor('run_5'),
        1,
        2,
        2,
        fallbackProjection.applyExhausted(current),
      ).error,
    },
    {
      duplicate: 'AlreadyObserved',
      badSuccessor: 'InvalidTransition',
      badCount: 'InvalidTransition',
      otherRun: 'DifferentRun',
      afterExhausted: 'AlreadyExhausted',
    },
  )
})

test('FALLBACK_007_a_stale_previous_offset_is_refused_even_when_the_step_is_valid', () => {
  // `1 → 2` is a legal step, but the cursor is at 0. Accepting it would apply an
  // advance computed against a state the journal never reached.
  const current = fallbackProjection.forAuthority(RUN, ROOT)
  const applied = fallbackProjection.applyAdvance(identityFor('run_1'), 1, 2, 1, current)

  assert.deepEqual(applied, { ok: false, error: 'InvalidTransition' })
})

test('FALLBACK_003_the_same_attempt_observed_twice_advances_the_cursor_once', () => {
  // The concrete case the clause is about: one failure seen by both a retry
  // signal and an idle reconcile. Deduped on ProviderRunIdentity, so the second
  // observation cannot move the cursor.
  let current = fallbackProjection.forAuthority(RUN, ROOT)
  const first = fallbackProjection.applyAdvance(identityFor('run_1'), 0, 1, 1, current)
  assert.equal(first.ok, true)
  current = first.value

  assert.deepEqual(fallbackProjection.applyAdvance(identityFor('run_1'), 1, 2, 2, current), {
    ok: false,
    error: 'AlreadyObserved',
  })

  // A genuinely different attempt still advances.
  const second = fallbackProjection.applyAdvance(identityFor('run_2'), 1, 2, 2, current)
  assert.equal(second.ok, true)
  assert.equal(fallbackProjection.read(second.value).offset, 2)
})

test('FALLBACK_003_the_dedupe_window_is_bounded_so_the_projection_cannot_grow_with_history', () => {
  // PERSIST-008. A failed attempt is re-observed within a few signals or not at
  // all, so an unbounded set would only accumulate.
  let current = fallbackProjection.forAuthority(RUN, ROOT)

  for (let attempt = 1; attempt <= 60; attempt += 1) {
    const applied = fallbackProjection.applyAdvance(
      identityFor(`run_${attempt}`),
      (attempt - 1) % 4,
      attempt % 4,
      attempt,
      current,
    )
    assert.equal(applied.ok, true, applied.ok ? '' : `advance ${attempt}: ${applied.error}`)
    current = applied.value
  }

  const state = fallbackProjection.read(current)
  assert.equal(state.failures, 60)
  assert.equal(state.dedupeKeys, 32, 'the window is capped regardless of how many failures preceded it')
})

test('FALLBACK_004_recording_success_clears_the_dedupe_window_too', () => {
  // A later failure is a new attempt. Keeping stale keys would let it be mistaken
  // for a replay and silently not advance the cursor.
  const advanced = fallbackProjection.applyAdvance(identityFor('run_1'), 0, 1, 1, fallbackProjection.forAuthority(RUN, ROOT))
  assert.equal(advanced.ok, true)

  const afterSuccess = fallbackProjection.recordSuccess(advanced.value)
  assert.deepEqual(fallbackProjection.read(afterSuccess), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    offset: 1,
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })

  // The same ProviderRunIdentity may now advance again — it is a new occasion.
  const again = fallbackProjection.applyAdvance(identityFor('run_1'), 1, 2, 1, afterSuccess)
  assert.equal(again.ok, true)
})

test('ENFORCER_063_success_clears_failures_after_multiple_advances_without_touching_offset', () => {
  // BlogObservationCommitted is BloggerMain business success: zero the budget, park the
  // offset. Multi-failure path proves the clear is not a one-shot edge case.
  let current = fallbackProjection.forAuthority(RUN, ROOT)
  for (let attempt = 1; attempt <= 3; attempt += 1) {
    const applied = fallbackProjection.applyAdvance(
      identityFor(`run_${attempt}`),
      (attempt - 1) % 4,
      attempt % 4,
      attempt,
      current,
    )
    assert.equal(applied.ok, true, applied.ok ? '' : `advance ${attempt}: ${applied.error}`)
    current = applied.value
  }

  const before = fallbackProjection.read(current)
  assert.equal(before.failures, 3)
  assert.equal(before.offset, 3)

  const after = fallbackProjection.recordSuccess(current)
  const state = fallbackProjection.read(after)
  assert.equal(state.failures, 0)
  assert.equal(state.offset, before.offset)
})

test('FALLBACK_005_exhaustion_is_stored_rather_than_re_derived_from_the_count', () => {
  // The fold must be able to refuse a late advance without knowing the configured
  // budget, so the terminal state is durable rather than computed.
  const advanced = fallbackProjection.applyAdvance(identityFor('run_1'), 0, 1, 1, fallbackProjection.forAuthority(RUN, ROOT))
  const exhausted = fallbackProjection.applyExhausted(advanced.value)

  assert.equal(fallbackProjection.read(exhausted).exhausted, true)

  // A count well below any budget, yet still terminal.
  assert.equal(fallbackProjection.read(exhausted).failures, 1)
  assert.equal(fallbackProjection.mayContinue(12, exhausted), false)
  assert.equal(fallbackProjection.mayContinue(9999, exhausted), false)
})

test('FALLBACK_005_may_continue_answers_the_projection_level_question', () => {
  let current = fallbackProjection.forAuthority(RUN, ROOT)
  assert.equal(fallbackProjection.mayContinue(3, current), true)

  for (let attempt = 1; attempt <= 3; attempt += 1) {
    current = fallbackProjection.applyAdvance(identityFor(`run_${attempt}`), (attempt - 1) % 4, attempt % 4, attempt, current)
      .value
  }

  assert.equal(fallbackProjection.read(current).failures, 3)
  assert.equal(fallbackProjection.mayContinue(3, current), false, 'budget reached is not "one more allowed"')
  assert.equal(fallbackProjection.mayContinue(4, current), true)
})

// ── FALLBACK-010: the Host's attempt number is not the domain count ──────────

test('FALLBACK_010_the_domain_count_is_reachable_only_through_a_confirmed_failure', () => {
  // The clause's prohibition is structural here: nothing in the cursor API takes
  // an attempt number. `recordFailure` has one input — the cursor — so a Host
  // `Attempt` value has no way in.
  assert.equal(cursor.recordFailure.length, 1)
  assert.equal(cursor.recordSuccess.length, 1)

  // And an advance is keyed by ProviderRunIdentity, not by an ordinal, so two
  // Host retries of one provider run cannot spend two units of budget.
  let current = fallbackProjection.forAuthority(RUN, ROOT)
  const first = fallbackProjection.applyAdvance(identityFor('run_same'), 0, 1, 1, current)
  assert.equal(first.ok, true)
  assert.deepEqual(fallbackProjection.applyAdvance(identityFor('run_same'), 1, 2, 2, first.value), {
    ok: false,
    error: 'AlreadyObserved',
  })
})

test('FALLBACK_010_the_dedupe_identity_names_the_run_the_root_and_the_attempt', () => {
  // Four components. A missing one would merge distinct attempts: without
  // ProviderRun every failure in a run would be "the same attempt", so the cursor
  // would advance once and then never again.
  const identity = identityFor('run_1')

  assert.deepEqual(
    {
      session: idValue.session(identity.SessionId),
      run: idValue.logicalRun(identity.LogicalRunId),
      root: idValue.authorityRoot(identity.AuthorityRootUserMessageId),
      attempt: idValue.providerRun(identity.ProviderRun),
    },
    { session: 'ses_a', run: 'run_L', root: 'msg_u1', attempt: 'run_1' },
  )

  // The key is a `\u001f`-joined string of exactly those four, in that order.
  assert.equal(
    fallbackAttemptIdentity.dedupeKey(identity),
    ['ses_a', 'run_L', 'msg_u1', 'run_1'].join('\u001f'),
  )

  // Each component must move the key, or two different attempts would dedupe as one.
  const base = fallbackAttemptIdentity.dedupeKey(identity)
  assert.notEqual(fallbackAttemptIdentity.dedupeKey(identityFor('run_2')), base)
  assert.notEqual(
    fallbackAttemptIdentity.dedupeKey(identityFor('run_1', { logical: logicalRunId('run_other') })),
    base,
  )
  assert.notEqual(
    fallbackAttemptIdentity.dedupeKey(identityFor('run_1', { root: authorityRoot('msg_u9') })),
    base,
  )
})

// ── the fold: which refusals are absorbed and which stop the journal ─────────

test('FALLBACK_001_the_authority_root_fact_is_what_creates_the_cursor', () => {
  const folded = foldFacts([rootFact()])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  assert.deepEqual(fallbackOf(folded.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    offset: 0,
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})

test('FALLBACK_001_an_advance_with_no_accepted_root_stops_the_replay', () => {
  // The cursor's absence means the root fact is missing from the journal, so the
  // history is incomplete rather than merely odd. The diagnostic must say that
  // and not blame the offsets, which are perfectly valid on this line.
  const folded = foldFacts([advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 })])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'FallbackCursorAdvanced')
  assert.equal(
    folded.error.Reason,
    'cursor advance has no cursor to advance: FALLBACK-001 requires an accepted Authority Root',
  )
})

test('FALLBACK_007_a_replayed_journal_reaches_the_same_cursor', () => {
  const folded = foldFacts([
    rootFact(),
    advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 }),
    advanceFact({ run: 'run_2', previous: 1, next: 2, count: 2 }),
    advanceFact({ run: 'run_3', previous: 2, next: 3, count: 3 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = fallbackOf(folded.value)
  assert.deepEqual({ offset: state.offset, failures: state.failures }, { offset: 3, failures: 3 })
})

test('FALLBACK_007_a_replayed_journal_with_intervening_success_streak_restart_reaches_the_same_cursor', () => {
  // Intervening success reset failure streak to 0; subsequent failure advances offset from 2 to 3 with count 1
  const folded = foldFacts([
    rootFact(),
    advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 }),
    advanceFact({ run: 'run_2', previous: 1, next: 2, count: 2 }),
    advanceFact({ run: 'run_3', previous: 2, next: 3, count: 1 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = fallbackOf(folded.value)
  assert.deepEqual({ offset: state.offset, failures: state.failures }, { offset: 3, failures: 1 })
})

test('FALLBACK_003_a_duplicate_line_is_absorbed_because_replay_produces_it', () => {
  // Expected on replay, so the fold continues with the projection unchanged. This
  // is the one refusal class that must NOT stop startup.
  const folded = foldFacts([
    rootFact(),
    advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 }),
    advanceFact({ run: 'run_1', previous: 1, next: 2, count: 2 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = fallbackOf(folded.value)
  assert.deepEqual({ offset: state.offset, failures: state.failures }, { offset: 1, failures: 1 })
})

test('FALLBACK_007_a_corrupt_transition_stops_the_replay_instead_of_being_absorbed', () => {
  // A correct writer cannot produce this line, so absorbing it would replay the
  // journal into a state the domain forbids.
  const folded = foldFacts([rootFact(), advanceFact({ run: 'run_1', previous: 0, next: 2, count: 1 })])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'FallbackCursorAdvanced')
  assert.equal(folded.error.Reason, 'cursor advance violates FALLBACK-007 (offset or count is not the successor)')

  // The two fatal causes must read differently: one sends an operator to look for
  // a missing Authority Root, the other to inspect the offsets on the line.
  const missingRoot = foldFacts([advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 })])
  assert.notEqual(missingRoot.error.Reason, folded.error.Reason)
})

test('FALLBACK_005_an_advance_after_exhaustion_is_absorbed_not_applied', () => {
  const folded = foldFacts([
    rootFact(),
    advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 }),
    exhaustedFact({ count: 1, offset: 1 }),
    advanceFact({ run: 'run_2', previous: 1, next: 2, count: 2 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = fallbackOf(folded.value)
  assert.deepEqual(
    { offset: state.offset, failures: state.failures, exhausted: state.exhausted },
    { offset: 1, failures: 1, exhausted: true },
    'the late advance changed nothing',
  )
})

test('FALLBACK_001_a_new_authority_root_replaces_the_cursor_entirely', () => {
  const folded = foldFacts([
    rootFact(),
    advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 }),
    advanceFact({ run: 'run_2', previous: 1, next: 2, count: 2 }),
    rootFact({ logical: logicalRunId('run_M'), root: authorityRoot('msg_u2') }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.deepEqual(fallbackOf(folded.value), {
    logicalRun: 'run_M',
    authorityRoot: 'msg_u2',
    offset: 0,
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})

test('FALLBACK_007_an_advance_naming_another_run_is_absorbed_not_applied', () => {
  // Absorbed rather than fatal: a line for a superseded run is what a journal
  // written across an Authority Root change looks like.
  const folded = foldFacts([
    rootFact(),
    rootFact({ logical: logicalRunId('run_M'), root: authorityRoot('msg_u2') }),
    advanceFact({ run: 'run_1', previous: 0, next: 1, count: 1 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = fallbackOf(folded.value)
  assert.deepEqual({ run: state.logicalRun, offset: state.offset, failures: state.failures }, {
    run: 'run_M',
    offset: 0,
    failures: 0,
  })
})

test('FALLBACK_007_success_writes_no_fact_so_no_journal_line_zeroes_the_count', () => {
  // The clause is explicit: `ConsecutiveFailureCount = 0` is derived from a
  // successful provider attempt proven by the Host snapshot (HOST-004), not
  // persisted. A success fact would be a second writer for the cursor, which
  // FALLBACK-003 forbids.
  //
  // Asserted as absence over the whole fact union, because a new fact is exactly
  // how this guarantee would be lost — and it would look like a feature.
  const names = agentFactCaseNames()

  assert.deepEqual(
    names.filter((name) => name.startsWith('Fallback')).sort(),
    ['FallbackCursorAdvanced', 'FallbackExhausted'],
    'the cursor has exactly two durable facts: one advance, one terminal',
  )
})

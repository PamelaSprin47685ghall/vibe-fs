// tests/unit/Review/witness.test.mjs — REVIEW-002/003/004/005/006/007/008/010.
//
// One question: what proves that a second PERFECT actually saw the first
// challenge. Everything else in docs/what/review.md exists to stop that question being
// answered by a cheaper substitute — a shared authority root, a matching
// physical message id, or a stored boolean.
//
// So the tests come in pairs: the positive path builds the causal proof, and the
// negative path removes exactly one component of it and shows the confirmation
// disappears. A test that only exercised the happy path would pass against an
// implementation that confirms unconditionally.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'
import * as provider from '../../../dist/Participant/Provider/LanguageSurface.js'

const reviewChallenge = {
  path: review.challengePath,
  text: review.challengeText('English'),
  textVersion: review.challengeTextVersion,
  prompt: review.challengePrompt(review.challengeText('English')),
  promptOf: review.challengePrompt,
  contentDigest: (sha256, prompt) => review.challengeDigest(sha256, prompt),
  issued: ({ barrier, tree, reviewer, run, call, digest }) => review.issuedChallenge({
    BarrierId: barrier, GitTreeHash: tree, ReviewerSessionId: reviewer,
    FirstProviderRun: run, FirstToolCallId: call, ChallengeTextVersion: 1,
    ChallengeContentDigest: digest,
  }),
}
const reviewWitness = {
  attemptIdentity: review.attemptIdentity,
  isDistinctAttempt: review.isDistinctAttempt,
  isConfirmed: review.isConfirmed,
  isPerfectPending: review.isPerfectPending,
  isRevision: review.isRevision,
  isValidForTree: review.isValidForTree,
  confirmedReviewer: review.confirmedReviewer,
  gitTreeHash: review.gitTreeHash,
  read: (value) => review.readWitness(value?.value?.Witness ?? value?.value ?? value),
  noReview: review.noReview,
  confirm: (barrier, digest, input, first, second) => {
    const value = review.confirmWitness(barrier, digest, input, first, second)
    return value == null ? undefined : { ok: true, value: { Witness: value } }
  },
}
const guardWrap = (handle) => ({ handle, Witness: review.guardWitness(handle) })
const guardUnwrap = (value) => value.handle ?? value
const guardResult = (result) => result.ok ? { ok: true, value: guardWrap(result.value) } : result
const reviewProjection = {
  empty: guardWrap(review.emptyGuard()),
  startBarrier: (barrier, tree, current) => guardWrap(review.startBarrier('ses_manager', barrier, tree, guardUnwrap(current))),
  applyChallengeIssued: (challenge, current) => guardWrap(review.applyChallengeIssued(challenge, guardUnwrap(current))),
  applySeal: (seal, current) => guardWrap(review.applySeal(seal, guardUnwrap(current))),
  applyVerdict: (attempt, verdict, current) => guardResult(review.applyVerdict(attempt, verdict, guardUnwrap(current))),
  applyConfirmedWitness: (barrier, digest, input, first, second, current) =>
    guardResult(review.applyConfirmedWitness(barrier, digest, input, first, second, guardUnwrap(current))),
  read: (current) => {
    const value = review.guardView(guardUnwrap(current))
    return {
      barrier: value.barrier ?? undefined,
      tree: value.tree ?? undefined,
      witness: value.witness.state,
      hasPendingChallenge: value.hasPendingChallenge,
      seals: value.sealCount,
      observedAttempts: value.observedAttempts,
    }
  },
  satisfiesGuard: (tree, current) => review.satisfiesGuard(tree, guardUnwrap(current)),
  hasObservedAttempt: (attempt, current) => review.hasObservedAttempt(attempt, guardUnwrap(current)),
}
const requirementWrap = (handle) => {
  const value = review.requirementsView(handle)
  return { handle, HumanPromptInputs: value.humanPromptInputs, LastConfirmedProviderRun: value.lastConfirmedProviderRun }
}
const requirementUnwrap = (value) => value.handle ?? value
const reviewRequirements = {
  empty: requirementWrap(review.requirementsEmpty()),
  addRequirement: (session, root, current) => requirementWrap(review.addRequirement(session, root, requirementUnwrap(current))),
  clearOnConfirmation: (run, current) => requirementWrap(review.clearRequirements(run, requirementUnwrap(current))),
  read: (current) => review.requirementsView(requirementUnwrap(current)),
}
const providerLanguage = { english: 'English', simplifiedChinese: 'SimplifiedChinese' }
const providerResources = { readText: (language, path) => provider.readText(language, path) }
const reviewAttemptIdentity = { dedupeKey: review.dedupeKey }
const providerInputSeal = (value) => review.providerInputSeal(value)
const verdictWitness = (value) => review.verdictWitness({ ProviderRun: value.run, ToolCallId: value.call, GitTreeHash: value.tree, ReviewerSessionId: value.reviewer })
const verdict = { perfect: 'PERFECT', revise: 'REVISE' }
const reviewBarrierId = (value) => value
const gitTreeHash = (value) => value
const providerRun = (value) => value
const sealDigest = (value) => value
const sessionId = (value) => value
const authorityRoot = (value) => value
const idValue = {
  sealDigest: (value) => value,
  session: (value) => value,
  providerRun: (value) => value,
  gitTree: (value) => value,
}
const isSome = (value) => value != null


// A visible stand-in for sha256: the property under test is which text is
// digested, not the hash function.
const H = (input) => `H(${input})`

const BARRIER = reviewBarrierId('bar_1')
const TREE = gitTreeHash('tree_1')
const OTHER_TREE = gitTreeHash('tree_2')
const REVIEWER = 'ses_rev'

const CHALLENGE_DIGEST = reviewChallenge.contentDigest(H)
const CHALLENGE_DIGEST_TEXT = idValue.sealDigest(CHALLENGE_DIGEST)

const witnessAt = ({ run, call, tree = 'tree_1', reviewer = REVIEWER }) =>
  reviewWitness.attemptIdentity(BARRIER, verdictWitness({ run, call, tree, reviewer }))

const first = verdictWitness({ run: 'run_1', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER })
const second = verdictWitness({ run: 'run_2', call: 'call_2', tree: 'tree_1', reviewer: REVIEWER })

const issuedChallenge = ({ run = 'run_1', call = 'call_1', tree = 'tree_1', digest = CHALLENGE_DIGEST } = {}) =>
  reviewChallenge.issued({ barrier: 'bar_1', tree, reviewer: REVIEWER, run, call, digest })

/** A guard that has issued its first challenge and sealed the second run. */
const afterChallengeAndSeal = ({ includedDigests = [CHALLENGE_DIGEST_TEXT], secondRun = 'run_2' } = {}) => {
  let guard = reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty)
  guard = reviewProjection.applyChallengeIssued(issuedChallenge(), guard)
  return reviewProjection.applySeal(
    providerInputSeal({ session: REVIEWER, run: secondRun, digest: 'seal_2', included: includedDigests }),
    guard,
  )
}

const confirmOn = (guard, { challengeDigest = CHALLENGE_DIGEST, secondInputDigest = sealDigest('seal_2') } = {}) =>
  reviewProjection.applyConfirmedWitness(BARRIER, challengeDigest, secondInputDigest, first, second, guard)

// ── REVIEW-003: the fixed challenge is one fact viewed three ways ─────────────

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_003_the_challenge_text_and_its_version_are_pinned', () => {
  // English canonical bytes are the historical EN seal. A new locale is not a
  // new TextVersion; bump only when the English sentence itself changes.
  assert.equal(reviewChallenge.path, 'review/challenge')
  assert.equal(
    reviewChallenge.text,
    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?",
  )
  assert.equal(reviewChallenge.textVersion, 1)
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_003_the_challenge_digest_is_the_digest_of_that_exact_text', () => {
  // Derived through the tool-result digest, not hashed locally. The challenge IS
  // a tool result, so sealing it necessarily produces this value; a second hash
  // spelled elsewhere would agree only by coincidence, and any drift would refuse
  // every confirmation while looking like correct fail-closed behaviour.
  //
  // `promptOf` renders the sentence as an ARCH-010 instruction comment; the
  // digest is of those exact bytes (`# ...\n`).
  assert.equal(CHALLENGE_DIGEST_TEXT, `H(# ${reviewChallenge.text}\n)`)

  // Deterministic: the same text digests the same way on any process.
  assert.equal(idValue.sealDigest(reviewChallenge.contentDigest(H)), CHALLENGE_DIGEST_TEXT)
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_003_challenge_follows_session_language', () => {
  const en = providerResources.readText(providerLanguage.english, reviewChallenge.path)
  const zh = providerResources.readText(
    providerLanguage.simplifiedChinese,
    reviewChallenge.path,
  )
  assert.equal(en, reviewChallenge.text)
  assert.equal(zh, '不行，再评估一遍：它是否真的完整满足原任务，而没有偷工减料？')
  assert.equal(reviewChallenge.promptOf(en), reviewChallenge.prompt)
  assert.equal(reviewChallenge.promptOf(zh), `# ${zh}\n`)
  assert.notEqual(
    idValue.sealDigest(reviewChallenge.contentDigest(H, reviewChallenge.promptOf(zh))),
    CHALLENGE_DIGEST_TEXT,
  )
})

// ── REVIEW-004: one provider run counts once ─────────────────────────────────

test('WHAT[REVIEW-ASSURANCE-003] REVIEW_004_the_attempt_identity_names_all_five_components', () => {
  // Five components joined with `\u001f`. Dropping ToolCallId would make parallel
  // PERFECT calls in one assistant message indistinguishable; dropping the tree
  // would let a confirmation for an old tree count for a new one.
  assert.equal(
    reviewAttemptIdentity.dedupeKey(witnessAt({ run: 'run_1', call: 'call_1' })),
    ['bar_1', 'tree_1', REVIEWER, 'run_1', 'call_1'].join('\u001f'),
  )
})

test('WHAT[REVIEW-ASSURANCE-001] REVIEW_003_two_attempts_are_distinct_only_when_run_AND_call_both_differ', () => {
  // Conditions 1-5: same reviewer, same barrier, same tree, DIFFERENT run,
  // DIFFERENT call. Each negative case below removes exactly one.
  const distinct = (a, b) => reviewWitness.isDistinctAttempt(BARRIER, a, b)
  const w = (run, call, tree = 'tree_1', reviewer = REVIEWER) => verdictWitness({ run, call, tree, reviewer })

  assert.equal(distinct(w('run_1', 'call_1'), w('run_2', 'call_2')), true)

  assert.deepEqual(
    {
      sameRun: distinct(w('run_1', 'call_1'), w('run_1', 'call_2')),
      sameCall: distinct(w('run_1', 'call_1'), w('run_2', 'call_1')),
      sameBoth: distinct(w('run_1', 'call_1'), w('run_1', 'call_1')),
      differentTree: distinct(w('run_1', 'call_1'), w('run_2', 'call_2', 'tree_2')),
      differentReviewer: distinct(w('run_1', 'call_1'), w('run_2', 'call_2', 'tree_1', 'ses_other')),
    },
    { sameRun: false, sameCall: false, sameBoth: false, differentTree: false, differentReviewer: false },
  )
})

test('WHAT[REVIEW-ASSURANCE-003] REVIEW_004_a_repeated_attempt_is_refused_as_a_duplicate', () => {
  const guard = reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty)
  const attempt = witnessAt({ run: 'run_1', call: 'call_1' })

  const applied = reviewProjection.applyVerdict(attempt, verdict.perfect, guard)
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.equal(reviewProjection.read(applied.value).observedAttempts, 1)

  // Expected on replay, so it is a named refusal rather than a fold failure.
  assert.deepEqual(reviewProjection.applyVerdict(attempt, verdict.perfect, applied.value), {
    ok: false,
    error: 'DuplicateAttempt',
  })

  // And the writer can ask before appending, rather than learning by rejection.
  assert.equal(reviewProjection.hasObservedAttempt(attempt, applied.value), true)
  assert.equal(reviewProjection.hasObservedAttempt(witnessAt({ run: 'run_9', call: 'call_9' }), applied.value), false)
})

test('WHAT[REVIEW-ASSURANCE-003] REVIEW_004_the_attempt_window_is_bounded', () => {
  // PERSIST-008. The window only has to recognise repeats within the current
  // barrier, so it does not grow with history.
  let guard = reviewProjection.empty

  for (let index = 1; index <= 20; index += 1) {
    const applied = reviewProjection.applyVerdict(
      witnessAt({ run: `run_${index}`, call: `call_${index}` }),
      verdict.perfect,
      guard,
    )
    assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
    guard = applied.value
  }

  assert.equal(reviewProjection.read(guard).observedAttempts, 8)
})

// ── REVIEW-005: confirmation is a witness state, never a stored flag ─────────

test('WHAT[REVIEW-ASSURANCE-004] REVIEW_005_an_empty_guard_is_NoReview_and_satisfies_nothing', () => {
  const guard = reviewProjection.empty

  assert.deepEqual(reviewProjection.read(guard), {
    barrier: undefined,
    tree: undefined,
    witness: 'NoReview',
    hasPendingChallenge: false,
    seals: 0,
    observedAttempts: 0,
  })

  assert.deepEqual(reviewWitness.read(reviewWitness.noReview), { state: 'NoReview' })
  assert.equal(reviewProjection.satisfiesGuard(TREE, guard), false)

  // No tree, so nothing can be valid for one — including the current tree.
  assert.equal(reviewWitness.isValidForTree(TREE, reviewWitness.noReview), false)
  assert.equal(reviewWitness.gitTreeHash(reviewWitness.noReview) != null, false)
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_005_a_first_PERFECT_becomes_a_pending_witness_the_fold_can_produce', () => {
  // The regression this pins: a previous version stored only `PendingChallenge`,
  // so `isPerfectPending` was never true and both of its readers waited for a
  // state the fold could not produce. A first PERFECT looked like no review.
  const guard = reviewProjection.applyChallengeIssued(
    issuedChallenge(),
    reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty),
  )

  assert.deepEqual(reviewProjection.read(guard), {
    barrier: 'bar_1',
    tree: 'tree_1',
    witness: 'PerfectPending',
    hasPendingChallenge: true,
    seals: 0,
    observedAttempts: 0,
  })

  assert.equal(reviewWitness.isPerfectPending(guard.Witness), true)
  assert.equal(reviewWitness.isConfirmed(guard.Witness), false)
  assert.equal(reviewProjection.satisfiesGuard(TREE, guard), false, 'pending is not confirmed')

  // The pending witness is built from the challenge, so the two cannot disagree.
  assert.deepEqual(reviewWitness.read(guard.Witness), {
    state: 'PerfectPending',
    first: { run: 'run_1', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER },
  })
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_005_recording_a_PERFECT_verdict_alone_does_not_make_it_pending', () => {
  // `applyVerdict` counts the attempt (REVIEW-004); the challenge is a separate
  // fact. Confirmation must never be reachable by recording a verdict alone.
  const guard = reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty)
  const applied = reviewProjection.applyVerdict(witnessAt({ run: 'run_1', call: 'call_1' }), verdict.perfect, guard)

  assert.equal(applied.ok, true)
  assert.deepEqual(reviewProjection.read(applied.value), {
    barrier: 'bar_1',
    tree: 'tree_1',
    witness: 'NoReview',
    hasPendingChallenge: false,
    seals: 0,
    observedAttempts: 1,
  })
})

test('WHAT[REVIEW-ASSURANCE-001] REVIEW_002_a_REVISE_clears_an_unfinished_confirmation', () => {
  const pending = reviewProjection.applyChallengeIssued(
    issuedChallenge(),
    reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty),
  )
  assert.equal(reviewProjection.read(pending).hasPendingChallenge, true)

  const revised = reviewProjection.applyVerdict(witnessAt({ run: 'run_9', call: 'call_9' }), verdict.revise, pending)
  assert.equal(revised.ok, true, revised.ok ? '' : revised.error)

  assert.equal(reviewProjection.read(revised.value).witness, 'RevisionWitness')
  assert.equal(reviewProjection.read(revised.value).hasPendingChallenge, false)
  assert.equal(reviewWitness.isRevision(revised.value.Witness), true)
  assert.equal(reviewProjection.satisfiesGuard(TREE, revised.value), false)
})

// ── REVIEW-010: the seal is the causal evidence ──────────────────────────────

test('WHAT[REVIEW-ASSURANCE-007] REVIEW_010_a_seal_records_the_tool_result_digests_the_run_actually_saw', () => {
  const guard = reviewProjection.applySeal(
    providerInputSeal({ session: REVIEWER, run: 'run_2', digest: 'seal_2', included: [CHALLENGE_DIGEST_TEXT] }),
    reviewProjection.empty,
  )

  assert.equal(reviewProjection.read(guard).seals, 1)
})

test('WHAT[REVIEW-ASSURANCE-003] REVIEW_010_the_seal_window_is_bounded', () => {
  // Seals matter only until the verdict that consumes them, so the window is
  // small and keyed by provider run (PERSIST-008).
  let guard = reviewProjection.empty

  for (let index = 1; index <= 20; index += 1) {
    guard = reviewProjection.applySeal(
      providerInputSeal({ session: REVIEWER, run: `run_${index}`, digest: `seal_${index}` }),
      guard,
    )
  }

  assert.equal(reviewProjection.read(guard).seals, 8)
})

// ── REVIEW-003 + REVIEW-006: the confirmed witness and its evidence ─────────

test('WHAT[REVIEW-ASSURANCE-005] REVIEW_006_a_confirmed_witness_answers_every_identity_question_inline', () => {
  const confirmed = confirmOn(afterChallengeAndSeal())
  assert.equal(confirmed.ok, true, confirmed.ok ? '' : confirmed.error)

  // "Who reviewed, for which tree, which two runs, and did the second really see
  // the first challenge" — all readable from the witness with no surrounding map.
  assert.deepEqual(reviewWitness.read(confirmed.value.Witness), {
    state: 'Confirmed',
    barrier: 'bar_1',
    tree: 'tree_1',
    first: { run: 'run_1', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER },
    second: { run: 'run_2', call: 'call_2', tree: 'tree_1', reviewer: REVIEWER },
    challengeResultDigest: CHALLENGE_DIGEST_TEXT,
    secondProviderInputDigest: 'seal_2',
  })

  assert.equal(reviewProjection.read(confirmed.value).hasPendingChallenge, false)
  assert.equal(reviewProjection.satisfiesGuard(TREE, confirmed.value), true)
})

test('WHAT[REVIEW-ASSURANCE-005] REVIEW_006_the_witness_has_no_authority_root_field_at_all', () => {
  // REVIEW-003 forbids confirming on a shared authority root. Carrying the field
  // "for context" is how that comes back: once it exists, comparing it is one
  // line away. So the record must not have one.
  //
  // Asserted against the PRODUCTION record's own keys, not the facade's reading of
  // it — a facade projection that simply omitted the field would make this pass
  // while the record carried it.
  assert.deepEqual(Object.keys(first).sort(), ['GitTreeHash', 'ProviderRun', 'ReviewerSessionId', 'ToolCallId'])

  const confirmed = confirmOn(afterChallengeAndSeal())
  assert.equal(confirmed.ok, true, confirmed.ok ? '' : confirmed.error)
  const payload = review.confirmedWitnessRecord(confirmed.value)

  assert.deepEqual(Object.keys(payload).sort(), [
    'BarrierId',
    'ChallengeResultDigest',
    'First',
    'GitTreeHash',
    'Second',
    'SecondProviderInputDigest',
  ])

  // REVIEW-006's list is these six plus the manager/job identities the fact
  // carries. None of them is a root or a physical message id.
  const everyKey = [...Object.keys(payload), ...Object.keys(payload.First), ...Object.keys(payload.Second)]
  assert.deepEqual(
    everyKey.filter((key) => /authority|root|physical/i.test(key)),
    [],
  )
})

test('WHAT[REVIEW-ASSURANCE-001] REVIEW_003_confirmation_requires_two_distinct_attempts', () => {
  // Conditions 1-5 are enforced at confirmation time, not merely documented. The
  // second witness here reuses the first run, which is the exact shape a reviewer
  // emitting two PERFECT calls in one assistant message produces.
  const guard = afterChallengeAndSeal()
  const sameRun = verdictWitness({ run: 'run_1', call: 'call_9', tree: 'tree_1', reviewer: REVIEWER })

  const refused = reviewProjection.applyConfirmedWitness(
    BARRIER,
    CHALLENGE_DIGEST,
    sealDigest('seal_2'),
    first,
    sameRun,
    guard,
  )

  assert.deepEqual(refused, { ok: false, error: 'NotDistinctAttempt' })
  assert.equal(reviewProjection.read(guard).witness, 'PerfectPending', 'the guard is unchanged')
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_003_the_witness_carries_the_digests_rather_than_a_boolean', () => {
  // `confirm` takes the two digests, not a `proven: bool`. A boolean would leave
  // `SecondProviderInputDigest` to be fetched again by whoever builds the
  // witness — a second lookup that can disagree with the first.
  const confirmed = reviewWitness.confirm(BARRIER, CHALLENGE_DIGEST, sealDigest('seal_2'), first, second)

  assert.equal(confirmed.value.Witness.state, 'Confirmed')
  assert.deepEqual(
    {
      challenge: reviewWitness.read(confirmed).challengeResultDigest,
      secondInput: reviewWitness.read(confirmed).secondProviderInputDigest,
    },
    { challenge: CHALLENGE_DIGEST_TEXT, secondInput: 'seal_2' },
  )

  // And it refuses a non-distinct pair rather than fabricating evidence.
  assert.equal(reviewWitness.confirm(BARRIER, CHALLENGE_DIGEST, sealDigest('seal_2'), first, first), undefined)
})

test('WHAT[REVIEW-ASSURANCE-004] REVIEW_005_confirmedReviewer_is_derived_from_the_witness_not_stored_beside_it', () => {
  const confirmed = confirmOn(afterChallengeAndSeal())

  assert.equal(idValue.session(reviewWitness.confirmedReviewer(confirmed.value.Witness)), REVIEWER)

  // Every non-confirmed state answers "nobody". A stored reviewer id could name
  // one while the witness said NoReview — the same mistake as a stored boolean,
  // one step removed.
  const pending = reviewProjection.applyChallengeIssued(
    issuedChallenge(),
    reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty),
  )
  assert.equal(reviewWitness.confirmedReviewer(pending.Witness) != null, false)
  assert.equal(reviewWitness.confirmedReviewer(reviewWitness.noReview) != null, false)
})

test('WHAT[REVIEW-ASSURANCE-013] REVIEW_007_a_started_barrier_is_mirrored_to_the_manager_guard', () => {
  const manager = sessionId('ses_manager')
  const mirrored = review.startBarrier(manager, BARRIER, TREE, review.emptyGuard())
  assert.equal(review.guardView(mirrored).managerSession, manager)
  assert.equal(review.guardView(mirrored).barrier, BARRIER)
  assert.equal(review.guardView(mirrored).tree, TREE)
})

// ── REVIEW-008: a tree change invalidates without deleting ──────────────────

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_a_tree_change_makes_a_confirmed_witness_insufficient', () => {
  const confirmed = confirmOn(afterChallengeAndSeal()).value

  assert.equal(reviewProjection.satisfiesGuard(TREE, confirmed), true)
  assert.equal(reviewProjection.satisfiesGuard(OTHER_TREE, confirmed), false)

  // Not deleted: the witness stays auditable and still reports Confirmed. The
  // clause forbids discarding history; validity is a question asked against the
  // current tree, so it is derived rather than mutated.
  assert.equal(reviewWitness.isConfirmed(confirmed.Witness), true)
  assert.equal(idValue.gitTree(reviewWitness.gitTreeHash(confirmed.Witness)), 'tree_1')
  assert.equal(reviewWitness.isValidForTree(OTHER_TREE, confirmed.Witness), false)
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_a_new_barrier_clears_the_pending_challenge_but_keeps_the_witness', () => {
  const confirmed = confirmOn(afterChallengeAndSeal()).value
  const next = reviewProjection.startBarrier(reviewBarrierId('bar_2'), OTHER_TREE, confirmed)

  assert.deepEqual(reviewProjection.read(next), {
    barrier: 'bar_2',
    tree: 'tree_2',
    witness: 'Confirmed',
    hasPendingChallenge: false,
    seals: 1,
    observedAttempts: 0,
  })

  // The kept witness is for the OLD tree, so the new barrier is not satisfied by
  // it — which is the whole reason keeping it is safe.
  assert.equal(reviewProjection.satisfiesGuard(OTHER_TREE, next), false)
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_a_new_barrier_invalidates_a_witness_even_when_the_tree_hash_is_unchanged', () => {
  const confirmed = confirmOn(afterChallengeAndSeal()).value
  const next = reviewProjection.startBarrier(reviewBarrierId('bar_2'), TREE, confirmed)

  assert.equal(reviewWitness.isConfirmed(next.Witness), true, 'the old witness remains auditable')
  assert.equal(reviewProjection.satisfiesGuard(TREE, next), false, 'the new barrier requires two new PERFECT attempts')
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_a_late_confirmation_cannot_rewind_the_current_barrier', () => {
  const newer = reviewProjection.startBarrier(reviewBarrierId('bar_2'), TREE, reviewProjection.empty)
  const late = confirmOn(newer).value

  assert.equal(reviewProjection.read(late).barrier, 'bar_2')
  assert.equal(reviewWitness.isConfirmed(late.Witness), true, 'the late witness remains auditable')
  assert.equal(reviewProjection.satisfiesGuard(TREE, late), false)
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_re_entering_the_same_barrier_changes_nothing', () => {
  // Idempotent, because the Guard re-checks on every assistant terminal. If this
  // reset the attempt window, a second PERFECT in the same barrier would stop
  // being recognised as a repeat.
  const confirmed = confirmOn(afterChallengeAndSeal()).value
  const again = reviewProjection.startBarrier(BARRIER, TREE, confirmed)

  assert.deepEqual(reviewProjection.read(again), reviewProjection.read(confirmed))
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_every_witness_state_reports_the_tree_it_belongs_to', () => {
  // The Guard's question is always "for the CURRENT tree", so a state without a
  // tree could never be validated — and `NoReview` is exactly that state.
  const pending = reviewProjection.applyChallengeIssued(
    issuedChallenge(),
    reviewProjection.startBarrier(BARRIER, TREE, reviewProjection.empty),
  )
  const revised = reviewProjection.applyVerdict(witnessAt({ run: 'run_9', call: 'call_9' }), verdict.revise, pending)
    .value
  const confirmed = confirmOn(afterChallengeAndSeal()).value

  assert.deepEqual(
    {
      pending: idValue.gitTree(reviewWitness.gitTreeHash(pending.Witness)),
      revised: idValue.gitTree(reviewWitness.gitTreeHash(revised.Witness)),
      confirmed: idValue.gitTree(reviewWitness.gitTreeHash(confirmed.Witness)),
      noReview: reviewWitness.gitTreeHash(reviewWitness.noReview) != null,
    },
    { pending: 'tree_1', revised: 'tree_1', confirmed: 'tree_1', noReview: false },
  )
})

// ── REVIEW-007: the requirement a human prompt creates ──────────────────────

test('WHAT[REVIEW-ASSURANCE-013] REVIEW_007_a_requirement_is_keyed_by_authority_root_and_deduped', () => {
  // Keyed by Authority Root rather than physical message: the requirement is
  // about the task a human asked for, and PROMPT-002 makes the root that task's
  // identity. Keying by the wire message would also force converting one identity
  // into the other, which PROMPT-001 exists to prevent.
  let requirements = reviewRequirements.empty
  assert.equal(requirements.HumanPromptInputs.length, 0)

  requirements = reviewRequirements.addRequirement(sessionId('ses_m'), authorityRoot('msg_1'), requirements)
  requirements = reviewRequirements.addRequirement(sessionId('ses_m'), authorityRoot('msg_1'), requirements)
  assert.equal(requirements.HumanPromptInputs.length, 1, 'the same root twice is one requirement')

  requirements = reviewRequirements.addRequirement(sessionId('ses_m'), authorityRoot('msg_2'), requirements)
  assert.equal(requirements.HumanPromptInputs.length, 2)
})

test('WHAT[REVIEW-ASSURANCE-013] REVIEW_007_a_confirmed_review_clears_the_requirements_it_covered', () => {
  let requirements = reviewRequirements.addRequirement(
    sessionId('ses_m'),
    authorityRoot('msg_1'),
    reviewRequirements.empty,
  )
  requirements = reviewRequirements.addRequirement(sessionId('ses_m'), authorityRoot('msg_2'), requirements)

  const cleared = reviewRequirements.clearOnConfirmation(providerRun('run_conf'), requirements)

  assert.equal(cleared.HumanPromptInputs.length, 0)
  assert.equal(idValue.providerRun(cleared.LastConfirmedProviderRun), 'run_conf')

  // Idempotent for the same run, so a replayed confirmation cannot clear
  // requirements that arrived after it.
  const laterRequirement = reviewRequirements.addRequirement(sessionId('ses_m'), authorityRoot('msg_3'), cleared)
  const replayed = reviewRequirements.clearOnConfirmation(providerRun('run_conf'), laterRequirement)
  assert.equal(replayed.HumanPromptInputs.length, 1, 'a replay must not clear a newer requirement')

  // A genuinely different confirmation does clear it.
  const nextConfirmation = reviewRequirements.clearOnConfirmation(providerRun('run_other'), laterRequirement)
  assert.equal(nextConfirmation.HumanPromptInputs.length, 0)
})

// tests/unit/Context/recovery-slot.test.mjs — FALLBACK-011/012, CTX-006/007/008.
//
// The recovery slot's control flow. One rule here is worth more than the rest:
//
//   Arming is NOT a property of the cursor's position.
//
// Arming is still not a property of cursor parity: a crash can preserve a durable
// primed Offset while losing the process-local recovery opportunity. Deriving
// "armed" from `isOdd offset` would therefore invent authority after restart.
//
// The production module offers no way to ask "is offset N armed", and neither does
// the facade. These tests pin that absence as much as the behaviour.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as slot from '../../../dist/Context/Companion/CompressionSurface.js'
const requestKind = slot.requestKind
const cursor = slot.cursor

// ── arming is a control-flow fact, not a position ───────────────────────────

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_a_new_sequence_always_starts_unarmed', () => {
  // Even when the recovered Offset is odd. That is the whole clause.
  assert.equal(slot.armingName(slot.beginSequence), 'NotArmed')
  assert.equal(slot.isArmed(slot.beginSequence), false)
})

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_only_a_failure_advance_arms_the_next_slot', () => {
  assert.equal(slot.armingName(slot.afterFailureAdvance), 'ArmedByAdvance')
  assert.equal(slot.isArmed(slot.afterFailureAdvance), true)
})

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_arming_is_lost_across_a_restart_and_the_safe_side_is_unarmed', () => {
  // Resuming armed would invent a recovery opportunity after restart. Unarmed
  // costs at most one missed compression opportunity.
  assert.equal(slot.armingName(slot.afterRestart), 'NotArmed')
  assert.equal(slot.afterRestart, slot.beginSequence, 'a restart is indistinguishable from a fresh sequence')
})

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_the_facade_offers_no_way_to_derive_arming_from_an_offset', () => {
  // Mirrors the production module. There is no `armingOf(offset)`: arming is a
  // control-flow fact, and a function mapping a position to it would let a test
  // assert parity-derived recovery authority as correct behaviour.
  //
  // `mayRecover` does read the offset, but only as one conjunct alongside arming —
  // it cannot return true for an unarmed slot whatever the offset is.
  assert.equal(typeof slot.afterFailureAdvance, 'string')
  assert.equal(typeof slot.afterRestart, 'string')
  assert.equal(typeof slot.armingName, 'function')
  assert.equal(typeof slot.beginSequence, 'string')
  assert.equal(typeof slot.isArmed, 'function')
  assert.equal(typeof slot.mayRecover, 'function')
  assert.equal(typeof slot.onMain, 'function')
  assert.equal(typeof slot.onSquash, 'function')
})

// ── CTX-006: armed means "may recover", not "compresses" ───────────────────

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_recovery_needs_arming_a_primed_offset_and_material', () => {
  // The three conjuncts. Offsets 1 and 3 are the primed slots (A′ / B′).
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 1, true), true)
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 3, true), true)

  // An even offset is the FIRST attempt on its side and always sends an ordinary
  // request, even when it was reached by a failure.
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 0, true), false)
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 2, true), false)

  // Armed and primed with nothing to work with: CTX-011 says send the ordinary main
  // request rather than construct an empty probe. Normal, not an error.
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 1, false), false)

  // A crash can leave a primed durable offset with no process-local arming.
  assert.equal(slot.mayRecover(slot.beginSequence, 1, true), false)
  assert.equal(slot.mayRecover(slot.beginSequence, 3, true), false)
})

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_the_primed_slots_are_exactly_the_odd_offsets', () => {
  assert.deepEqual([0, 1, 2, 3].map(cursor.isRecoverySlot), [false, true, false, true])
})

// ── CTX-007: the squash sub-request ────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_a_valid_squash_commits_permanently_and_the_slot_continues', () => {
  const decision = slot.onSquash('Completed')

  assert.equal(decision.name, 'CommitSquashThenMain')

  // FALLBACK-011: the slot has not terminated, so no cursor advance — which is what
  // makes one armed slot produce at most one advance despite two physical requests.
  assert.equal(decision.advancesCursor, false)
})

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_an_invalid_squash_is_skipped_rather_than_repaired', () => {
  // The frames are still there, so spending FALLBACK-008's one repair on a
  // compression would spend it on the wrong thing.
  const decision = slot.onSquash('CompletedInvalid')

  assert.equal(decision.name, 'MainWithoutSquash')
  assert.equal(decision.advancesCursor, false)
})

test('WHAT[CONTEXT-COMPRESSION-007] CTX_007_a_failed_squash_fails_the_slot_without_sending_the_main_request', () => {
  for (const outcome of ['Failed', 'Aborted']) {
    const decision = slot.onSquash(outcome)

    assert.equal(decision.name, 'FailSlot', `${outcome} must fail the slot`)
    assert.equal(decision.advancesCursor, true)
  }
})

// ── CTX-007: the main request ──────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-007] FALLBACK_011_unknown_kind_and_outcome_fail_closed', () => {
  assert.equal(slot.onMain({ kind: 'unknown', outcome: 'Completed' }).ok, false)
  assert.equal(slot.onMain({ kind: requestKind.workMain, outcome: 'unknown' }).ok, false)
  assert.equal(slot.onSquash('unknown').ok, false)
})

test('WHAT[CONTEXT-COMPRESSION-007] FALLBACK_011_only_a_business_main_success_clears_the_failure_count', () => {
  const clears = (kind) => slot.onMain({ kind, outcome: 'Completed' }).clearsFailureCount

  // A squash produced a better representation, not a completed unit of the run's
  // work. A repair salvaged an attempt that already failed to produce a usable
  // terminal. Neither is business completion.
  assert.equal(clears(requestKind.workMain), true)
  assert.equal(clears(requestKind.bloggerMain), true)
  assert.equal(clears(requestKind.bloggerSquash), false)
  assert.equal(clears(requestKind.interactionRepair), false)
})

test('WHAT[CONTEXT-COMPRESSION-007] CTX_007_a_successful_main_commits_and_does_not_move_the_cursor', () => {
  const decision = slot.onMain({ kind: requestKind.workMain, outcome: 'Completed' })

  assert.equal(decision.name, 'CommitMain')
  assert.equal(decision.advancesCursor, false)
  assert.equal(decision.nextArmingName, 'NotArmed', 'success ends the recovery sequence')
})

test('WHAT[CONTEXT-COMPRESSION-007] FALLBACK_008_an_invalid_terminal_earns_exactly_one_repair', () => {
  const first = slot.onMain({ kind: requestKind.bloggerMain, aabbConsumed: false, outcome: 'CompletedInvalid' })
  assert.equal(first.name, 'RepairOnce')
  assert.equal(first.advancesCursor, false, 'an unusable terminal is not a failed slot')

  const second = slot.onMain({ kind: requestKind.bloggerMain, aabbConsumed: true, outcome: 'CompletedInvalid' })
  assert.equal(second.name, 'AbandonRoundProduct')

  // Still no advance: FALLBACK-008 keeps an invalid terminal out of the A/B count
  // entirely. The next offer recomputes the delta from the unchanged baseline
  // (COMPANION-008), so nothing is lost by abandoning this round's product.
  assert.equal(second.advancesCursor, false)
})

test('WHAT[CONTEXT-COMPRESSION-007] CTX_007_a_failed_main_fails_the_slot_for_every_kind', () => {
  for (const kind of requestKind.all) {
    for (const outcome of ['Failed', 'Aborted']) {
      const decision = slot.onMain({ kind, outcome })

      assert.equal(decision.name, 'FailSlot', `${requestKind.label(kind)} / ${outcome}`)
      assert.equal(decision.advancesCursor, true)
    }
  }
})

test('WHAT[CONTEXT-COMPRESSION-005] CTX_005_Failed_and_Aborted_take_the_identical_path', () => {
  // No error text reaches this module and there is no overflow case. Every failure
  // gets the same recovery protocol, so a discriminator would only grow a branch
  // that never executes.
  const failed = slot.onMain({ kind: requestKind.workMain, outcome: 'Failed' })
  const aborted = slot.onMain({ kind: requestKind.workMain, outcome: 'Aborted' })

  assert.deepEqual(failed, aborted)
  assert.deepEqual(slot.onSquash('Failed'), slot.onSquash('Aborted'))
})

// ── CTX-008: exactly one advance per failed slot ───────────────────────────

test('WHAT[CONTEXT-COMPRESSION-007] CTX_008_only_a_failed_slot_advances_the_cursor', () => {
  const advancing = [
    slot.onSquash('Completed'),
    slot.onSquash('CompletedInvalid'),
    slot.onSquash('Failed'),
    slot.onMain({ kind: requestKind.workMain, outcome: 'Completed' }),
    slot.onMain({ kind: requestKind.workMain, outcome: 'CompletedInvalid' }),
    slot.onMain({ kind: requestKind.workMain, aabbConsumed: true, outcome: 'CompletedInvalid' }),
    slot.onMain({ kind: requestKind.workMain, outcome: 'Failed' }),
  ].filter((d) => d.advancesCursor)

  assert.deepEqual(advancing.map((d) => d.name), ['FailSlot', 'FailSlot'])
})

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_the_next_slot_is_armed_exactly_when_this_one_failed', () => {
  assert.equal(slot.onSquash('Failed').nextArmingName, 'ArmedByAdvance')
  assert.equal(slot.onMain({ kind: requestKind.workMain, outcome: 'Failed' }).nextArmingName, 'ArmedByAdvance')

  assert.equal(slot.onSquash('Completed').nextArmingName, 'NotArmed')
  assert.equal(slot.onSquash('CompletedInvalid').nextArmingName, 'NotArmed')
  assert.equal(slot.onMain({ kind: requestKind.workMain, outcome: 'Completed' }).nextArmingName, 'NotArmed')
})

// ── the acceptance trace ───────────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_success_closes_the_primed_slot_so_the_next_failure_recovers_immediately', () => {
  const trace = []
  let arming = slot.beginSequence
  let offset = 0
  let squashes = 0

  const runSlot = ({ label, squashOutcome, mainOutcome, hasMaterial = true }) => {
    const recovering = slot.mayRecover(arming, offset, hasMaterial)

    if (recovering) {
      squashes += 1
      const squash = slot.onSquash(squashOutcome)
      trace.push(`${label}: squash → ${squash.name}`)

      if (squash.advancesCursor) {
        offset = (offset + 1) % 4
        arming = squash.nextArmingName
        return
      }
    } else {
      trace.push(`${label}: no squash (offset=${offset} arming=${slot.armingName(arming)})`)
    }

    const main = slot.onMain({ kind: requestKind.bloggerMain, outcome: mainOutcome })
    trace.push(`${label}: main → ${main.name}`)

    if (main.advancesCursor) {
      offset = (offset + 1) % 4
    } else if (mainOutcome === 'Completed') {
      // PAR-004 settlement: A′→A and B′→B; ordinary A/B stay put.
      if (offset === 1 || offset === 3) offset -= 1
    }
    arming = main.nextArmingName
  }

  // turns 1–4: plain successes. Offset stays 0, nothing is ever armed.
  for (const n of [1, 2, 3, 4]) {
    runSlot({ label: `turn${n}`, mainOutcome: 'Completed' })
  }
  assert.equal(offset, 0)
  assert.equal(squashes, 0)

  // turn 5: the first main fails → Offset 0→1, arming becomes ArmedByAdvance.
  runSlot({ label: 'turn5.slot0', mainOutcome: 'Failed' })
  assert.equal(offset, 1)
  assert.equal(slot.armingName(arming), 'ArmedByAdvance')

  // turn 5 retry: armed AND on the primed offset 1, so it squashes first, then the
  // main succeeds. Success closes A′ and normalizes back to A's ordinary slot.
  runSlot({ label: 'turn5.slot1', squashOutcome: 'Completed', mainOutcome: 'Completed' })
  assert.equal(squashes, 1)
  assert.equal(offset, 0, 'success closes the primed recovery subslot')
  assert.equal(slot.armingName(arming), 'NotArmed')

  // A later real failure now reaches A′ immediately. It must not burn an ordinary
  // B request first; that is the regression seen as paired context-overflow errors.
  runSlot({ label: 'turn6', mainOutcome: 'Completed' })
  assert.equal(squashes, 1, 'an ordinary successful turn must not trigger a second squash')
  assert.equal(offset, 0)

  runSlot({ label: 'later.slot0', mainOutcome: 'Failed' })
  assert.equal(offset, 1)
  runSlot({ label: 'later.slot1', squashOutcome: 'Completed', mainOutcome: 'Completed' })
  assert.equal(squashes, 2, 'the cascade squash is the second one overall')
  assert.equal(offset, 0)

  assert.deepEqual(trace, [
    'turn1: no squash (offset=0 arming=NotArmed)',
    'turn1: main → CommitMain',
    'turn2: no squash (offset=0 arming=NotArmed)',
    'turn2: main → CommitMain',
    'turn3: no squash (offset=0 arming=NotArmed)',
    'turn3: main → CommitMain',
    'turn4: no squash (offset=0 arming=NotArmed)',
    'turn4: main → CommitMain',
    'turn5.slot0: no squash (offset=0 arming=NotArmed)',
    'turn5.slot0: main → FailSlot',
    'turn5.slot1: squash → CommitSquashThenMain',
    'turn5.slot1: main → CommitMain',
    'turn6: no squash (offset=0 arming=NotArmed)',
    'turn6: main → CommitMain',
    'later.slot0: no squash (offset=0 arming=NotArmed)',
    'later.slot0: main → FailSlot',
    'later.slot1: squash → CommitSquashThenMain',
    'later.slot1: main → CommitMain',
  ])
})

test('WHAT[CONTEXT-COMPRESSION-002] FALLBACK_012_at_least_one_real_failure_separates_any_two_squashes', () => {
  // The invariant the whole design exists to produce: compression is a by-product of
  // recovery, not routine housekeeping. Stated as a property over the decision graph
  // rather than one trace — a squash is only reachable from an armed slot, and the
  // only thing that arms a slot is a failure advance.
  const armingSources = [
    slot.onSquash('Completed'),
    slot.onSquash('CompletedInvalid'),
    slot.onSquash('Failed'),
    slot.onMain({ kind: requestKind.bloggerMain, outcome: 'Completed' }),
    slot.onMain({ kind: requestKind.bloggerMain, outcome: 'CompletedInvalid' }),
    slot.onMain({ kind: requestKind.bloggerMain, aabbConsumed: true, outcome: 'CompletedInvalid' }),
    slot.onMain({ kind: requestKind.bloggerMain, outcome: 'Failed' }),
    slot.onMain({ kind: requestKind.bloggerMain, outcome: 'Aborted' }),
  ]

  for (const decision of armingSources) {
    if (decision.nextArmingName === 'ArmedByAdvance') {
      assert.equal(decision.name, 'FailSlot', 'only a failed slot may arm the next one')
      assert.equal(decision.advancesCursor, true)
    }
  }
})

// ── the request kinds themselves ───────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_only_the_work_main_request_may_carry_a_prefix_probe', () => {
  // A Companion request has no prefix to probe — its history is the frame sequence —
  // and a repair reuses whatever the attempt it repairs already sent.
  assert.equal(requestKind.mayCarryProbe(requestKind.workMain), true)
  assert.equal(requestKind.mayCarryProbe(requestKind.bloggerMain), false)
  assert.equal(requestKind.mayCarryProbe(requestKind.bloggerSquash), false)
  assert.equal(requestKind.mayCarryProbe(requestKind.interactionRepair), false)
})

test('WHAT[CONTEXT-COMPRESSION-007] PROMPT_008_every_request_kind_has_a_distinct_diagnostic_label', () => {
  const labels = requestKind.all.map(requestKind.label)

  // Every kind has a distinct diagnostic label, and no two kinds share one.
  assert.equal(new Set(labels).size, labels.length)
  assert.ok(labels.includes('work-main'))
})

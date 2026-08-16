// TipDeliveryProjection — restart-safe Main tip Full/Identity delivery fold.
//
// Folded only from HostFact.TipGuidanceDelivered; never a private file ledger
// or process-local set (Rulebook §14–16, ENFORCER-071). Full adds the tip to
// FullDeliveredTips; IdentityOnly is audit-only; ContextReanchored voids Full
// history so a later resolve re-emits full main.md (semantic restoration, not
// a new occurrence).
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  FactModule,
} from '../../verification-system/tests/support/domain.mjs'
import {
  TipDeliveryProjection_empty as empty,
  TipDeliveryProjection_apply as apply,
  TipDeliveryProjection_applyReanchor as applyReanchor,
  TipDeliveryProjection_hasFullDelivered as hasFullDelivered,
} from '../../../dist/Enforcer/Guidance/DeliveryProjection.js'

const TipPresentation = FactModule.TipPresentation

test('WHAT[GD-004] TDP_001_empty_state_has_nothing_delivered', () => {
  assert.equal(hasFullDelivered('primitive-obsession', empty), false)
})

test('WHAT[GD-003] TDP_002_full_marks_tip_delivered_identity_only_does_not', () => {
  let state = apply('primitive-obsession', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)

  // IdentityOnly repeat is audit-only: it must not record a Full delivery.
  state = apply('ignored-tdd', TipPresentation.IdentityOnly, state)
  assert.equal(hasFullDelivered('ignored-tdd', state), false)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)
})

test('WHAT[GD-003] TDP_003_blank_or_null_tip_name_is_ignored', () => {
  const afterBlank = apply('   ', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('   ', afterBlank), false)
  assert.deepEqual(afterBlank, empty)
  const afterNull = apply(null, TipPresentation.Full, empty)
  assert.deepEqual(afterNull, empty)
})

test('WHAT[GD-005] TDP_004_reanchor_voids_full_history_so_next_resolve_refulls', () => {
  let state = apply('primitive-obsession', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)

  // HOST-006 compaction reanchor: coverage is horizon-relative and lost, so
  // Full history is voided — the next resolve must re-emit full main.md.
  state = applyReanchor(state)
  assert.equal(hasFullDelivered('primitive-obsession', state), false)

  // And the re-emission is a Full again (restoration), recorded on the fold.
  state = apply('primitive-obsession', TipPresentation.Full, state)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)
})

test('WHAT[GD-005] TDP_005_reanchor_does_not_advance_occurrence_frontier', () => {
  // applyReanchor only clears the horizon-relative Full history. The
  // occurrence-based frontier is a different axis (TipDeliveryProjection
  // tracks FullDeliveredTips per Main session; reanchor never mints a new
  // occurrence). This locks that re-Full after reanchor is restoration, not
  // a fresh first delivery.
  let state = apply('primitive-obsession', TipPresentation.Full, empty)
  const before = state
  state = applyReanchor(state)
  assert.notDeepEqual(state, before, 'reanchor must void Full history')
  // Re-applying Full after reanchor yields the same observable state as the
  // original first Full — no accumulation, no new semantic identity.
  const refilled = apply('primitive-obsession', TipPresentation.Full, state)
  assert.deepEqual(refilled, before)
})

test('WHAT[GD-001] TDP_006_frontier_and_coverage_are_two_axes_not_one_bool', () => {
  // GD-001 两轴分离：Frontier（哪些 occurrence 已交付，durable/monotonic）与
  // Coverage（全文此刻是否可恢复，horizon-relative）不得压成单一 durable bool。
  // 前沿轴：Full 交付被记录（monotonic 前进）。
  const firstFull = apply('primitive-obsession', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('primitive-obsession', firstFull), true)
  // 覆盖轴：reanchor 清 Coverage 表达（FullDeliveredTips 投影被 void），
  // 但 re-Full 后的状态与首次 Full 逐字节相同——既不误删已交付事实，
  // 也不把语义恢复记成新 occurrence（单一 bool 必然在二选一上失败）。
  const afterReanchor = applyReanchor(firstFull)
  assert.equal(hasFullDelivered('primitive-obsession', afterReanchor), false)
  const refilled = apply('primitive-obsession', TipPresentation.Full, afterReanchor)
  assert.deepEqual(refilled, firstFull)
})


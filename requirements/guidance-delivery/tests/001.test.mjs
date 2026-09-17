import assert from 'node:assert/strict'
import test from 'node:test'
import * as delivery from '../../../dist/Enforcer/Guidance/DeliverySurface.js'

const { empty, apply, applyReanchor, hasFullDelivered } = delivery

const TipPresentation = Object.freeze({ Full: 'Full', IdentityOnly: 'IdentityOnly' })

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

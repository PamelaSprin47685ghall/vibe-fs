// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: provider-attempt-recovery.
//
// AttemptExecutionProfile (SPLIT@cutover note in context-compression PROOF):
// the cursor is the only thing that moves the effective agent, and promotion is
// gated on a probe attempt with a usable terminal.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as cursorOwner from '../../../dist/Participant/Provider/Attempt/Fallback/CursorSurface.js'

const planner = compression.attemptPlanner
const cursor = cursorOwner.cursor
const prefix = compression
const prefixProbe = compression.prefixProbe
const requestKind = compression.requestKind
const slot = compression

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) =>
  prefixProbe({ probeId: id, basedOnEpoch: 0, candidate: snapshotAt(cutoff) })

test('WHAT[PAR-013] FALLBACK_002_the_cursor_is_the_only_thing_that_moves_the_effective_agent', () => {
  const at = (offset) =>
    planner.plan({ cursor: cursor.atOffset(offset), kind: requestKind.workMain }).effectiveAgent

  // A/A′ take the selected side, B/B′ the peer. The authority profile is identical in
  // all four; only the cursor differs.
  assert.deepEqual([0, 1, 2, 3].map(at), ['fast-coder', 'fast-coder', 'deep-coder', 'deep-coder'])
})

// ── CTX-012: what may promote ─────────────────────────────────────────────

test('WHAT[PAR-008] CTX_012_only_a_probe_attempt_with_a_usable_terminal_may_promote', () => {
  const withProbe = planner.plan({
    kind: requestKind.workMain,
    mayRecover: true,
    probe: probeFor({ id: 'probe-p1' }),
  })

  assert.equal(planner.promotableProbeId(withProbe, 'Completed'), 'probe-p1')

  // An invalid terminal arrived intact but is unusable (CTX-004), so there is nothing
  // to promote — FALLBACK-008 gives it a repair instead.
  assert.equal(planner.promotableProbeId(withProbe, 'CompletedInvalid'), null)
  assert.equal(planner.promotableProbeId(withProbe, 'Failed'), null)
  assert.equal(planner.promotableProbeId(withProbe, 'Aborted'), null)
})

test('WHAT[PAR-011] CTX_012_an_attempt_without_a_probe_cannot_promote_even_on_success', () => {
  const withoutProbe = planner.plan({ kind: requestKind.workMain, mayRecover: false })

  assert.equal(planner.promotableProbeId(withoutProbe, 'Completed'), null)
})

// ── PAR-010: 槽内维护子请求（RecoverySlot 决策表）───────────────────────────
//
// 一个自动恢复槽至多两个物理 provider request:Step 1 维护(BloggerSquash),
// Step 2 业务主请求(WorkMain/BloggerMain)。决策逻辑在 Domain/RecoverySlot.fs。
// 每个失败槽在终态恰好产生一次 FallbackCursorAdvanced;维护成功单独不算
// Logical Run 业务完成(不清零 ConsecutiveFailureCount)。

test('WHAT[PAR-010] PAR_010_a_failed_squash_fails_the_slot_without_sending_the_main_request', () => {
  // 维护失败 → 槽失败,不发主请求,记录唯一 FallbackCursorAdvanced。
  const decision = slot.onSquash('Failed')

  assert.equal(decision.name, 'FailSlot')
  assert.equal(decision.advancesCursor, true)
})

test('WHAT[PAR-010] PAR_010_a_successful_squash_keeps_the_count_and_continues_to_the_main_request', () => {
  // 维护成功 → 不清零 ConsecutiveFailureCount,继续主请求(CommitSquashThenMain
  // 之后才是 CommitMain)。squash 成功不是业务完成,所以不清零。
  const decision = slot.onSquash('Completed')

  assert.equal(decision.name, 'CommitSquashThenMain')
  assert.equal(decision.advancesCursor, false)
  assert.equal(decision.clearsFailureCount, false, 'squash 成功不携带清零语义')

  const main = slot.onMain({ kind: requestKind.workMain, outcome: 'Completed' })
  assert.equal(main.name, 'CommitMain')
  assert.equal(main.clearsFailureCount, true, '只有业务主请求成功才清零')
})

test('WHAT[PAR-010] PAR_010_a_failed_main_fails_the_slot_and_advances_exactly_once', () => {
  // 主失败 → 槽失败,记录唯一 FallbackCursorAdvanced。维护失败与主失败都走
  // FailSlot,每个失败槽恰好推进一次——一个 armed 槽两个物理请求至多一次 advance。
  const failing = [slot.onSquash('Failed'), slot.onMain({ kind: requestKind.workMain, outcome: 'Failed' })]

  assert.deepEqual(
    failing.map((d) => ({ name: d.name, advances: d.advancesCursor })),
    [
      { name: 'FailSlot', advances: true },
      { name: 'FailSlot', advances: true },
    ],
  )
})

test('WHAT[PAR-010] PAR_010_only_a_business_main_success_clears_the_failure_count', () => {
  // 清零只属于 WorkMain/BloggerMain 的成功;BloggerSquash(维护)与
  // InteractionRepair(修复)的成功不清零——它们不是 Logical Run 业务完成。
  const clears = (kind) => slot.onMain({ kind, outcome: 'Completed' }).clearsFailureCount

  assert.equal(clears(requestKind.workMain), true)
  assert.equal(clears(requestKind.bloggerMain), true)
  assert.equal(clears(requestKind.bloggerSquash), false)
  assert.equal(clears(requestKind.interactionRepair), false)
})

// ── PAR-008: 空 / XML-only terminal 不计入推进 ───────────────────────────────

test('WHAT[PAR-008] PAR_008_an_invalid_terminal_earns_at_most_one_repair_and_never_advances', () => {
  // CompletedInvalid 是「回应完整但不可用」,不是「请求失败」:至多一次
  // Interaction Repair continuation,不推进 cursor、不消耗预算。
  const first = slot.onMain({ kind: requestKind.bloggerMain, aabbConsumed: false, outcome: 'CompletedInvalid' })
  assert.equal(first.name, 'RepairOnce')
  assert.equal(first.advancesCursor, false, '不可用 terminal 不是已确认失败')

  // 第二次仍不可用:放弃本轮产品,依然不推进——无效 terminal 完全不出现在 A/B 计数里。
  const second = slot.onMain({ kind: requestKind.bloggerMain, aabbConsumed: true, outcome: 'CompletedInvalid' })
  assert.equal(second.name, 'AbandonRoundProduct')
  assert.equal(second.advancesCursor, false)
})

// ── PAR-011: armed 合取（本包视角的 slot 门控）────────────────────────────────

test('WHAT[PAR-011] PAR_011_recovery_requires_arming_a_primed_offset_and_material', () => {
  // armedByFailure ∧ primed ∧ hasMaterial 三者缺一不可。primed = Offset 为奇数。
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 1, true), true)
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 3, true), true)

  // 偶数 Offset 是侧首attempt,即使由失败推进而来也发普通请求。
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 0, true), false)
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 2, true), false)

  // 无材料:发普通主请求(CTX-011 no-candidate),不是错误。
  assert.equal(slot.mayRecover(slot.afterFailureAdvance, 1, false), false)

  // parked-cursor 陷阱:奇数 Offset + 材料,但没有 armed——成功可停在奇数 Offset,
  // 此时 armedByFailure=false,禁止仅凭 Offset 判定 armed。
  assert.equal(slot.mayRecover(slot.beginSequence, 1, true), false)
  assert.equal(slot.mayRecover(slot.beginSequence, 3, true), false)
})

test('WHAT[PAR-011] PAR_011_arming_is_a_control_flow_fact_not_a_position', () => {
  // 新 Logical Run 第一槽永不 armed;崩溃/重启后自动丢失(安全侧 Fail-Closed)。
  // 类型故意不提供「这个 Offset 是否 armed」的函数——答案是本次序列的控制流事实。
  assert.equal(slot.armingName(slot.beginSequence), 'NotArmed')
  assert.equal(slot.armingName(slot.afterRestart), 'NotArmed')
  assert.equal(slot.afterRestart, slot.beginSequence, '重启后与全新序列不可区分')
  assert.equal(slot.armingName(slot.afterFailureAdvance), 'ArmedByAdvance')

  // 任意两次 squash 之间至少隔一次真实物理失败:只有失败推进把下一个槽 armed。
  assert.equal(slot.onMain({ kind: requestKind.workMain, outcome: 'Completed' }).nextArmingName, 'NotArmed')
  assert.equal(slot.onMain({ kind: requestKind.workMain, outcome: 'Failed' }).nextArmingName, 'ArmedByAdvance')
})

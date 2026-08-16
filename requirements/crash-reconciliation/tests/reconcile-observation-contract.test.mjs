// requirements/crash-reconciliation/tests/reconcile-observation-contract.test.mjs
//
// CRASH-003 / CRASH-007 最小 contract test：本包直接消费 Reconcile 纯域
// （structured-workflow 的 RECONCILE_PROGRAM_* 完整矩阵留在原 owner 处，本文件
// 只锁本包命题的契约面）：
//   - CRASH-003：未决外部 effect 先 reconcile 再决定是否可重试——finish=None
//     的稳定快照只作为 reconciliation 私有观测（SnapshotObservation），在无
//     quiescence 证据时绝不被当作「未发生」而重放（StopPass），ReconcileDecision
//     只有 Reread/Publish/StopPass observation vocabulary，无业务 repair 名字。
//   - CRASH-007：TurnUnknown 不得作为 TurnOutcome case 发布；publishDecision 的
//     交接面（PublishTurn.Outcome）在类型上只接受 TurnOutcome。
import assert from 'node:assert/strict'
import test from 'node:test'

import {
  quiescencePermit,
  reconcileProgram,
  reconcileWake,
} from '../../verification-system/tests/support/domain.mjs'

test('WHAT[CRASH-003] unknown_effect_without_quiescence_is_not_replayed', () => {
  // decideStep 是 reconcile 的核心：finish=None（Unknown）稳定快照在
  // Retry/Failure wake 下耗尽预算 → StopPass，而不是 Publish 一个「当作未发生」
  // 的 continuation；只有 IdleWake（携带 QuiescencePermit）才把观测交接给业务。
  const name = (remaining, evidence, wake) =>
    reconcileProgram.decisionName(reconcileProgram.decideStep(wake, remaining, evidence))

  // 耗尽预算：SnapshotError / NoTurn 无可作用对象 → StopPass（不重放、不猜继续）。
  assert.equal(name(0, reconcileProgram.evidence.snapshotError('transient')), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.noTurn()), 'StopPass')

  // 耗尽预算：Unknown 在 retry/failure wake 下 StopPass —— 观测稳定但不静止，
  // 不得当作「已 reconcile 完成」重试。
  assert.equal(name(0, reconcileProgram.evidence.unknown(), reconcileWake.retryWake()), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.unknown(), reconcileWake.failureWake()), 'StopPass')
  assert.equal(name(0, reconcileProgram.evidence.unknown(), reconcileWake.abortWake()), 'StopPass')

  // 只有 IdleWake（fresh idle 证据）才 Publish —— reconcile 先于任何 effect 决策。
  assert.equal(
    name(0, reconcileProgram.evidence.unknown(), reconcileWake.idleWake(quiescencePermit.create('ses-a', 1))),
    'Publish',
  )

  // 预算未耗尽时仍在 reconcile（Reread），不提前下结论。
  assert.equal(name(3, reconcileProgram.evidence.unknown(), reconcileWake.retryWake()), 'Reread')
})

test('WHAT[CRASH-003] reconcile_decision_has_no_business_repair_vocabulary', async () => {
  // ReconcileDecision 只有 observation vocabulary：Reread / Publish / StopPass。
  // 不含任何业务 repair 名字（CRASH-003/004 同一契约面）。
  const mod = await import(new URL('../../../dist/Composition/Turn/Program.js', import.meta.url).pathname)
  const decisionCases = Object.create(mod.ReconcileDecision.prototype).cases()
  assert.deepEqual(
    decisionCases,
    ['Reread', 'Publish', 'StopPass'],
    `ReconcileDecision must stay observation-only; have: ${decisionCases.join(', ')}`,
  )
  assert.equal(
    decisionCases.some((c) => /Repair|Resend|Rollback|Abort|Replay/i.test(c)),
    false,
    'ReconcileDecision must not carry business repair names',
  )
})

test('WHAT[CRASH-007] turn_unknown_is_snapshot_observation_not_turn_outcome', async () => {
  const mod = await import(new URL('../../../dist/Composition/Turn/Program.js', import.meta.url).pathname)

  // TurnUnknown 不在 TurnOutcome case 列表里：publishDecision 在类型上不可接收它。
  const turnOutcomeCases = Object.create(mod.TurnOutcome.prototype).cases()
  assert.deepEqual(
    turnOutcomeCases,
    ['TurnInProgress', 'TurnNeedsContinuation', 'TurnCompleted', 'TurnAborted', 'TurnFailed'],
    `publishable TurnOutcome must exclude TurnUnknown; have: ${turnOutcomeCases.join(', ')}`,
  )

  // TurnUnknown 只作为 reconciliation 私有 SnapshotObservation 存在。
  assert.equal(typeof mod.SnapshotObservation, 'function')
  assert.deepEqual(
    Object.create(mod.SnapshotObservation.prototype).cases(),
    ['TurnUnknown'],
    'SnapshotObservation must carry TurnUnknown only',
  )

  // outcomeOf 拒绝把 TurnUnknown 铸成 TurnOutcome（不得静默 mint，也不得塌缩成
  // TurnFailed 假 terminal）。
  let minted
  let refused = false
  try {
    minted = mod.outcomeOf('TurnUnknown')
  } catch {
    refused = true
  }
  if (!refused) {
    assert.notEqual(
      minted.cases()[minted.tag],
      'TurnUnknown',
      'outcomeOf("TurnUnknown") must not return a TurnOutcome.TurnUnknown case',
    )
    assert.notEqual(
      minted.cases()[minted.tag],
      'TurnFailed',
      'outcomeOf("TurnUnknown") must not collapse Unknown into a false TurnFailed terminal',
    )
  }
})

test('WHAT[CRASH-007] publish_boundary_carries_turn_outcome_not_snapshot_observation', async () => {
  // publishDecision 的交接面 PublishTurn.Outcome 的 reflection 类型是 TurnOutcome；
  // 结构上不可能携带 SnapshotObservation。
  const mod = await import(new URL('../../../dist/Composition/Turn/Program.js', import.meta.url).pathname)
  const publishTurnRef = mod.PublishTurn_$reflection()
  const outcomeFieldType = JSON.stringify((publishTurnRef.fields() ?? []).find(([name]) => name === 'Outcome')?.[1] ?? '')
  assert.match(outcomeFieldType, /TurnOutcome/, 'PublishTurn.Outcome must be typed TurnOutcome')
  assert.doesNotMatch(
    outcomeFieldType,
    /SnapshotObservation/,
    'PublishTurn.Outcome must never be typed SnapshotObservation',
  )

  // 行为面：terminal 交接照常 publish 一次并 seal，重复 token 被 dedupe ——
  // 交接面只消费 TurnOutcome，Unknown 无法进入。
  const terminal = reconcileProgram.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })
  const first = reconcileProgram.publishDecision(reconcileProgram.publishMaps.empty(), terminal)
  assert.equal(first.shouldPublish, true)
  const second = reconcileProgram.publishDecision(first.maps, terminal)
  assert.equal(second.shouldPublish, false, 'same completion must be sealed once (dedupe, no replay)')
})

// WHAT[effect-accounting-002] Requested-only 是未知结果：
//   有请求、无确认只表示结局未知，不得视为未发生而重发，也不得视为成功而推进。
//   修复预算耗尽时须以明确失败结束处理，副作用的未知事实仍须保留，
//   不得无限挂起或伪造结局。
//
// 取证面全部是已注册的公开 Surface：
//   * dist/Execution/Delegation/Fork/Host/HostForkRunLifecycleSurface.js ——
//     失败结算（Failed）不经过 durable 见证，用它断言「明确失败」那一支；
//   * dist/Execution/Delegation/Handle/JournalSurface.js —— 工作单元的 durable
//     事实（lifecycle / lifecycleKind / completionRef / completionDigest），
//     这是「未知」的非等待观察面：不需要等一个可能永远不会来的完成。
//
// 「保持 pending」这一支不在伪造面上证明：伪造面的 journal 恒为 None，
// 其 Completed 分支找不到围栏匹配的见证（LC-004）因而永不结算，await 一个
// 不会 settle 的 completion cell 只会挂起。因此 pending 的两条 case 走真实
// fork 路径：真实 `this.fork`、真实子会话、真实共享终端端口。
//
// 已知缺口（如实记录，不假装已修）：伪造面 `HostForkRunLifecycleSurface` 的
// journal 恒为 None，它的 `Completed` API 因此是一个**假 API**——生产在
// journal-less 分支里拿不到围栏匹配的见证，而它给的 view 也是空，LC-004
// 于是让它只观察、永不结算。本文件因此**不再**对它断言任何 Completed 结算
// （旧断言里的 `view: ''` 从来不是一个可被产出的结局，已删除）。彻底清退该
// 面需要给它接上真实 journal，或者让它的最后一条 case 也迁到真实路径——
// 两者都超出本次可交付范围。

import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycleModule from '../../../dist/Execution/Delegation/Fork/Host/HostForkRunLifecycleSurface.js'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as handleJournal from '../../../dist/Execution/Delegation/Handle/JournalSurface.js'
import {
  agentIdOf,
  childTurn,
  deliverIntoOpenWork,
  forkCall,
  joinCall,
  observeTerminals,
  physicalUserMessageOf,
  withRealFork,
} from '../../delegation/tests/support/real-fork.mjs'

// The provider language is a global preference; the real plugin path boots the
// plugin and refreshes that preference, so this file pins one language.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

const lifecycle = {
  create: lifecycleModule.HostForkRunLifecycleSurface_create,
  complete: lifecycleModule.HostForkRunLifecycleSurface_complete,
  observe: lifecycleModule.HostForkRunLifecycleSurface_observe,
  completion: lifecycleModule.HostForkRunLifecycleSurface_completion,
}

const makeRun = (agentId, childId, parentId) => lifecycle.create({ agentId, childId, parentId })

/** The durable view of one work unit, read through the registered public surface. */
const recordOf = (runtime, owner, agentId, child, root) =>
  handleJournal.snapshot(runtime.journal, owner, agentId, child, root).record

/**
 * The non-waiting observation of a pending work: the work unit stays Active
 * and it has accepted no completion at all, so no completion identity exists.
 *
 * This is a single durable snapshot taken after the real event barrier — it
 * never awaits a completion cell (a pending run's cell may legitimately never
 * settle, and waiting for it would hang the very case under proof) and it
 * never repeats an assertion over a fixed interval.
 */
const assertStillPending = (record, message) => {
  assert.ok(record, `${message}: the work unit must stay admitted`)
  assert.equal(record.lifecycle, 'Active', `${message}: the run must not settle`)
  assert.equal(record.lifecycleKind, null, `${message}: no completion may be accepted`)
  assert.equal(record.completionDigest, null, `${message}: no completion may be recorded`)
  assert.equal(record.completionRef, null, `${message}: no completion may be recorded`)
}

test('WHAT[effect-accounting-002] EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed', async () => {
  const owner = 'effect-accounting-002-missing-final-report'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'MISSING-FINAL-REPORT-CHARGE', 'call-fork-mfr')
    assert.match(first, /Ada/)
    const agentId = agentIdOf(first)
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const root = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      // The repair budget of this attempt is exhausted: the child reports
      // MISSING_FINAL_REPORT. That is not a proven failure of the work — the
      // run must stay open for a later proven terminal, so it must not be
      // failed and must not settle the parent's completion cell.
      //
      // The stop carries the work's own admitted authority root, so it
      // addresses this run. The event barrier is the production publication
      // of that stop on the shared terminal port.
      const published = eventsSurface.notifyForAuthority(
        await runtime.terminalPort,
        child,
        'Failed',
        root,
        'MISSING_FINAL_REPORT: bounded retry continues',
      )
      assert.equal(published, true, 'the stop must reach the shared terminal port')
      await terminals.awaitRun(child, undefined, { kind: 'Failed' })

      assertStillPending(recordOf(runtime, owner, agentId, child, root), 'MISSING_FINAL_REPORT')

      // The run is still alive and still owns the work: a later proven
      // delivery settles it as completed, with the material it really
      // delivered. This turn runs under the SAME physical input, so it
      // addresses the SAME admitted work unit — the original completion cell,
      // not a fresh obligation under a new root.
      await childTurn(
        hooks,
        runtime,
        child,
        `run-${child}-2`,
        'RECOVERED REPORT',
        'RECOVERED-RECORD with files = 1',
        terminals.awaitRun,
      )

      const recovered = recordOf(runtime, owner, agentId, child, root)
      assert.equal(recovered.lifecycle, 'CompletedAwaitingJoin')
      assert.equal(recovered.lifecycleKind, 'Terminal')
      assert.ok(recovered.completionDigest, 'the proven delivery records its own identity')
      assert.ok(recovered.completionRef)

      const joinReceipt = await joinCall(hooks, owner, 'call-join-mfr')
      assert.match(joinReceipt, /RECOVERED-RECORD with files = 1/)
      assert.doesNotMatch(joinReceipt, /MISSING_FINAL_REPORT/)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[effect-accounting-002] EXEC_join_empty_Completed_keeps_run_pending_not_failed', async () => {
  const owner = 'effect-accounting-002-empty-completed'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'EMPTY-COMPLETION-CHARGE', 'call-fork-empty')
    assert.match(first, /Ada/)
    const agentId = agentIdOf(first)
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const root = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      // A completed turn that delivered no material: the child owes the parent
      // a delivery and delivered nothing of substance. Production therefore
      // publishes no terminal for that turn at all — the outcome is unknown,
      // not a failure, so the run must stay open and pending. The turn's own
      // production handling (the session.idle hook returns only after the
      // terminal decision has been made) is the event barrier.
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'EMPTY MATERIAL REPORT', null, () => {})

      assertStillPending(recordOf(runtime, owner, agentId, child, root), 'empty Completed')

      // The child was not failed either: an unknown outcome publishes no
      // terminal of any kind.
      assert.deepEqual(
        terminals.seen.filter((entry) => entry.sessionId === child),
        [],
        'an undelivered turn must publish no terminal for the child',
      )

      // The work unit is still open and still deliverable, so no join is
      // called yet: a join against an open work unit legitimately waits.
      //
      // The later proven delivery goes into the SAME admitted work unit: it
      // runs under the same physical input, so it addresses the same
      // authority root and settles the ORIGINAL work unit — not a fresh
      // obligation under a new root. The physical input has already spent
      // its single provider step, so the delivery does not re-enter the
      // provider (a second provider step on one physical message fails
      // closed with EMR-010); it delivers into the open work unit directly.
      await deliverIntoOpenWork(hooks, runtime, child, `run-${child}-2`, 'REAL REPORT', 'REAL-RECORD with files = 3')
      await terminals.awaitRun(child, `run-${child}-2`)

      const settled = recordOf(runtime, owner, agentId, child, root)
      assert.equal(settled.lifecycle, 'CompletedAwaitingJoin')
      assert.equal(settled.lifecycleKind, 'Terminal')
      assert.ok(settled.completionDigest, 'the proven delivery records its own identity')
      assert.ok(settled.completionRef)

      // Only now, after the work unit has settled, is a join meaningful.
      const joinReceipt = await joinCall(hooks, owner, 'call-join-empty')
      assert.match(joinReceipt, /REAL-RECORD with files = 3/)
      assert.doesNotMatch(joinReceipt, /EMPTY MATERIAL REPORT/)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[effect-accounting-002] EXEC_join_interaction_repair_exhausted_settles_the_run', async () => {
  // A proven failure settles the run: Failed settlement involves no durable
  // delivery witness, so the fabricated lifecycle surface is the honest place
  // to assert the owner's real Failed path.
  const run = makeRun('agent-repair-exhausted', 'ses_repair_exhausted_child', 'ses_repair_exhausted_parent')
  await lifecycle.complete(run, { kind: 'Failed', message: 'INTERACTION_REPAIR_EXHAUSTED' })

  const observed = lifecycle.observe(run)
  assert.equal(observed.finished, true, 'bounded repair exhaustion is a terminal failure, not another repair opportunity')
  assert.equal(observed.pending, false)
  assert.deepEqual(await lifecycle.completion(run), {
    status: 'failed',
    agentId: 'agent-repair-exhausted',
    code: 'ERROR',
    message: 'INTERACTION_REPAIR_EXHAUSTED',
  })
})

test('WHAT[effect-accounting-002] EXEC_join_real_Failed_still_claims_run', async () => {
  // A proven failure settles the run: Failed settlement involves no durable
  // delivery witness, so the fabricated lifecycle surface is the honest place
  // to assert the owner's real Failed path.
  const run = makeRun('agent-real', 'ses_real_child', 'ses_real_parent')
  await lifecycle.complete(run, { kind: 'Failed', message: 'provider timeout' })

  const observed = lifecycle.observe(run)
  assert.equal(observed.finished, true, 'real failure must still claim the run')
  assert.equal(observed.pending, false)
  assert.deepEqual(await lifecycle.completion(run), {
    status: 'failed',
    agentId: 'agent-real',
    code: 'ERROR',
    message: 'provider timeout',
  })
})

test('WHAT[effect-accounting-002] EXEC_join_first_proven_terminal_is_single_assignment', async () => {
  // Failed-first: a proven failure settles the run and no later terminal may
  // take the cell again. Failed settlement involves no durable delivery
  // witness, so it is asserted on the fabricated lifecycle surface.
  const failedFirst = makeRun('agent-failed-first', 'ses_failed_first_child', 'ses_failed_first_parent')
  await lifecycle.complete(failedFirst, { kind: 'Failed', message: 'providers exhausted' })
  await lifecycle.complete(failedFirst, { kind: 'Completed', terminalText: 'late success' })
  assert.deepEqual(await lifecycle.completion(failedFirst), {
    status: 'failed',
    agentId: 'agent-failed-first',
    code: 'ERROR',
    message: 'providers exhausted',
  })

  // Completed-first: proved on the real fork path, because a settled Completed
  // carries the material that was really delivered — the fabricated surface
  // has no journal and no witness, so its Completed branch can never settle
  // (LC-004: a blank view is not a settlement). The old `view: ''`
  // expectation was therefore not a reachable outcome and has been deleted.
  const owner = 'effect-accounting-002-single-assignment'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'COMPLETED-FIRST-CHARGE', 'call-fork-single')
    assert.match(first, /Ada/)
    const agentId = agentIdOf(first)
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const root = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      await childTurn(
        hooks,
        runtime,
        child,
        `run-${child}-1`,
        'PROVEN REPORT',
        'PROVEN-RECORD with files = 4',
        terminals.awaitRun,
      )

      const settled = recordOf(runtime, owner, agentId, child, root)
      assert.equal(settled.lifecycle, 'CompletedAwaitingJoin')
      assert.equal(settled.lifecycleKind, 'Terminal')
      assert.ok(settled.completionDigest, 'the proven delivery records its own identity')

      // A late stale failure addressing the same root must not take the cell
      // a second time: the first proven terminal keeps the single assignment.
      eventsSurface.notifyForAuthority(
        await runtime.terminalPort,
        child,
        'Failed',
        root,
        'late stale failure',
      )
      await terminals.awaitRun(child, undefined, { kind: 'Failed' })

      const after = recordOf(runtime, owner, agentId, child, root)
      assert.equal(after.lifecycle, 'CompletedAwaitingJoin', 'the first proven terminal keeps the cell')
      assert.equal(after.lifecycleKind, 'Terminal')
      assert.equal(after.completionDigest, settled.completionDigest)

      const joinReceipt = await joinCall(hooks, owner, 'call-join-single')
      assert.match(joinReceipt, /PROVEN-RECORD with files = 4/)
      assert.doesNotMatch(joinReceipt, /late stale failure/)
    } finally {
      terminals.dispose()
    }
  })
})

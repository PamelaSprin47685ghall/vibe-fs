// WHAT[effect-accounting-002] Requested-only 是未知结果：
//   有请求、无确认只表示结局未知，不得视为未发生而重发，也不得视为成功而推进。
//   修复预算耗尽时须以明确失败结束处理，副作用的未知事实仍须保留，
//   不得无限挂起或伪造结局。
//
// 取证面全部是已注册的公开 Surface：
//   * dist/OpenCode/Host/EventsSurface.js —— 在共享终端端口上发布一条带权威根
//     的终局（Failed），这是生产自己的终局发布入口；
//   * dist/Execution/Delegation/Handle/JournalSurface.js —— 工作单元的 durable
//     事实（lifecycle / lifecycleKind / completionRef / completionDigest），
//     这是「未知」的非等待观察面：不需要等一个可能永远不会来的完成。
//
// 全部五条 case 都走真实 fork 路径：真实 `this.fork`、真实子会话、真实共享
// 终端端口、真实 journal。此前使用的伪造面 `HostForkRunLifecycleSurface` 已
// 删除：它的 `complete` 恒以 `journal = None` 调生产，其 Completed 分支拿不到
// 围栏匹配的 durable 见证，于是走 LC-004 的「只观察、不结算」分支（空白 view
// 不是结算），既不 HandleCompleted 也不 SetResult——那是一个假 API，await 它
// 只会挂起。失败结算不需要 durable 见证，真实路径上的带根 Failed 因此足以
// 证明「明确失败」；而单次赋值也只能在真实路径上证明。
//
// 「保持 pending」这一支同样只在真实路径上证明：pending 的两条 case 不 await
// 一个可能永不 settle 的完成 cell，只在真实事件屏障之后取一次 durable 快照。

import assert from 'node:assert/strict'
import test from 'node:test'

import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as handleJournal from '../../../dist/Execution/Delegation/Handle/JournalSurface.js'
import {
  agentIdOf,
  childTurn,
  deliverIntoOpenWork,
  executeHostTool,
  forkCall,
  joinCall,
  observeTerminals,
  physicalUserMessageOf,
  withRealFork,
} from '../../delegation/tests/support/real-fork.mjs'

// The provider language is a global preference; the real plugin path boots the
// plugin and refreshes that preference, so this file pins one language.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

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

/**
 * The settled durable view of one work unit.
 *
 * Publishing a terminal on the shared port and committing the work's
 * completion are two different acts: the port carries the observation, the
 * journal carries the settlement. The settlement therefore lands after the
 * terminal is observed, so this waits for the **durable fact** to change
 * (a bounded poll on the journal projection, with a hard deadline) rather
 * than assuming the two are simultaneous. It is a wait for a real durable
 * transition, not a fixed sleep and not a weakened assertion: on expiry the
 * work is still Active and the call fails.
 */
const settleWithin = async (runtime, owner, agentId, child, root, timeoutMs = 10000) => {
  const deadline = Date.now() + timeoutMs
  let record = recordOf(runtime, owner, agentId, child, root)
  while (record?.lifecycle === 'Active' && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 25))
    record = recordOf(runtime, owner, agentId, child, root)
  }
  assert.ok(record, 'the work unit must stay admitted')
  assert.notEqual(
    record.lifecycle,
    'Active',
    `the run must settle within ${timeoutMs}ms of its terminal`,
  )
  return record
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
      // The later delivery and its separate proven Host stop address the
      // SAME admitted work unit under the same physical input and authority
      // root. Together they settle the ORIGINAL work unit — not a fresh
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
  // Bounded repair exhaustion is a terminal failure, not another repair
  // opportunity: the run must settle with an explicit failure. A failed
  // settlement involves no durable delivery witness, so the real fork path
  // proves it honestly — the stop carries the work's own admitted authority
  // root, so it addresses this run and no other.
  const owner = 'effect-accounting-002-repair-exhausted'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'REPAIR-EXHAUSTED-CHARGE', 'call-fork-repair')
    assert.match(first, /Ada/)
    const agentId = agentIdOf(first)
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const root = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      const published = eventsSurface.notifyForAuthority(
        await runtime.terminalPort,
        child,
        'Failed',
        root,
        'INTERACTION_REPAIR_EXHAUSTED',
      )
      assert.equal(published, true, 'the stop must reach the shared terminal port')
      await terminals.awaitRun(child, undefined, { kind: 'Failed' })

      const settled = await settleWithin(runtime, owner, agentId, child, root)
      assert.equal(settled.lifecycle, 'CompletedAwaitingJoin', 'bounded repair exhaustion settles the run')
      assert.equal(settled.lifecycleKind, 'SendFailure')
      assert.ok(settled.completionDigest, 'the failure records its own identity')
      assert.ok(settled.completionRef)

      const joinReceipt = await joinCall(hooks, owner, 'call-join-repair')
      assert.match(joinReceipt, /status = "failed"/)
      assert.match(joinReceipt, /INTERACTION_REPAIR_EXHAUSTED/)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[effect-accounting-002] EXEC_join_real_Failed_still_claims_run', async () => {
  // A proven failure still claims the run: the owner settles it as failed and
  // the parent's join reports that failure — on the real fork path.
  const owner = 'effect-accounting-002-real-failed'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'REAL-FAILED-CHARGE', 'call-fork-real-failed')
    assert.match(first, /Ada/)
    const agentId = agentIdOf(first)
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const root = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      const published = eventsSurface.notifyForAuthority(
        await runtime.terminalPort,
        child,
        'Failed',
        root,
        'provider timeout',
      )
      assert.equal(published, true, 'the stop must reach the shared terminal port')
      await terminals.awaitRun(child, undefined, { kind: 'Failed' })

      const settled = await settleWithin(runtime, owner, agentId, child, root)
      assert.equal(settled.lifecycle, 'CompletedAwaitingJoin', 'a real failure claims the run')
      assert.equal(settled.lifecycleKind, 'SendFailure')
      assert.ok(settled.completionDigest, 'the failure records its own identity')
      assert.ok(settled.completionRef)

      const joinReceipt = await joinCall(hooks, owner, 'call-join-real-failed')
      assert.match(joinReceipt, /status = "failed"/)
      assert.match(joinReceipt, /provider timeout/)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[effect-accounting-002] EXEC_join_first_proven_terminal_is_single_assignment', async () => {
  // Failed-first: a proven failure settles the run and no later terminal may
  // take the cell again — proved on the real fork path, where a settled work
  // unit owns its single assignment.
  const failedOwner = 'effect-accounting-002-failed-first'
  await withRealFork(failedOwner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, failedOwner, 'Ada', 'FAILED-FIRST-CHARGE', 'call-fork-failed-first')
    assert.match(first, /Ada/)
    const agentId = agentIdOf(first)
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const root = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      const published = eventsSurface.notifyForAuthority(
        await runtime.terminalPort,
        child,
        'Failed',
        root,
        'providers exhausted',
      )
      assert.equal(published, true, 'the stop must reach the shared terminal port')
      await terminals.awaitRun(child, undefined, { kind: 'Failed' })

      const settled = await settleWithin(runtime, failedOwner, agentId, child, root)
      assert.equal(settled.lifecycle, 'CompletedAwaitingJoin')
      assert.equal(settled.lifecycleKind, 'SendFailure')
      assert.ok(settled.completionDigest, 'the failure records its own identity')

      // The closed work must refuse an actual late delivery before it can
      // overwrite the first proven terminal.
      const lateRun = `run-${child}-2`
      const refused = await executeHostTool(hooks, runtime, child, 'engineer', root, lateRun,
        'todowrite-for-engineer', {
          program: 'class Js extends JsProgram { async run() { await this.deliver({ 0: "LATE-RECORD with files = 9" }); } }',
        }, `call-${lateRun}`, 'LATE REPORT')
      assert.equal(String(refused), '# failed\n\ncode = "DELIVERY_NOT_ESTABLISHED"\nreason = "delivery was not established: the delivering call has no active task authority"\n')

      const after = recordOf(runtime, failedOwner, agentId, child, root)
      assert.equal(after.lifecycle, 'CompletedAwaitingJoin', 'the first proven terminal keeps the cell')
      assert.equal(after.lifecycleKind, 'SendFailure')
      assert.equal(after.completionDigest, settled.completionDigest)
      assert.equal(after.completionRef, settled.completionRef)

      // And the settled cell published no second terminal for the child.
      const childTerminals = terminals.seen.filter((entry) => entry.sessionId === child)
      assert.equal(childTerminals.length, 1, 'the settled cell publishes no second terminal')
      assert.equal(childTerminals[0].outcome?.kind, 'Failed')

      const joinReceipt = await joinCall(hooks, failedOwner, 'call-join-failed-first')
      assert.match(joinReceipt, /status = "failed"/)
      assert.match(joinReceipt, /providers exhausted/)
      assert.doesNotMatch(joinReceipt, /LATE-RECORD/)
    } finally {
      terminals.dispose()
    }
  })

  // Completed-first: also proved on the real fork path. A settled Completed
  // carries the material that was really delivered, so the owner's real
  // settlement — and the single assignment it owns — is only observable
  // where a durable delivery witness exists.
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

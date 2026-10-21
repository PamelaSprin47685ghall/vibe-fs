// WHAT[delegation-024] 子到父的返回来自 this.deliver 交付格的 view；未交付的子工作不写完成。
// WHAT[delegation-025] completion 以因果身份认领。
//
// D1：被中断的子会话若已经交付了，那次中断不是交付的失败 —— 调用按成功结算，
// 返回的材料就是那份交付视图。
//
// 取证面全部是已注册的公开 Surface：
//   * dist/Execution/Delegation/SyncDelegate/Surface.js —— 真实 SyncDelegate 宿主：
//     invoke 起一次真实的 devops 成员调用，observeTurn 喂进一次真实的终态观察
//     （TurnAborted = 被中断），调用按返回的 view 结算；
//   * dist/Repository/Programming/Js/RuntimeSurface.js —— 进程内的带围栏交付格
//     （this.deliver 交回的那份材料）。
//
// 断言只打在消费者可见事实上：调用是否按成功结算、结算材料的内容是什么、
// 父方看到的是交付视图而不是失败。

import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncDelegate from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as jsRuntime from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const OWNER = 'owner-sync-delegate-abort'
const CHILD_PROSE = 'DEVOPS PROSE'
const DELIVERED_VIEW = 'THE DEVOPS DELIVERY'

// 一次真实的同步 devops 成员调用：真子会话、真提示词、真交付格。
const withSyncDelegateCall = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-delegation-sync-abort-'))
  const harness = await syncDelegate.create(directory, [{ sessionId: OWNER, agent: 'manager' }])
  try {
    return await body(harness, directory)
  } finally {
    syncDelegate.dispose(harness)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[delegation-024] an interrupted devops member that already delivered settles the call with the delivered view, not as a failure', async () => {
  await withSyncDelegateCall(async (harness) => {
    // 真实的同步成员调用：等待子会话的完成。
    const work = syncDelegate.invoke(harness, OWNER, 'DevOps', 'INVESTIGATE THE FLAKY TEST')

    // 子会话的提示词被接受（真实物理认领）。
    await syncDelegate.awaitPromptCount(harness, OWNER, 'DevOps', 1)
    assert.equal(syncDelegate.acceptPrompt(harness, OWNER, 'DevOps', 0), true)

    const child = syncDelegate.child(harness, OWNER, 'DevOps')
    assert.ok(child, 'the devops child session must exist')

    // 子会话交付了：真实写入带围栏的交付格。
    jsRuntime.recordFencedDeliveredView(child, 'devops-run', 'devops-call', {
      prefixUntil: 0,
      thenMap: { 0: DELIVERED_VIEW },
    })
    assert.equal(
      jsRuntime.deliveredFenceOf(child)?.providerRun,
      'devops-run',
      'the delivery cell is fenced by this provider run',
    )

    // 子会话随后被中断：终态是 TurnAborted，而不是完成。
    const observed = await syncDelegate.observeTurn(
      harness,
      OWNER,
      'DevOps',
      'TurnAborted',
      CHILD_PROSE,
      'devops-run',
    )
    assert.equal(observed, true, 'the interrupted turn is observed by the real owner')

    // 消费者可见的事实：调用按成功结算，返回的材料就是那份交付视图。
    const result = await Promise.race([
      work,
      new Promise((resolve) => setTimeout(() => resolve('TIMEOUT'), 5000)),
    ])
    assert.notEqual(result, 'TIMEOUT', 'the interrupted delegate call must settle')
    assert.equal(result.ok, true, 'an interrupted member that delivered settles as a success')

    const view = result.value
    assert.ok(
      view.includes(DELIVERED_VIEW),
      `the settled view must carry the delivered material, got: ${view}`,
    )
    assert.ok(
      !/not established|failed|error/i.test(view),
      `the settled view must not be a failure, got: ${view}`,
    )
  })
})

test('WHAT[delegation-024] an interrupted devops member that delivered nothing leaves the call unsettled: the obligation stays open', async () => {
  await withSyncDelegateCall(async (harness) => {
    const work = syncDelegate.invoke(harness, OWNER, 'DevOps', 'INVESTIGATE THE FLAKY TEST')
    await syncDelegate.awaitPromptCount(harness, OWNER, 'DevOps', 1)
    assert.equal(syncDelegate.acceptPrompt(harness, OWNER, 'DevOps', 0), true)

    const child = syncDelegate.child(harness, OWNER, 'DevOps')
    assert.ok(child, 'the devops child session must exist')

    // 没有交付：交付格是空的。
    assert.equal(jsRuntime.deliveredFenceOf(child), null, 'no delivery was made')

    // 同样的中断观察：没有交付 ⇒ 这次观察不结算任何东西。
    const observed = await syncDelegate.observeTurn(
      harness,
      OWNER,
      'DevOps',
      'TurnAborted',
      CHILD_PROSE,
      'devops-run',
    )
    assert.equal(
      observed,
      false,
      'an interrupted member that delivered nothing settles nothing at all',
    )

    // 消费者可见的事实：调用没有按成功结算，也没有被结算成失败 ——
    // 那次交付仍然欠着，父方还可以继续等或重派。
    const settled = await Promise.race([
      work.then((value) => ({ settled: true, value })),
      new Promise((resolve) => setTimeout(() => resolve({ settled: false }), 1500)),
    ])
    assert.equal(
      settled.settled,
      false,
      'an interrupted member that delivered nothing must not settle the call',
    )
  })
})

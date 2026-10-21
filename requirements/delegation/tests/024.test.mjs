import test from 'node:test'
import assert from 'node:assert/strict'
import {
  childTurn,
  forkCall,
  joinCall,
  joinRecordsOf,
  observeTerminals,
  physicalUserMessageOf,
  resumeCall,
  workLifecycleOf,
  withRealFork,
} from './support/real-fork.mjs'

// The provider language is a global preference; this file mixes the real
// plugin path (which boots the plugin and refreshes that preference) with the
// sync path, so the file pins one language for every member.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

test('WHAT[delegation-024] pre-join resume preserves both public join delivered views', async () => {
  const owner = 'prejoin-retained-delivery'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'FIRST')
    assert.match(first, /Ada/)
    assert.equal(createdIds.length, 1)
    const child = createdIds[0]

    const terminals = await observeTerminals(runtime)
    try {
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'WORK REPORT A', 'DELIVERED-A', terminals.awaitRun)

      const second = await resumeCall(hooks, owner, 'Ada', 'SECOND')
      assert.match(second, /Ada/)
      assert.equal(createdIds.length, 1, 'the same Byname must reuse the same physical child')

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'WORK REPORT B', 'DELIVERED-B', terminals.awaitRun)

      const result = await joinCall(hooks, owner)
      assert.match(result, /DELIVERED-A/)
      assert.match(result, /DELIVERED-B/)
      const records = joinRecordsOf(result)
      // The delivered view of each turn reaches the join records once, as the
      // returned body of that turn (the view names the material twice: as the
      // anchor id and as its text).
      assert.equal((records.match(/text = \\"DELIVERED-A\\"/g) ?? []).length, 1)
      assert.equal((records.match(/text = \\"DELIVERED-B\\"/g) ?? []).length, 1)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] an undelivered Completed terminal does not close the child and join stays not-ready', async () => {
  const owner = 'delivered-and-undelivered-join'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'DELIVER-CHARGE')
    assert.match(first, /Ada/)
    const agentId = /agentId = "([^"]+)"/.exec(first)?.[1]
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const deliveredRoot = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      await childTurn(
        hooks,
        runtime,
        child,
        `run-${child}-1`,
        'WORK REPORT DELIVERED',
        'DELIVERED-RECORD with files = 2',
        terminals.awaitRun,
      )

      const second = await resumeCall(hooks, owner, 'Ada', 'UNDELIVERED-CHARGE')
      assert.match(second, /Ada/)
      const undeliveredRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(undeliveredRoot, deliveredRoot, 'the resumed turn roots a new physical input')

      // A Completed turn without a delivery does not write a completion: the
      // work unit stays Active and no terminal reaches the parent.
      await childTurn(hooks, runtime, child, `run-${child}-2`, 'PLAIN-RETURN-NO-DELIVER', null, () => {})

      assert.equal(workLifecycleOf(runtime, owner, agentId, child, undeliveredRoot), 'Active')

      // The join still reports the delivered turn, and nothing of the
      // undelivered one: the undelivered turn left the child open.
      const joinReceipt = await joinCall(hooks, owner)
      assert.match(joinReceipt, /DELIVERED-RECORD with files = 2/)
      assert.doesNotMatch(joinReceipt, /PLAIN-RETURN-NO-DELIVER/)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join', async () => {
  const owner = 'manager-reuse'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'FIRST-FORK-CHARGE')
      assert.equal(createdIds.length, 1)
      const child = createdIds[0]
      assert.notEqual(child, null)
      assert.match(first, /Ada/)
      const agentId = /agentId = "([^"]+)"/.exec(first)?.[1]
      assert.ok(agentId, 'the fork receipt must name the delegated agent')

      await childTurn(hooks, runtime, child, `run-${child}-1`, 'REPORT ONE', 'FIRST-FORK-ANSWER', terminals.awaitRun)
      const firstRoot = physicalUserMessageOf(runtime, child).id

      const second = await resumeCall(hooks, owner, 'Ada', 'SECOND-FORK-CHARGE')
      assert.match(second, /Ada/)
      assert.equal(createdIds.length, 1, 'same Byname must reuse the same physical child')
      assert.doesNotMatch(second, /SECOND-FORK-ANSWER/)
      assert.doesNotMatch(second, /FIRST-FORK-ANSWER/)

      const secondRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(secondRoot, firstRoot, 'the reused child received a new physical input')
      assert.equal(
        workLifecycleOf(runtime, owner, agentId, child, secondRoot),
        'Active',
        'fork return must not imply that the reused child already completed',
      )

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'REPORT TWO', 'SECOND-FORK-ANSWER', terminals.awaitRun)
      const thirdRoot = physicalUserMessageOf(runtime, child).id
      assert.equal(
        workLifecycleOf(runtime, owner, agentId, child, thirdRoot),
        'CompletedAwaitingJoin',
        'completion remains a separately consumable join fact',
      )
    } finally {
      terminals.dispose()
    }
  })
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");
const runtimeSurface = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");

const live = async (owner) => sync.create(
  await mkdtemp(join(tmpdir(), 'wxs-sync-delegate-')),
  [{ sessionId: owner, agent: 'manager' }],
)
const waitForChild = async (h, owner, role) => {
  await sync.awaitPromptCount(h, owner, role, 1)
  return sync.child(h, owner, role)
}
const waitForPromptCount = async (h, owner, role, count) => {
  await sync.awaitPromptCount(h, owner, role, count)
  assert.equal(sync.acceptPrompt(h, owner, role, count - 1), true)
}
const settle = async (h, owner, role, answer, run = 'run-1') => {
  runtimeSurface.recordFencedDeliveredView(sync.child(h, owner, role), run, `call-${run}`, {
    prefixUntil: 0,
    thenMap: {},
  })
  return sync.settle(h, owner, role, answer, run)
}
const remainsPending = async (promise) =>
  Promise.race([
    promise.then((value) => ({ kind: 'resolved', value })),
    new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
  ])
const verifyReusableHandoff = async (role) => {
  const owner = `owner-handoff-${role.toLowerCase()}`
  const h = await live(owner)
  try {
    const first = sync.invoke(h, owner, role, 'FIRST-CHARGE')
    await waitForPromptCount(h, owner, role, 1)
    assert.match(sync.prompt(h, owner, role, 0), /FIRST-CHARGE/)
    assert.match(
      sync.prompt(h, owner, role, 0),
      /closing report/,
      'the charge prompt must carry the shared fork-child Base guidance (delegation-019)',
    )

    assert.equal(await settle(h, owner, role, 'FIRST-ANSWER', 'run-first'), true)
    const firstResult = await first
    assert.equal(firstResult.ok, true)
    assert.match(firstResult.value, /FIRST-ANSWER/)

    const second = sync.invoke(h, owner, role, 'SECOND-CHARGE')
    await waitForPromptCount(h, owner, role, 2)
    const secondPrompt = sync.prompt(h, owner, role, 1)
    assert.match(secondPrompt, /SECOND-CHARGE/)

    assert.equal(
      await sync.settleWithAuthorityRoot(h, owner, role, 'STALE-ANSWER', 'run-stale', 'old-authority-root'),
      false,
    )
    assert.deepEqual(await remainsPending(second), { kind: 'pending' })

    assert.equal(await settle(h, owner, role, 'SECOND-ANSWER', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /SECOND-ANSWER/)
    assert.doesNotMatch(secondResult.value, /FIRST-ANSWER/)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
}

test('WHAT[delegation-024] Engineer reuse sends parent delta waits for its own root and returns its own child delta', async () => {
  await verifyReusableHandoff('Engineer')
})

test('WHAT[delegation-024] DevOps completion is claimed by HandleTurn and its delivered view returns to the caller', async () => {
  await verifyReusableHandoff('DevOps')
})
}

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
// (Moved from tests/033.test.mjs: these cases own clause [024], and the
// requirement tree keeps one test file per clause.)
{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const syncDelegate = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");
const jsRuntime = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");

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
}

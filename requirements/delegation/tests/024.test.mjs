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

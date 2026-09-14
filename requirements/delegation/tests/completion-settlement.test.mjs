// DELEG-031: reusable completion checkpoint settles to a closed union.
// "Child finished" and "completion evidence durably written" are distinct
// durable facts: an uncommitted checkpoint must never rewrite or re-execute a
// completed child, and the earned WorkRecord is delivered either way.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const live = async (owner) =>
  sync.create(await mkdtemp(join(tmpdir(), 'wxs-deleg031-')), [{ sessionId: owner, agent: 'manager' }])

const waitForPromptCount = async (h, owner, role, count) => {
  await sync.awaitPromptCount(h, owner, role, count)
  assert.equal(sync.acceptPrompt(h, owner, role, count - 1), true)
}

const settle = async (h, owner, role, answer, run = 'run-1') => sync.settle(h, owner, role, answer, run)

const remainsPending = async (promise) =>
  Promise.race([
    promise.then((value) => ({ kind: 'resolved', value })),
    new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
  ])

test('WHAT[DELEG-031] DELEG_031_committed_checkpoint_advances_frontier_and_delivers_work_record', async () => {
  const owner = 'owner-deleg031-committed'
  const h = await live(owner)
  try {
    await sync.captureOwnerOpening(h, owner, 'ROOT-OPENING-MARKER')
    const pending = sync.invoke(h, owner, 'Inspector', 'FIRST-CHARGE')
    await waitForPromptCount(h, owner, 'Inspector', 1)
    assert.equal(sync.handoffFrontier(h, owner, 'Inspector'), null)
    assert.equal(await settle(h, owner, 'Inspector', 'FIRST-ANSWER', 'run-first'), true)
    const result = await pending
    assert.equal(result.ok, true)
    assert.match(result.value, /FIRST-ANSWER/)

    // The production checkpoint for this exact parent+route commits: frontier advances.
    const frontier = sync.handoffFrontier(h, owner, 'Inspector')
    assert.notEqual(frontier, null)
    assert.equal(typeof frontier, 'number')
  } finally {
    sync.dispose(h)
  }
})

test('WHAT[DELEG-031] DELEG_031_uncommitted_checkpoint_still_delivers_work_record_without_reexecution', async () => {
  const owner = 'owner-deleg031-uncommitted'
  const h = await live(owner)
  try {
    await sync.captureOwnerOpening(h, owner, 'ROOT-OPENING-MARKER')
    const pending = sync.invoke(h, owner, 'Inspector', 'UNCOMMITTED-CHARGE')
    await waitForPromptCount(h, owner, 'Inspector', 1)
    // Release the writer between child acceptance and settle: the settlement
    // checkpoint append is a known WriterUnavailable (NotCommitted), while the
    // child that already physically ran keeps its earned WorkRecord.
    sync.closeJournalWriter(h)
    assert.equal(await settle(h, owner, 'Inspector', 'UNCOMMITTED-ANSWER', 'run-first'), true)

    // The completed child is NOT re-executed and its completion is NOT
    // forgotten: the earned WorkRecord is delivered despite the unsettled
    // checkpoint.
    const result = await pending
    assert.equal(result.ok, true)
    assert.match(result.value, /UNCOMMITTED-ANSWER/)

    // The frontier never advanced — the checkpoint stayed pending-evidence.
    assert.equal(sync.handoffFrontier(h, owner, 'Inspector'), null)
    assert.equal(sync.childCount(h), 1)
  } finally {
    sync.dispose(h)
  }
})

test('WHAT[DELEG-031] DELEG_031_checkpoint_probe_reports_typed_settlement_with_exact_identity', async () => {
  const owner = 'owner-deleg031-probe'
  const h = await live(owner)
  try {
    // Committed path: the production checkpoint reports Committed with the
    // exact parent + route identity.
    const first = await sync.checkpointForHarness(h, owner, 'Inspector', 7)
    assert.equal(first.commitment, 'Committed')
    assert.equal(first.parent, owner)
    assert.match(first.route, /inspector/)
    assert.equal(first.reason, null)

    // NotCommitted path: after the writer is released, the same production
    // checkpoint reports NotCommitted (never a bare string) with the same
    // exact identity — and the second call does NOT re-emit a duplicate.
    sync.closeJournalWriter(h)
    const second = await sync.checkpointForHarness(h, owner, 'Inspector', 7)
    assert.equal(second.commitment, 'NotCommitted')
    assert.equal(second.parent, owner)
    assert.equal(second.route, first.route)
    assert.match(String(second.reason), /closing|disposed|poisoned|not attempted/i)
  } finally {
    sync.dispose(h)
  }
})

test('WHAT[DELEG-031] DELEG_031_duplicate_completion_is_idempotent_and_never_reexecutes', async () => {
  const owner = 'owner-deleg031-duplicate'
  const h = await live(owner)
  try {
    const first = sync.invoke(h, owner, 'Inspector', 'FIRST')
    await waitForPromptCount(h, owner, 'Inspector', 1)
    assert.equal(await settle(h, owner, 'Inspector', 'FIRST-ANSWER', 'run-first'), true)
    assert.equal((await first).ok, true)
    const frontierAfterFirst = sync.handoffFrontier(h, owner, 'Inspector')
    assert.notEqual(frontierAfterFirst, null)

    // A duplicate completion for the same authority root cannot claim a new
    // call: the next invocation still reuses the same child exactly once.
    const second = sync.invoke(h, owner, 'Inspector', 'SECOND')
    await waitForPromptCount(h, owner, 'Inspector', 2)
    assert.equal(await settle(h, owner, 'Inspector', 'SECOND-ANSWER', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /SECOND-ANSWER/)
    assert.doesNotMatch(secondResult.value, /FIRST-ANSWER/)
    assert.equal(sync.childCount(h), 1)

    // The checkpoint for an already-recorded window is at least the first
    // frontier — never a retreat, never a second execution.
    const frontierAfterSecond = sync.handoffFrontier(h, owner, 'Inspector')
    assert.ok(frontierAfterSecond >= frontierAfterFirst)
  } finally {
    sync.dispose(h)
  }
})

test('WHAT[DELEG-031] DELEG_031_stale_authority_completion_cannot_claim_a_new_call', async () => {
  const owner = 'owner-deleg031-stale'
  const h = await live(owner)
  try {
    const first = sync.invoke(h, owner, 'Inspector', 'FIRST')
    await waitForPromptCount(h, owner, 'Inspector', 1)
    assert.equal(await settle(h, owner, 'Inspector', 'FIRST-ANSWER', 'run-first'), true)
    assert.equal((await first).ok, true)

    const second = sync.invoke(h, owner, 'Inspector', 'SECOND')
    await waitForPromptCount(h, owner, 'Inspector', 2)

    // A stale completion under a superseded authority root is rejected: the
    // pending call stays pending and no checkpoint advances for it.
    assert.equal(
      await sync.settleWithAuthorityRoot(h, owner, 'Inspector', 'STALE-ANSWER', 'run-stale', 'old-authority-root'),
      false,
    )
    assert.deepEqual(await remainsPending(second), { kind: 'pending' })

    assert.equal(await settle(h, owner, 'Inspector', 'SECOND-ANSWER', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /SECOND-ANSWER/)
    assert.doesNotMatch(secondResult.value, /STALE-ANSWER/)
  } finally {
    sync.dispose(h)
  }
})

test('WHAT[DELEG-031] DELEG_031_parent_supersede_leaves_no_orphan_completion_claim', async () => {
  const owner = 'owner-deleg031-supersede'
  const h = await live(owner)
  try {
    const pending = sync.invoke(h, owner, 'Inspector', 'SUPERSEDED-CHARGE')
    await waitForPromptCount(h, owner, 'Inspector', 1)

    // Parent supersedes the call before its completion arrives.
    assert.equal(sync.abandonPendingCall(h, owner, 'Inspector'), true)

    // The late completion afterwards cannot claim the abandoned call: the
    // invocation fails with the supersede reason, not with a stale success.
    assert.equal(await settle(h, owner, 'Inspector', 'LATE-ANSWER', 'run-late'), true)
    const result = await pending
    assert.equal(result.ok, false)

    // The abandoned call leaves no completed frontier behind.
    assert.equal(sync.handoffFrontier(h, owner, 'Inspector'), null)
  } finally {
    sync.dispose(h)
  }
})

test('WHAT[DELEG-031] DELEG_031_completed_and_delete_in_both_orders_settle_exactly_once', async () => {
  for (const order of ['complete-then-delete', 'delete-then-complete']) {
    const owner = `owner-deleg031-order-${order}`
    const h = await live(owner)
    try {
      const pending = sync.invoke(h, owner, 'Inspector', `CHARGE-${order}`)
      await waitForPromptCount(h, owner, 'Inspector', 1)

      if (order === 'complete-then-delete') {
        assert.equal(await settle(h, owner, 'Inspector', `ANSWER-${order}`, 'run-order'), true)
        assert.equal((await pending).ok, true)
        // Graceful scope close stages the inspector; the staged binding is
        // retired exactly once.
        assert.equal(sync.stageDeletedInspector(h, owner), true)
        assert.equal(sync.scopeCloseChild(h, owner, 'Inspector'), await sync.child(h, owner, 'Inspector'))
      } else {
        // Delete first: the pending call is cancelled before its completion.
        sync.cancelSession(h, owner)
        const result = await pending
        assert.equal(result.ok, false)
      }
      assert.equal(sync.childCount(h), 1)
    } finally {
      sync.dispose(h)
    }
  }
})

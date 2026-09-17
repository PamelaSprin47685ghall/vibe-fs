import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

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

const settle = async (h, owner, role, answer, run = 'run-1') => sync.settle(h, owner, role, answer, run)

const remainsPending = async (promise) =>
  Promise.race([
    promise.then((value) => ({ kind: 'resolved', value })),
    new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
  ])

const verifyReusableHandoff = async (role) => {
  const owner = `owner-handoff-${role.toLowerCase()}`
  const h = await live(owner)
  try {
    await sync.captureOwnerOpening(h, owner, 'ROOT-OPENING-MARKER')

    const first = sync.invoke(h, owner, role, 'FIRST-CHARGE')
    await waitForPromptCount(h, owner, role, 1)
    assert.equal(sync.handoffFrontier(h, owner, role), null)
    assert.match(sync.prompt(h, owner, role, 0), /ROOT-OPENING-MARKER/)

    assert.equal(await settle(h, owner, role, 'FIRST-ANSWER', 'run-first'), true)
    const firstResult = await first
    assert.equal(firstResult.ok, true)
    assert.match(firstResult.value, /FIRST-ANSWER/)
    const firstFrontier = sync.handoffFrontier(h, owner, role)
    assert.notEqual(firstFrontier, null)

    await sync.captureOwnerDeltaPart(h, owner, 'PARENT-DELTA-ONLY-MARKER', 'parent-run-2')

    const second = sync.invoke(h, owner, role, 'SECOND-CHARGE')
    await waitForPromptCount(h, owner, role, 2)
    const secondPrompt = sync.prompt(h, owner, role, 1)
    assert.match(secondPrompt, /SECOND-CHARGE/)
    assert.match(secondPrompt, /parent_delta_work_record\s*=/)
    assert.match(secondPrompt, /PARENT-DELTA-ONLY-MARKER/)
    assert.doesNotMatch(secondPrompt, /ROOT-OPENING-MARKER/)
    assert.equal(sync.handoffFrontier(h, owner, role), firstFrontier)

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
    assert.notEqual(sync.handoffFrontier(h, owner, role), firstFrontier)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
}

test('WHAT[DELEG-023] SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted', async () => {
  const h = await live('owner-retry')
  try {
    const pending = sync.invoke(h, 'owner-retry', 'Engineer', 'retry charge')
    await waitForChild(h, 'owner-retry', 'Engineer')
    await waitForPromptCount(h, 'owner-retry', 'Engineer', 1)
    // The retry decorator admits a fresh attempt: the failure stays child-local
    // and the caller keeps waiting on the same invocation.
    sync.scriptRetry(h, ['dispatched'])
    assert.equal(await sync.observeTurn(h, 'owner-retry', 'Engineer', 'TurnFailed', '', 'run-retry-1'), true)
    assert.equal(sync.retryCalls(h), 1, 'the transient failure must ask the shared retry decorator')
    assert.equal(sync.dispatchRetryAttempt(h, 'owner-retry', 'Engineer'), true)
    assert.equal(await sync.observeTurn(h, 'owner-retry', 'Engineer', 'TurnCompleted', 'retry WorkRecord', 'run-retry-2'), true)
    const result = await pending
    assert.equal(result.ok, true)
    assert.match(result.value, /retry WorkRecord/)
  } finally { sync.dispose(h) }

  // Only the decorator's terminal verdict reaches the caller; a later observation
  // of the settled invocation is an idempotent no-op.
  const exhausted = await live('owner-exhausted')
  try {
    sync.scriptRetry(exhausted, ['terminal:provider retry budget exhausted'])
    const pending = sync.invoke(exhausted, 'owner-exhausted', 'Engineer', 'exhausted charge')
    await waitForChild(exhausted, 'owner-exhausted', 'Engineer')
    await waitForPromptCount(exhausted, 'owner-exhausted', 'Engineer', 1)
    assert.equal(await sync.observeTurn(exhausted, 'owner-exhausted', 'Engineer', 'TurnFailed', '', 'run-exhausted-1'), true)
    assert.deepEqual(await pending, { ok: false, error: 'SyncDelegate run failed: provider retry budget exhausted' })
    assert.equal(sync.retryCalls(exhausted), 1)
  } finally { sync.dispose(exhausted) }
})

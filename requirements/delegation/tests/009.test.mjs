import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

const OWNER = 'owner-ce'
const descriptor = [{ sessionId: OWNER, agent: 'manager' }]

test('WHAT[DELEG-009] SYNC_SERIALIZATION_second_active_call_is_rejected', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-')), descriptor)
  try {
    const first = sync.invoke(h, 'owner-ce', 'Engineer', 'first arrival')
    await sync.awaitPromptCount(h, 'owner-ce', 'Engineer', 1)
    assert.equal(sync.acceptPrompt(h, 'owner-ce', 'Engineer', 0), true)
    const second = sync.invoke(h, 'owner-ce', 'Engineer', 'second arrival')
    assert.deepEqual(await second, {
      ok: false,
      error: 'sync delegate rejected: dedicated delegate already has an active batch',
    })
    assert.equal(await sync.settle(h, 'owner-ce', 'Engineer', 'first answer', 'run-ce'), true)
    assert.equal((await first).ok, true)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

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

test('WHAT[DELEG-009] SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent', () => {
  const blocked = sync.serializationDecision('owner-a', 'owner-a', false)
  assert.equal(blocked.accepted, false)
  assert.match(blocked.reason, /same ReuseScope/)
  const sameRun = sync.serializationDecision('owner-a', 'owner-a', true)
  assert.equal(sameRun.accepted, true)
  const independent = sync.serializationDecision('owner-a', 'owner-b', false)
  assert.equal(independent.accepted, true)
})
}

import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");


test('WHAT[DELEG-025] reusable fork terminal failure is guarded by the accepted authority root', () => {
  const lifecycle = readFileSync(
    new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs', import.meta.url),
    'utf8',
  )

  assert.match(lifecycle, /TerminalStop\.belongsTo root stop/)
  assert.match(lifecycle, /Failed stop when not \(stopBelongsToRun run stop\) -> Task\.FromResult\(\(\)\)/)
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

test('WHAT[DELEG-025] SYNC_RUNTIME_late_failure_from_previous_authority_root_cannot_fail_reused_call', async () => {
  const h = await live('owner-failure-causality')
  try {
    const first = sync.invoke(h, 'owner-failure-causality', 'Engineer', 'FIRST')
    await waitForPromptCount(h, 'owner-failure-causality', 'Engineer', 1)
    assert.equal(await settle(h, 'owner-failure-causality', 'Engineer', 'FIRST-ANSWER', 'run-first'), true)
    assert.equal((await first).ok, true)

    const second = sync.invoke(h, 'owner-failure-causality', 'Engineer', 'SECOND')
    await waitForPromptCount(h, 'owner-failure-causality', 'Engineer', 2)

    assert.equal(
      await sync.failWithAuthorityRoot(
        h,
        'owner-failure-causality',
        'Engineer',
        'late previous failure',
        'msg-physical-1',
      ),
      'Ignored',
    )
    assert.deepEqual(await remainsPending(second), { kind: 'pending' })

    assert.equal(
      await sync.failWithAuthorityRoot(
        h,
        'owner-failure-causality',
        'Engineer',
        'coarse current-root failure',
        'msg-physical-1',
      ),
      'Ignored',
    )
    assert.deepEqual(await remainsPending(second), { kind: 'pending' })

    assert.equal(
      await sync.observeTurn(
        h,
        'owner-failure-causality',
        'Engineer',
        'TurnFailed',
        'current failure',
        'run-second',
      ),
      true,
    )
    assert.deepEqual(await second, { ok: false, error: 'SyncDelegate run failed: current failure' })
  } finally { sync.dispose(h) }
})
}

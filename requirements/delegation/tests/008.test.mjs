import test from 'node:test'

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

test('WHAT[DELEG-008] SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order', () => {
  const batch = sync.batchOrder('Engineer', ['engineer', 'establish-behavior', 'engineer'], 'engineer')
  assert.deepEqual(batch.order, ['engineer', 'engineer'])
  assert.equal(batch.currentPresent, true)
  const coder = sync.batchOrder('Coder', ['inspect', 'establish-behavior', 'repair-behavior'], 'repair-behavior')
  assert.deepEqual(coder.order, ['establish-behavior', 'repair-behavior'])
  assert.equal(coder.currentPresent, true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

const surface = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs', import.meta.url), 'utf8')

test('WHAT[DELEG-008] DEFERRED_ENGINEER_stage_and_replace_tool_results_in_messages', () => {
  const placeholder = sync.stageDeferredInspection('session-test-deferred', 'call-deferred-1', 'check auth integrity', 'auth', 1)
  assert.match(placeholder, /Engineer charge accepted and deferred for batch execution/)
  assert.match(placeholder, /check auth integrity/)

  // Messages with part.type === "tool"
  const msgWithPart = {
    info: { role: 'assistant' },
    parts: [
      {
        type: 'tool',
        callID: 'call-deferred-1',
        state: { status: 'completed', output: placeholder },
      },
    ],
  }
  // Messages with role === "tool"
  const msgWithRole = {
    role: 'tool',
    tool_call_id: 'call-deferred-1',
    content: placeholder,
  }

  const list = [msgWithPart, msgWithRole]
  sync.applyReplacedResults(list)

  // Prior to settlement, durable map does not replace
  assert.equal(msgWithPart.parts[0].state.output, placeholder)
  assert.equal(msgWithRole.content, placeholder)
})
}

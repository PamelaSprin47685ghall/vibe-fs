// SyncDelegate runtime behavior through the opaque delegation owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const live = async (owner) => sync.create(
  await mkdtemp(join(tmpdir(), 'wxs-sync-delegate-')),
  [{ sessionId: owner, agent: 'fast-manager' }],
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

for (const role of ['Inspector', 'Coder']) {
  test(`WHAT[DELEG-021] SYNC_RUNTIME_${role.toLowerCase()}_invoke_admits_one_managed_child_and_settles_answer`, async () => {
    const owner = `owner-sync-${role.toLowerCase()}`
    const h = await live(owner)
    try {
      const pending = sync.invoke(h, owner, role, `${role} question`)
      await waitForPromptCount(h, owner, role, 1)
      const child = sync.child(h, owner, role)
      assert.ok(child)
      assert.equal(sync.childCount(h), 1)
      assert.equal(await settle(h, owner, role, `${role} answer`), true)
      const result = await pending
      assert.equal(result.ok, true)
      assert.match(result.value, /Recent work/)
      assert.match(result.value, new RegExp(`${role} answer`))
    } finally { sync.dispose(h) }
  })
}

test('WHAT[DELEG-025] SYNC_RUNTIME_late_failure_from_previous_authority_root_cannot_fail_reused_call', async () => {
  const h = await live('owner-failure-causality')
  try {
    const first = sync.invoke(h, 'owner-failure-causality', 'Inspector', 'FIRST')
    await waitForPromptCount(h, 'owner-failure-causality', 'Inspector', 1)
    assert.equal(await settle(h, 'owner-failure-causality', 'Inspector', 'FIRST-ANSWER', 'run-first'), true)
    assert.equal((await first).ok, true)

    const second = sync.invoke(h, 'owner-failure-causality', 'Inspector', 'SECOND')
    await waitForPromptCount(h, 'owner-failure-causality', 'Inspector', 2)

    assert.equal(
      await sync.failWithAuthorityRoot(
        h,
        'owner-failure-causality',
        'Inspector',
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
        'Inspector',
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
        'Inspector',
        'TurnFailed',
        'current failure',
        'run-second',
      ),
      true,
    )
    assert.deepEqual(await second, { ok: false, error: 'SyncDelegate run failed: current failure' })
  } finally { sync.dispose(h) }
})

test('WHAT[DELEG-010] SYNC_RUNTIME_same_role_reuses_one_child_after_completion', async () => {
  const h = await live('owner-sync')
  try {
    const first = sync.invoke(h, 'owner-sync', 'Inspector', 'first')
    const firstChild = await waitForChild(h, 'owner-sync', 'Inspector')
    await waitForPromptCount(h, 'owner-sync', 'Inspector', 1)
    assert.equal(await settle(h, 'owner-sync', 'Inspector', 'first answer', 'run-first'), true)
    const firstResult = await first
    assert.equal(firstResult.ok, true)
    assert.match(firstResult.value, /Recent work/)
    assert.match(firstResult.value, /first answer/)

    const second = sync.invoke(h, 'owner-sync', 'Inspector', 'second')
    assert.equal(await waitForChild(h, 'owner-sync', 'Inspector'), firstChild)
    const immediate = await Promise.race([
      second.then((value) => ({ kind: 'resolved', value })),
      new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
    ])
    assert.deepEqual(immediate, { kind: 'pending' }, 'fresh reuse must remain pending until its own completion')
    await waitForPromptCount(h, 'owner-sync', 'Inspector', 2)
    assert.equal(await settle(h, 'owner-sync', 'Inspector', 'second answer', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /Recent work/)
    assert.match(secondResult.value, /second answer/)
    assert.doesNotMatch(secondResult.value, /first answer/)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})

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

test('WHAT[DELEG-024] SYNC_RUNTIME_inspector_reuse_sends_parent_delta_waits_for_own_root_and_returns_own_child_delta', async () => {
  await verifyReusableHandoff('Inspector')
})

test('WHAT[DELEG-024] SYNC_RUNTIME_coder_reuse_sends_parent_delta_waits_for_own_root_and_returns_own_child_delta', async () => {
  await verifyReusableHandoff('Coder')
})


test('WHAT[DELEG-008] SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order', () => {
  const batch = sync.batchOrder('Inspector', ['inspect', 'establish-behavior', 'inspect'], 'inspect')
  assert.deepEqual(batch.order, ['inspect', 'inspect'])
  assert.equal(batch.currentPresent, true)
  const coder = sync.batchOrder('Coder', ['inspect', 'establish-behavior', 'repair-behavior'], 'repair-behavior')
  assert.deepEqual(coder.order, ['establish-behavior', 'repair-behavior'])
  assert.equal(coder.currentPresent, true)
})

test('WHAT[DELEG-021] SYNC_RUNTIME_unknown_role_and_outcome_fail_closed_at_every_entry', async () => {
  const h = await live('owner-invalid')
  try {
    assert.deepEqual(await sync.invoke(h, 'owner-invalid', 'Mystery', 'charge'), { ok: false, error: 'unknown role: Mystery' })
    assert.deepEqual(await sync.invoke(h, 'owner-invalid', '', 'charge'), { ok: false, error: 'role is required' })
    assert.equal(await sync.settle(h, 'owner-invalid', 'Mystery', 'answer'), false)
    assert.equal(await sync.observeTurn(h, 'owner-invalid', 'Inspector', 'UnexpectedOutcome', '', 'run-invalid'), false)
    assert.equal(sync.child(h, 'owner-invalid', 'Mystery'), null)
    assert.deepEqual(sync.vocabulary('Mystery', 'Fast', 'scope'), { ok: false, error: 'unknown role: Mystery' })
    assert.deepEqual(sync.batchOrder('Mystery', ['inspect'], 'inspect'), { ok: false, error: 'unknown role: Mystery' })
    assert.deepEqual(
      await sync.invokeBatch(h, 'owner-invalid', 'Mystery', 'charge', 'run-invalid', 'call-invalid', ['call-invalid']),
      { kind: 'Error', error: 'unknown role: Mystery' },
    )
  } finally { sync.dispose(h) }
})

test('WHAT[DELEG-009] SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent', () => {
  const blocked = sync.serializationDecision('owner-a', 'owner-a', false)
  assert.equal(blocked.accepted, false)
  assert.match(blocked.reason, /same ReuseScope/)
  const sameRun = sync.serializationDecision('owner-a', 'owner-a', true)
  assert.equal(sameRun.accepted, true)
  const independent = sync.serializationDecision('owner-a', 'owner-b', false)
  assert.equal(independent.accepted, true)
})

test('WHAT[DELEG-011] SYNC_RUNTIME_ordinary_completion_settles_batch_without_return_channel', async () => {
  const h = await live('owner-ordinary')
  try {
    const pending = sync.invoke(h, 'owner-ordinary', 'Inspector', 'ordinary charge')
    await waitForChild(h, 'owner-ordinary', 'Inspector')
    await waitForPromptCount(h, 'owner-ordinary', 'Inspector', 1)
    assert.equal(await settle(h, 'owner-ordinary', 'Inspector', 'ordinary WorkRecord', 'run-ordinary'), true)
    const result = await pending
    assert.equal(result.ok, true)
    assert.match(result.value, /ordinary WorkRecord/)
    assert.equal(typeof sync.return, 'undefined')
  } finally { sync.dispose(h) }
})

test('WHAT[DELEG-012] SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference', async () => {
  const h = await live('owner-canonical')
  try {
    const first = sync.invokeBatch(h, 'owner-canonical', 'Inspector', 'first charge', 'run-canonical', 'call-first', ['call-first', 'call-second'])
    const second = sync.invokeBatch(h, 'owner-canonical', 'Inspector', 'second charge', 'run-canonical', 'call-second', ['call-first', 'call-second'])
    await waitForChild(h, 'owner-canonical', 'Inspector')
    await waitForPromptCount(h, 'owner-canonical', 'Inspector', 1)
    assert.equal(await settle(h, 'owner-canonical', 'Inspector', 'canonical WorkRecord', 'run-canonical'), true)
    const firstResult = await first
    const secondResult = await second
    assert.equal(firstResult.kind, 'WorkRecord')
    assert.match(firstResult.value, /Recent work/)
    assert.match(firstResult.value, /canonical WorkRecord/)
    assert.deepEqual(secondResult, { kind: 'MergedInto', canonical: 'call-first' })
  } finally { sync.dispose(h) }
})

test('WHAT[DELEG-017] SYNC_RUNTIME_work_record_is_evidence_and_does_not_transfer_authority', () => {
  const result = sync.evidenceBoundary('inspect this file', 'found one bounded fact')
  assert.equal(result.charge, 'inspect this file')
  assert.equal(result.workRecord, 'found one bounded fact')
  assert.equal(result.authorityTransferred, false)
})

test('WHAT[DELEG-023] SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted', async () => {
  const h = await live('owner-retry')
  try {
    const pending = sync.invoke(h, 'owner-retry', 'Inspector', 'retry charge')
    await waitForChild(h, 'owner-retry', 'Inspector')
    await waitForPromptCount(h, 'owner-retry', 'Inspector', 1)
    assert.equal(await sync.observeTurn(h, 'owner-retry', 'Inspector', 'TurnFailed', '', 'run-retry-1'), false)
    assert.equal(await sync.observeTurn(h, 'owner-retry', 'Inspector', 'TurnCompleted', 'retry WorkRecord', 'run-retry-2'), true)
    const result = await pending
    assert.equal(result.ok, true)
    assert.match(result.value, /retry WorkRecord/)
  } finally { sync.dispose(h) }

  const retry = sync.retryDisposition(['TurnFailed', 'RetryAvailable'])
  assert.equal(retry.result, 'ChildLocalRetry')
  assert.equal(retry.callerFailure, false)
  const exhausted = sync.retryDisposition(['TurnFailed', 'TurnFailed'])
  assert.equal(exhausted.result, 'ExhaustedFailure')
  assert.equal(exhausted.callerFailure, true)
  const completed = sync.retryDisposition(['TurnFailed', 'RetryAvailable', 'Completed'])
  assert.equal(completed.result, 'WorkRecord')
  assert.equal(completed.callerFailure, false)
})

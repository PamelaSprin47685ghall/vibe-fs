// SyncDelegate runtime behavior through the opaque delegation owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtemp } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const live = async () => sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-delegate-')))
const waitForChild = async (h, owner, role) => {
  for (let attempt = 0; attempt < 1000; attempt += 1) {
    const child = sync.child(h, owner, role)
    if (child) return child
    await new Promise((resolve) => setImmediate(resolve))
  }
  throw new Error(`delegate child was not admitted for ${owner}/${role}`)
}
const waitForPromptCount = async (h, owner, role, count) => {
  for (let attempt = 0; attempt < 1000; attempt += 1) {
    if (sync.promptCount(h, owner, role) >= count) return
    await new Promise((resolve) => setImmediate(resolve))
  }
  throw new Error(`delegate prompt ${count} was not admitted for ${owner}/${role}`)
}
const settle = async (h, owner, role, answer, run = 'run-1') => sync.settle(h, owner, role, answer, run)

for (const role of ['Inspector', 'Coder']) {
  test(`WHAT[DELEG-021] SYNC_RUNTIME_${role.toLowerCase()}_invoke_admits_one_managed_child_and_settles_answer`, async () => {
    const h = await live()
    try {
      const pending = sync.invoke(h, 'owner-sync', role, `${role} question`)
      const child = await new Promise((resolve) => {
        const tick = () => {
          const id = sync.child(h, 'owner-sync', role)
          if (id) resolve(id); else setImmediate(tick)
        }
        tick()
      })
      assert.ok(child)
      assert.equal(sync.childCount(h), 1)
      assert.equal(await settle(h, 'owner-sync', role, `${role} answer`), true)
      const result = await pending
      assert.equal(result.ok, true)
      assert.match(result.value, /Recent work/)
      assert.match(result.value, new RegExp(`${role} answer`))
    } finally { sync.dispose(h) }
  })
}

test('WHAT[DELEG-010] SYNC_RUNTIME_same_role_reuses_one_child_after_completion', async () => {
  const h = await live()
  try {
    const first = sync.invoke(h, 'owner-sync', 'Inspector', 'first')
    const firstChild = await waitForChild(h, 'owner-sync', 'Inspector')
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


test('WHAT[DELEG-008] SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order', () => {
  const batch = sync.batchOrder('Inspector', ['inspect', 'establish-behavior', 'inspect'], 'inspect')
  assert.deepEqual(batch.order, ['inspect', 'inspect'])
  assert.equal(batch.currentPresent, true)
  const coder = sync.batchOrder('Coder', ['inspect', 'establish-behavior', 'repair-behavior'], 'repair-behavior')
  assert.deepEqual(coder.order, ['establish-behavior', 'repair-behavior'])
  assert.equal(coder.currentPresent, true)
})

test('WHAT[DELEG-021] SYNC_RUNTIME_unknown_role_and_outcome_fail_closed_at_every_entry', async () => {
  const h = await live()
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
  const h = await live()
  try {
    const pending = sync.invoke(h, 'owner-ordinary', 'Inspector', 'ordinary charge')
    await waitForChild(h, 'owner-ordinary', 'Inspector')
    assert.equal(await settle(h, 'owner-ordinary', 'Inspector', 'ordinary WorkRecord', 'run-ordinary'), true)
    const result = await pending
    assert.equal(result.ok, true)
    assert.match(result.value, /ordinary WorkRecord/)
    assert.equal(typeof sync.return, 'undefined')
  } finally { sync.dispose(h) }
})

test('WHAT[DELEG-012] SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference', async () => {
  const h = await live()
  try {
    const first = sync.invokeBatch(h, 'owner-canonical', 'Inspector', 'first charge', 'run-canonical', 'call-first', ['call-first', 'call-second'])
    const second = sync.invokeBatch(h, 'owner-canonical', 'Inspector', 'second charge', 'run-canonical', 'call-second', ['call-first', 'call-second'])
    await waitForChild(h, 'owner-canonical', 'Inspector')
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
  const h = await live()
  try {
    const pending = sync.invoke(h, 'owner-retry', 'Inspector', 'retry charge')
    await waitForChild(h, 'owner-retry', 'Inspector')
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


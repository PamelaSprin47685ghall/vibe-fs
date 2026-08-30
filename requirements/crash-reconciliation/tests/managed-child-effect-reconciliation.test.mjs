import assert from 'node:assert/strict'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as AttachmentSurface from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'
import * as SyncDelegateSurface from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[MANAGED-SESSION-001] managed_child_effect_reconciliation_classifies_missing_matching_and_conflicting_evidence', () => {
  assert.deepEqual(AttachmentSurface.classifyObservation('missing'), {
    observation: 'missing',
    decision: 'Create',
    children: [],
  })
  assert.deepEqual(AttachmentSurface.classifyObservation('matching'), {
    observation: 'matching',
    decision: 'Adopt',
    children: ['host-child-existing'],
  })
  assert.deepEqual(AttachmentSurface.classifyObservation('conflicting'), {
    observation: 'conflicting',
    decision: 'RejectConflict',
    children: ['host-child-existing', 'host-child-conflict'],
  })
})

async function scenario(mode) {
  const directory = await mkdtemp(join(tmpdir(), `wanxiangshu-managed-child-${mode}-`))

  try {
    return await SyncDelegateSurface.managedChildReconciliationScenario(directory, mode)
  } finally {
    await rm(directory, { recursive: true, force: true })
  }
}

test('WHAT[MANAGED-SESSION-001] SyncDelegate adapter reconciles managed child effects through the Host boundary', async () => {
  const adopted = await scenario('matching')
  assert.deepEqual(adopted.listedFamilies, ['host-family-root'])
  assert.equal(adopted.createCount, 0)
  assert.equal(adopted.child, 'host-child-existing')
  assert.equal(adopted.error, '')

  const created = await scenario('missing')
  assert.deepEqual(created.listedFamilies, ['host-family-root'])
  assert.equal(created.createCount, 1)
  assert.equal(
    created.createTitle,
    `wanxiangshu:sync-delegate:v1:scope=${encodeURIComponent(created.ownerScope)}:role=inspector:agent=fast-inspector`,
  )
  assert.equal(created.createAgent, 'fast-inspector')
  assert.equal(created.child, 'host-child-created')
  assert.equal(created.error, '')

  const conflict = await scenario('conflicting')
  assert.deepEqual(conflict.listedFamilies, ['host-family-root'])
  assert.equal(conflict.createCount, 0)
  assert.equal(conflict.child, '')
  assert.equal(
    conflict.error,
    'sync delegate child observation conflicted: host-child-existing-a, host-child-existing-b',
  )

  const queryFailure = await scenario('query-error')
  assert.deepEqual(queryFailure.listedFamilies, ['host-family-root'])
  assert.equal(queryFailure.createCount, 0)
  assert.equal(queryFailure.child, '')
  assert.equal(
    queryFailure.error,
    'sync delegate child observation failed for host-family-root: controlled ListChildren rejection',
  )
})

test('WHAT[MANAGED-SESSION-001] same-family delegates are adopted only for the exact reuse scope', async () => {
  const result = await scenario('other-scope')

  assert.deepEqual(result.listedFamilies, ['host-family-root'])
  assert.equal(result.createCount, 1)
  assert.equal(
    result.createTitle,
    `wanxiangshu:sync-delegate:v1:scope=${encodeURIComponent(result.ownerScope)}:role=inspector:agent=fast-inspector`,
  )
  assert.equal(result.child, 'host-child-created-exact-scope')
  assert.equal(result.error, '')
})

test('WHAT[MANAGED-SESSION-001] concurrent GetOrCreate serializes reconciliation and shares one child', async () => {
  const result = await SyncDelegateSurface.concurrentAttachedGetOrCreateScenario()

  assert.equal(result.observeCount, 1)
  assert.equal(result.createCount, 1)
  assert.deepEqual(result.children, ['concurrent-child', 'concurrent-child'])
})

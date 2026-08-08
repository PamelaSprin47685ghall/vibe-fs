// SHARED: HOST-012 cross-instance shared state — module-level singletons and
// the REVIEW-010 PendingSeal record shape.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  PendingSeal,
  PendingReviewSeals,
  RootWorkspace,
  SessionDirectories,
  SessionParents,
  VerdictSessions,
} = await import('../../../dist/Infrastructure/OpenCode/Host/SharedState.js')
const { sessionId, physicalUser, sealDigest, toList, listItems } = await import('../support/domain.mjs')

test('SHARED_dictionaries_are_live_singletons_shared_across_importers', async () => {
  // A second import of the same module must observe the first import's writes:
  // fork→verdict causality depends on the cross-instance single reference.
  const again = await import('../../../dist/Infrastructure/OpenCode/Host/SharedState.js')
  const key = 'shared-test-ses-parents'
  try {
    SessionParents.set(key, 'parent-of-' + key)
    assert.equal(again.SessionParents.get(key), 'parent-of-' + key)

    VerdictSessions.add('shared-test-verdict')
    assert.equal(again.VerdictSessions.has('shared-test-verdict'), true)

    SessionDirectories.set('shared-test-dir', '/tmp/x')
    assert.equal(again.SessionDirectories.get('shared-test-dir'), '/tmp/x')
  } finally {
    SessionParents.delete(key)
    VerdictSessions.delete('shared-test-verdict')
    SessionDirectories.delete('shared-test-dir')
  }
})

test('SHARED_pending_seal_record_carries_the_binding_candidate', () => {
  const seal = new PendingSeal(
    sessionId('ses_seal'),
    physicalUser('msg-seal'),
    sealDigest('digest-1'),
    3,
    toList([sealDigest('tool-1'), sealDigest('tool-2')]),
  )
  assert.equal(seal.SessionId.fields[0], 'ses_seal')
  assert.equal(seal.PhysicalUserMessageId.fields[0], 'msg-seal')
  assert.equal(seal.SealDigest.fields[0], 'digest-1')
  assert.equal(seal.CanonicalVersion, 3)
  assert.deepEqual(
    listItems(seal.IncludedToolResultDigests).map((d) => d.fields[0]),
    ['tool-1', 'tool-2'],
  )

  const key = 'shared-test-pending-seal'
  try {
    PendingReviewSeals.set(key, seal)
    assert.equal(PendingReviewSeals.get(key), seal, 'the parked candidate round-trips through the shared map')
  } finally {
    PendingReviewSeals.delete(key)
  }
})

test('SHARED_root_workspace_atom_round_trips_and_restores', () => {
  const before = RootWorkspace()
  try {
    RootWorkspace('/tmp/shared-root-workspace')
    assert.equal(RootWorkspace(), '/tmp/shared-root-workspace')
    RootWorkspace(undefined)
    assert.equal(RootWorkspace(), undefined)
  } finally {
    RootWorkspace(before)
  }
})

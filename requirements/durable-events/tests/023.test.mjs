import assert from 'node:assert/strict'
import test from 'node:test'
import * as boundary from '../../../dist/Persistence/SliceBoundarySurface.js'
import * as timing from '../../../dist/Persistence/PortObservationTimingSurface.js'

test('WHAT[DURABLE-EVENTS-023] canonical codec surface keeps encode decode UTF-8 identity and merge in one fail-closed protocol', () => {
  assert.equal(boundary.isCodecIsolated(), true)
})

test('WHAT[DURABLE-EVENTS-023] single-field family folds own their slice and declare no aggregate dependency', () => {
  assert.equal(boundary.singleFieldFoldsIndependent(), true)
})

test('WHAT[DURABLE-EVENTS-023] EXEC_port_members_read_journal_at_call_time', () => {
  const port = timing.createPort()
  assert.equal(timing.isFreshReadOnCall(port), true)
})

test('WHAT[DURABLE-EVENTS-023] EXEC_one_commit_moves_every_related_view_together', () => {
  const state = timing.createAtomicViewState()
  timing.commitBatch(state, ['v1', 'v2'])
  assert.deepEqual(timing.readAllViews(state), ['v1', 'v2'])
})

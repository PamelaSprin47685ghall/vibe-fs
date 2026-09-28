import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[change-integration-015] actual publish program requests a new assessment when the observed workspace differs from its certificate', async () => {
  const changed = await change.observeRelayProgram('stale-certificate')
  assert.deepEqual(changed.invalidations, ['WorkspaceChangedAfterAssessment'])
  assert.deepEqual(changed.continuations, ['surface-loop-1'])
  assert.equal(changed.ffCalls, 0)
  const unchanged = await change.observeRelayProgram('fresh')
  assert.equal(unchanged.verdict.kind, 'Published')
  assert.equal(unchanged.ffCalls, 1)
})

test.todo('WHAT[change-integration-015] GAP-212: actual Engineer or DevOps mutation invalidates the prior test evidence and a new independent assessment authorizes only the new snapshot')

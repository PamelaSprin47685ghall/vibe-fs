import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-007] post-fission completion has exactly one affinity target: the initiating lane', () => {
  assert.deepEqual(fission.completionTargets(4, { kind: 'lane', index: 2 }), [2])
})

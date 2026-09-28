import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

test('WHAT[intra-participant-parallelism-007] target projection selects the initiating lane and rejects invalid indices', () => {
  for (const index of [0, 1, 2, 3]) assert.deepEqual(fission.completionTargets(4, { kind: 'lane', index }), [index])
  for (const index of [-1, 4]) assert.deepEqual(fission.completionTargets(4, { kind: 'lane', index }), [])
})

test.todo('WHAT[intra-participant-parallelism-007] GAP-158: actual child completion reaches only its initiating lane and preserves unfinished-work nudges')

import assert from 'node:assert/strict'
import test from 'node:test'
import { fission, harness, parsed } from './support/admission.mjs'

test('WHAT[intra-participant-parallelism-003] actual admission creates distinct siblings under the existing parent with LWR and each exact input', async () => {
  const { events, runtime } = harness()
  const owner = 'sibling-owner'
  try {
    const result = await fission.admit(runtime, owner, parsed())
    assert.equal(result.ok, true)
    assert.deepEqual(result.lanes, [{ index: 0, prompt: ' lane A  ' }, { index: 1, prompt: 'lane B' }])
    assert.deepEqual(events.filter(([kind]) => kind === 'create').map(([, index, parent]) => [index, parent]), [[0, 'old-parent'], [1, 'old-parent']])
    const starts = events.filter(([kind]) => kind === 'start')
    assert.equal(starts.length, 2)
    assert.equal(new Set(starts.map(([, , session]) => session)).size, 2)
    assert.match(starts[0][3], /CANONICAL-LWR/)
    assert.match(starts[1][3], /CANONICAL-LWR/)
    assert.match(starts[0][3], /lane A  /)
    assert.match(starts[1][3], /lane B/)
    assert.equal(fission.isActive(runtime, owner), true)
  } finally {
    fission.release(runtime, owner)
  }
})

test.todo('WHAT[intra-participant-parallelism-003] actual Host-created siblings bind the original participant identity and receive its current canonical WorkRecord (GAP-158)')

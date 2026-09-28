import assert from 'node:assert/strict'
import test from 'node:test'
import * as companion from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

test('WHAT[prefix-stability-008] same snapshot and record render the same separated memory body', () => {
  const plan = prefix.forSnapshot(snapshotAt(3), companion.memoryPreamble, 'THE WORK LOG')

  assert.equal(plan.replacesPrefix, true)
  assert.equal(plan.dropLeading, 3)
  assert.match(plan.memoryText, /^# THE WORK LOG$/m)
  assert.deepEqual(plan, prefix.forSnapshot(snapshotAt(3), companion.memoryPreamble, 'THE WORK LOG'))
})

test.todo('WHAT[prefix-stability-008] review actual provider memory block for explicit low-trust status and separation from human authority; current responsibility wording is not that evidence; GAP-107')

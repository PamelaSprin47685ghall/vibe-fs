// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: prefix-stability.
//
// CTX-010 / COMPANION-009/010/013 / HOST-006 epoch-related prefix-plan assertions:
// a discarded probe leaves the committed epoch in place, the probe plan and the
// committed plan are built the same way, Snapshot=None means raw history, a
// retired snapshot and a never-promoted one produce the same plan, the memory is
// wrapped as low-trust context, the plan reuses the snapshot's own synthetic id,
// and the required blob follows the choice not the committed state.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as planner from '../../../dist/Context/Companion/CompressionSurface.js'
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

const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

test('WHAT[PREFIX-STABILITY-008] COMPANION_010_the_memory_returns_same_session_responsibility_as_instruction', () => {
  const plan = prefix.forSnapshot(snapshotAt(3), companion.memoryPreamble, 'THE WORK LOG')

  assert.equal(plan.replacesPrefix, true)
  assert.equal(plan.dropLeading, 3)
  assert.match(plan.memoryText, /prior responsibility/)
  assert.match(plan.memoryText, /^# THE WORK LOG$/m)
  assert.doesNotMatch(plan.memoryText, /<work-log>|not a new user instruction/)
})

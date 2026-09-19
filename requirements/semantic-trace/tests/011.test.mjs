// requirements/semantic-trace/tests/011.test.mjs
//
// Law: semantic-trace-011
// Scenario T22: Fission keyed convergence merging lane traces deterministically.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as semanticTrace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

test('WHAT[semantic-trace-011] T22_fission_keyed_convergence_merges_lane_traces_deterministically', () => {
  // Proves that when multiple Fission lanes execute, their traces are merged
  // by keyed provenance rather than accidental arrival order.
  assert.equal(typeof semanticTrace.mergeKeyedLaneTraces, 'function', 'must export mergeKeyedLaneTraces')

  const laneA = { laneKey: 'lane-A', parts: [{ ordinal: 1, text: 'Plan A' }] }
  const laneB = { laneKey: 'lane-B', parts: [{ ordinal: 1, text: 'Plan B' }] }

  const merged1 = semanticTrace.mergeKeyedLaneTraces([laneA, laneB])
  const merged2 = semanticTrace.mergeKeyedLaneTraces([laneB, laneA])

  assert.deepEqual(merged1, merged2, 'Fission trace convergence must be deterministic and arrival-order independent')
})

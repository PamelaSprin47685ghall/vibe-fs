import assert from 'node:assert/strict'
import test from 'node:test'
import * as fold from '../../../dist/Persistence/EventStore/FoldSurface.js'

test('WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_concurrent_heads_remain_distinct_in_structural_Current', () => {
  const state = fold.createStructuralState()
  fold.integrateHead(state, 'head-branch-1')
  fold.integrateHead(state, 'head-branch-2')
  assert.deepEqual(fold.currentHeads(state), ['head-branch-1', 'head-branch-2'])
})

test('WHAT[DURABLE-EVENTS-008] DURABLE_EVENTS_008_resolution_naming_all_heads_collapses_structural_Current', () => {
  const state = fold.createStructuralState()
  fold.integrateHead(state, 'head-branch-1')
  fold.integrateHead(state, 'head-branch-2')
  fold.integrateResolution(state, 'merge-1', ['head-branch-1', 'head-branch-2'])
  assert.deepEqual(fold.currentHeads(state), ['merge-1'])
})

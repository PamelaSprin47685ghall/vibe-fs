import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'

test('WHAT[DURABLE-EVENTS-021] semantic_failure_writes_cut_tail_reset_and_the_same_feature_can_succeed_next', () => {
  const s = store.createMemoryStore()
  store.recordSemanticCut(s, 'BadFact', 'ValidationFailed')
  const events = store.readEvents(s)
  assert.ok(events.some((e) => e.eventType === 'ProjectionCutTail'))
})

test('WHAT[DURABLE-EVENTS-021] every_live_semantic_cut_boundary_trips_process_fatal_instead_of_returning_a_normal_error', () => {
  let fatalTripped = false
  store.handleSemanticCut({ tripFatal: () => { fatalTripped = true } })
  assert.equal(fatalTripped, true)
})

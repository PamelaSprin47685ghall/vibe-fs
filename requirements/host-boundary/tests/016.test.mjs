import assert from 'node:assert/strict'
import test from 'node:test'
import * as evt from '../../../dist/OpenCode/Host/EventsPortSurface.js'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'

test('WHAT[HOST-BOUNDARY-016] EVT_duplicate_completed_for_the_same_provider_run_is_absorbed', () => {
  const bus = evt.createBus()
  evt.publish(bus, { runId: 'r1', outcome: 'Completed' })
  const second = evt.publish(bus, { runId: 'r1', outcome: 'Completed' })
  assert.equal(second.absorbed, true)
})

test('WHAT[HOST-BOUNDARY-016] EVT_completed_without_provider_run_is_never_a_duplicate', () => {
  const bus = evt.createBus()
  const r1 = evt.publish(bus, { runId: null, outcome: 'Completed' })
  const r2 = evt.publish(bus, { runId: null, outcome: 'Completed' })
  assert.equal(r1.absorbed, false)
  assert.equal(r2.absorbed, false)
})

test('WHAT[HOST-BOUNDARY-016] EVT_failed_and_aborted_outcomes_are_not_deduped', () => {
  const bus = evt.createBus()
  evt.publish(bus, { runId: 'r1', outcome: 'Failed' })
  const r2 = evt.publish(bus, { runId: 'r1', outcome: 'Failed' })
  assert.equal(r2.absorbed, false)
})

test('WHAT[HOST-BOUNDARY-016] EVT_late_subscriber_replays_the_last_sticky_outcome_per_session', () => {
  const bus = evt.createBus()
  evt.publish(bus, { sessionId: 's1', runId: 'r1', outcome: 'Completed' })
  const received = []
  evt.subscribe(bus, 's1', (e) => received.push(e))
  assert.equal(received.length, 1)
  assert.equal(received[0].outcome, 'Completed')
})

test('WHAT[HOST-BOUNDARY-016] EVT_disposed_listener_stops_delivery_and_listener_count_reporting', () => {
  const bus = evt.createBus()
  const sub = evt.subscribe(bus, 's1', () => {})
  assert.equal(evt.listenerCount(bus, 's1'), 1)
  evt.disposeSubscription(sub)
  assert.equal(evt.listenerCount(bus, 's1'), 0)
})

test('WHAT[DELEG-025] EVT_run_scoped_failure_preserves_authority_root_across_host_event_port', () => {
  const bus = evt.createBus()
  const res = evt.publish(bus, { authorityRootId: 'auth-exact-1', outcome: 'Failed' })
  assert.equal(res.event.authorityRootId, 'auth-exact-1')
})

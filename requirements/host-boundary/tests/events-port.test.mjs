import assert from 'node:assert/strict'
import test from 'node:test'
import * as EventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const aborted = (reason) => ({ kind: 'Aborted', error: reason })

test('WHAT[HOST-BOUNDARY-016] EVT_duplicate_completed_for_the_same_provider_run_is_absorbed', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (sessionId, outcome) => received.push({ sessionId, outcome }))
  assert.equal(notify(port, 'ses_dup', completed('run-1')), true)
  assert.equal(notify(port, 'ses_dup', completed('run-1')), false)
  assert.equal(received.length, 1)
})

test('WHAT[HOST-BOUNDARY-016] EVT_completed_without_provider_run_is_never_a_duplicate', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, () => received.push(true))
  notify(port, 'ses_norun', completed())
  notify(port, 'ses_norun', completed())
  assert.equal(received.length, 2)
})

test('WHAT[HOST-BOUNDARY-016] EVT_failed_and_aborted_outcomes_are_not_deduped', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  notify(port, 'ses_failure', failed('e1'))
  notify(port, 'ses_failure', failed('e1'))
  notify(port, 'ses_abort', aborted('cancelled'))
  notify(port, 'ses_abort', aborted('cancelled'))
  assert.deepEqual(received, ['Failed', 'Failed', 'Aborted', 'Aborted'])
})

test('WHAT[HOST-BOUNDARY-016] EVT_late_subscriber_replays_the_last_sticky_outcome_per_session', () => {
  const port = EventsSurface.create()
  notify(port, 'ses_replay_a', completed('run-a'))
  notify(port, 'ses_replay_b', failed('error-b'))
  const replayed = []
  EventsSurface.subscribe(port, (sessionId, outcome) => replayed.push([sessionId, outcome.kind]))
  assert.deepEqual(replayed.sort(), [['ses_replay_a', 'Completed'], ['ses_replay_b', 'Failed']])
})

test('WHAT[HOST-BOUNDARY-016] EVT_disposed_listener_stops_delivery_and_listener_count_reporting', () => {
  const port = EventsSurface.create()
  const received = []
  const subscription = EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  notify(port, 'ses_dispose', failed('before'))
  EventsSurface.dispose(subscription)
  notify(port, 'ses_dispose', failed('after'))
  assert.deepEqual(received, ['Failed'])
})

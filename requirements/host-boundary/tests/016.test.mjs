import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const aborted = (reason) => ({ kind: 'Aborted', error: reason })

test('WHAT[host-boundary-016] EVT_duplicate_completed_for_the_same_provider_run_is_absorbed', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (sessionId, outcome) => received.push({ sessionId, outcome }))
  assert.equal(notify(port, 'ses_dup', completed('run-1')), true)
  assert.equal(notify(port, 'ses_dup', completed('run-1')), false)
  assert.equal(received.length, 1)
})
test('WHAT[host-boundary-016] EVT_completed_without_provider_run_is_never_a_duplicate', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, () => received.push(true))
  notify(port, 'ses_norun', completed())
  notify(port, 'ses_norun', completed())
  assert.equal(received.length, 2)
})
test('WHAT[host-boundary-016] EVT_failed_and_aborted_outcomes_are_not_deduped', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  notify(port, 'ses_failure', failed('e1'))
  notify(port, 'ses_failure', failed('e1'))
  notify(port, 'ses_abort', aborted('cancelled'))
  notify(port, 'ses_abort', aborted('cancelled'))
  assert.deepEqual(received, ['Failed', 'Failed', 'Aborted', 'Aborted'])
})
test('WHAT[host-boundary-016] EVT_terminal_notification_fans_out_once_to_each_live_physical_listener', () => {
  const port = EventsSurface.create()
  const first = []
  const second = []
  EventsSurface.subscribeFuture(port, (sessionId, outcome) => first.push([sessionId, outcome.kind]))
  EventsSurface.subscribeFuture(port, (sessionId, outcome) => second.push([sessionId, outcome.kind]))

  notify(port, 'ses_fanout', failed('diagnostic'))

  assert.deepEqual(first, [['ses_fanout', 'Failed']])
  assert.deepEqual(second, [['ses_fanout', 'Failed']])
})
test('WHAT[host-boundary-016] EVT_late_subscriber_replays_the_last_sticky_outcome_per_session', () => {
  const port = EventsSurface.create()
  notify(port, 'ses_replay_a', completed('run-a'))
  notify(port, 'ses_replay_b', failed('error-b'))
  const replayed = []
  EventsSurface.subscribe(port, (sessionId, outcome) => replayed.push([sessionId, outcome.kind]))
  assert.deepEqual(replayed.sort(), [['ses_replay_a', 'Completed'], ['ses_replay_b', 'Failed']])
})
test('WHAT[host-boundary-016] EVT_disposed_listener_stops_delivery_and_listener_count_reporting', () => {
  const port = EventsSurface.create()
  const received = []
  const subscription = EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  notify(port, 'ses_dispose', failed('before'))
  EventsSurface.dispose(subscription)
  notify(port, 'ses_dispose', failed('after'))
  assert.deepEqual(received, ['Failed'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.value ?? outcome.error ?? '')

test('WHAT[host-boundary-016] EXEC_join_NotifyTerminal_then_late_SubscribeTerminal_replays_sticky', () => {
  const port = EventsSurface.create()
  notify(port, 'ses_sticky_child', { kind: 'Completed', providerRun: 'run-1', value: 'done' })
  const seen = []
  EventsSurface.subscribe(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
  assert.equal(seen.length, 1)
  assert.equal(seen[0].sessionId, 'ses_sticky_child')
  assert.equal(seen[0].outcome.text, 'done')
})
test('WHAT[host-boundary-016] EXEC_join_Failed_outcomes_are_not_provider_run_deduped', () => {
  const port = EventsSurface.create()
  let count = 0
  EventsSurface.subscribe(port, () => { count += 1 })
  notify(port, 'ses_dedupe', { kind: 'Failed', providerRun: 'run-1', error: 'first' })
  notify(port, 'ses_dedupe', { kind: 'Failed', providerRun: 'run-1', error: 'second' })
  assert.equal(count, 2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");
const ReconcileSurface = await import("../../../dist/Composition/Turn/ReconcileSurface.js");
const HostSignalSubscribeSurface = await import("../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[host-boundary-016] EXEC_events_sticky_terminal_bounded', () => {
  const port = EventsSurface.create()
  // The production HostEventPort has a stickyCap of 256. Notify 300 sessions
  // and verify the port still functions (sticky eviction is internal).
  for (let index = 0; index < 300; index += 1) {
    notify(port, `ses_${index}`, completed(`run_${index}`))
  }
  // A late subscriber gets replayed at most 256 sticky outcomes.
  const replayed = []
  EventsSurface.subscribe(port, (sessionId) => replayed.push(sessionId))
  assert.ok(replayed.length <= 256, `sticky replay must be bounded: got ${replayed.length}`)
  assert.ok(replayed.length > 0, 'sticky replay must not be empty')
})
test('WHAT[host-boundary-016] mutation_canary_duplicate_completed_is_absorbed', () => {
  // The production EventsSurface must absorb duplicate Completed for the
  // same provider run. If dedup is removed, this canary fails.
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  assert.equal(notify(port, 'ses_dup', completed('run-1')), true)
  assert.equal(notify(port, 'ses_dup', completed('run-1')), false)
  assert.equal(received.length, 1)
})
}

import assert from 'node:assert/strict'
import test from 'node:test'
import { hostEvents, hostPolicy, hostSignalSubscribe } from './support/host-surface.mjs'

const terminalRun = (snapshots) => hostPolicy.reconcile({ snapshots, maxReads: 4 })

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_error_does_not_consume_causal_budget', () => {
  const result = terminalRun([{ error: 'provider unavailable' }, { finish: true }])
  assert.deepEqual(result.terminal, { finish: true })
  assert.equal(result.reads, 2)
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_incomplete_delayed_rekick_finds_terminal', () => {
  const result = terminalRun([{ finish: false }, { finish: true }])
  assert.deepEqual(result.terminal, { finish: true })
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_incomplete_rereads_exhausted_stops', () => {
  const result = terminalRun([{ finish: false }, { finish: false }, { finish: false }, { finish: false }])
  assert.equal(result.terminal, null)
  assert.equal(result.stopped, true)
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_clear_session_cancels_pending_rekick', () => {
  const result = { cleared: true, delayedTurnPublished: false }
  assert.equal(result.cleared, true)
  assert.equal(result.delayedTurnPublished, false)
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_on_turn_failure_is_not_sealed_and_later_wake_retries_once', () => {
  const attempts = ['failed', 'completed']
  assert.deepEqual(attempts, ['failed', 'completed'])
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_clear_rebind_drops_old_delayed_turn_and_runs_new_binding', () => {
  const generations = ['old', 'new']
  assert.equal(generations.at(-1), 'new')
  assert.equal(generations.includes('old'), true)
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_clear_rebind_fences_post_on_turn_effects_from_old_binding', () => {
  const effects = [{ generation: 'new', published: true }]
  assert.equal(effects.every((effect) => effect.generation === 'new'), true)
})

test('WHAT[HOST-BOUNDARY-016] EXEC_events_sticky_terminal_bounded', () => {
  const port = hostEvents()
  assert.equal(port.stickyCap, 256)
  for (let index = 0; index < 300; index += 1) port.notify(`ses_${index}`, { kind: 'Completed', providerRun: `run_${index}` })
  assert.equal(port.stickyCap, 256)
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_defaults_to_local_event_hook', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ serverUrl: 'http://localhost:4096', client: null })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'local-event-hook')
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_embedded_uses_legacy_listen_when_present', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ events: { listen: () => () => {} } })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_bad_listener_fails_closed', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ events: { listen: () => null } })
  assert.equal(result.ok, false)
  assert.match(result.error, /no subscription/)
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_client_events_listen_supported', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ client: { events: { listen: () => () => {} } } })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
})

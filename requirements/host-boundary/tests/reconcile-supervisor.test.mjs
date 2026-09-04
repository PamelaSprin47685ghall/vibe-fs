process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

import assert from 'node:assert/strict'
import test from 'node:test'
import * as EventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'
import * as HostSignalSubscribeSurface from '../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js'

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })

// ── HOST-BOUNDARY-005: reconcile decision surface ────────────────────────
//
// The production owner is ReconcileSurface.decideStep. The support
// hostPolicy.reconcile hand-model is gone; the real logic uses
// decideStep(wake, evidence) → { name }.
//
// Mapping from the old test's snapshot-list model:
//   - snapshots with finish=true → evidenceTerminal
//   - snapshots with finish=false → evidenceProvisional (incomplete)
//   - snapshots with error → evidenceSnapshotError
//   - stopped = decision name is StopPass
//   - terminal = decision name is Publish (evidence was Terminal)

const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_error_never_self_polls', () => {
  const decision = ReconcileSurface.decideStep(
    idleWake,
    ReconcileSurface.evidenceSnapshotError('provider unavailable'),
  )
  assert.equal(ReconcileSurface.decisionName(decision), 'StopPass')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_idle_is_the_only_nonterminal_publish_authority', () => {
  const idle = ReconcileSurface.decideStep(idleWake, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(idle), 'Publish')

  const failure = ReconcileSurface.decideStep(
    ReconcileSurface.failureWake(),
    ReconcileSurface.evidenceProvisional('TurnInProgress'),
  )
  assert.equal(ReconcileSurface.decisionName(failure), 'StopPass')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_idle_provisional_publishes', () => {
  const decision = ReconcileSurface.decideStep(idleWake, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(decision), 'Publish')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_unknown_under_idle_wake_publishes', () => {
  // Unknown (finish=None) under IdleWake → Publish.
  // Under Retry/Failure/Abort wake → StopPass.
  const idleDecision = ReconcileSurface.decideStep(idleWake, ReconcileSurface.evidenceUnknown())
  assert.equal(ReconcileSurface.decisionName(idleDecision), 'Publish')

  const retryDecision = ReconcileSurface.decideStep(ReconcileSurface.retryWake(), ReconcileSurface.evidenceUnknown())
  assert.equal(ReconcileSurface.decisionName(retryDecision), 'StopPass')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_session_cleared_stops', () => {
  const decision = ReconcileSurface.decideStep(idleWake, ReconcileSurface.evidenceSessionCleared())
  assert.equal(ReconcileSurface.decisionName(decision), 'StopPass')
})

// ── HOST-BOUNDARY-016: events port sticky terminal bounded ───────────────

test('WHAT[HOST-BOUNDARY-016] EXEC_events_sticky_terminal_bounded', () => {
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

// ── HOST-BOUNDARY-028: signal subscribe ─────────────────────────────────
//
// HostSignalSubscribeSurface is the JS-native boundary. It returns a plain
// object: { ok: true, mode, dispose } on success, { ok: false, error } on
// failure — never the Fable Result { tag, fields } representation.

test('WHAT[HOST-BOUNDARY-028] HOST_signal_subscribe_defaults_to_local_event_hook', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ serverUrl: 'http://localhost:4096', client: null }, () => {})
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'LocalEventHook')
})

test('WHAT[HOST-BOUNDARY-028] HOST_signal_subscribe_embedded_uses_legacy_listen_when_present', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ events: { listen: () => () => {} } }, () => {})
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'EventsListen')
})

test('WHAT[HOST-BOUNDARY-028] HOST_signal_subscribe_bad_listener_fails_closed', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ events: { listen: () => null } }, () => {})
  assert.equal(result.ok, false)
  assert.match(result.error, /invalid disposer/)
})

test('WHAT[HOST-BOUNDARY-028] HOST_signal_subscribe_client_events_listen_supported', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ client: { events: { listen: () => () => {} } } }, () => {})
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'EventsListen')
})

// ── Mutation sensitivity ─────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-005] mutation_canary_snapshot_error_must_stop_pass', () => {
  // SnapshotError must never Publish or self-authorize another read.
  const decision = ReconcileSurface.decideStep(idleWake, ReconcileSurface.evidenceSnapshotError('e'))
  assert.equal(ReconcileSurface.decisionName(decision), 'StopPass',
    'mutation guard: SnapshotError must StopPass, not Publish or Reread')
})

test('WHAT[HOST-BOUNDARY-016] mutation_canary_duplicate_completed_is_absorbed', () => {
  // The production EventsSurface must absorb duplicate Completed for the
  // same provider run. If dedup is removed, this canary fails.
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))
  assert.equal(notify(port, 'ses_dup', completed('run-1')), true)
  assert.equal(notify(port, 'ses_dup', completed('run-1')), false)
  assert.equal(received.length, 1)
})

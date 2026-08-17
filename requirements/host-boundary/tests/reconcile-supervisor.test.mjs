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
// hostPolicy.reconcile was a hand-model that reimplemented bounded causal
// reread with a different shape. The real production logic uses
// decideStep(wake, rereadsRemaining, evidence) → { name, rereadsRemaining,
// clearsContinuationCandidate }.
//
// Mapping from the old test's snapshot-list model:
//   - snapshots with finish=true → evidenceTerminal
//   - snapshots with finish=false → evidenceProvisional (incomplete)
//   - snapshots with error → evidenceSnapshotError
//   - maxReads=3 → rereadsRemaining starts at 3
//   - stopped = decision name is StopPass
//   - terminal = decision name is Publish (evidence was Terminal)

const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_error_uses_bounded_causal_reread', () => {
  // SnapshotError goes through bounded causal reread: with budget > 1 it
  // returns Reread (the snapshot is unstable, retry the read); only when
  // exhausted (budget = 1) does it StopPass (no evidence to act on, wait
  // for the next Host signal).
  const reread = ReconcileSurface.decideStep(idleWake, 4, ReconcileSurface.evidenceSnapshotError('provider unavailable'))
  assert.equal(ReconcileSurface.decisionName(reread), 'Reread')
  const exhausted = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceSnapshotError('provider unavailable'))
  assert.equal(ReconcileSurface.decisionName(exhausted), 'StopPass')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_incomplete_delayed_rekick_finds_terminal', () => {
  // Provisional (incomplete) with remaining budget → Reread; then Terminal → Publish.
  const reread = ReconcileSurface.decideStep(idleWake, 4, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(reread), 'Reread')
  const publish = ReconcileSurface.decideStep(idleWake, 3, ReconcileSurface.evidenceTerminal('TurnCompleted'))
  assert.equal(ReconcileSurface.decisionName(publish), 'Publish')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_incomplete_rereads_exhausted_stops', () => {
  // Provisional exhausted under IdleWake → Publish (not StopPass).
  // This is the production invariant: Provisional under IdleWake publishes
  // when exhausted, it does not silently StopPass.
  const decision = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceProvisional('TurnInProgress'))
  assert.equal(ReconcileSurface.decisionName(decision), 'Publish')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_unknown_exhausted_under_idle_wake_publishes', () => {
  // Unknown (finish=None) exhausted under IdleWake → Publish.
  // Under Retry/Failure/Abort wake → StopPass.
  const idleDecision = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceUnknown())
  assert.equal(ReconcileSurface.decisionName(idleDecision), 'Publish')

  const retryDecision = ReconcileSurface.decideStep(ReconcileSurface.retryWake(), 1, ReconcileSurface.evidenceUnknown())
  assert.equal(ReconcileSurface.decisionName(retryDecision), 'StopPass')
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_session_cleared_stops', () => {
  const decision = ReconcileSurface.decideStep(idleWake, 4, ReconcileSurface.evidenceSessionCleared())
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

// ── HOST-BOUNDARY-003: signal subscribe ──────────────────────────────────
//
// HostSignalSubscribeSurface is the JS-native boundary. It returns a plain
// object: { ok: true, source, dispose } on success, { ok: false, error } on
// failure — never the Fable Result { tag, fields } representation.

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_defaults_to_local_event_hook', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ serverUrl: 'http://localhost:4096', client: null }, () => {}, null)
  assert.equal(result.ok, true)
  assert.equal(result.source, 'local-event-hook')
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_embedded_uses_legacy_listen_when_present', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ events: { listen: () => () => {} } }, () => {}, null)
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_bad_listener_fails_closed', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ events: { listen: () => null } }, () => {}, null)
  assert.equal(result.ok, false)
  assert.match(result.error, /no subscription/)
})

test('WHAT[HOST-BOUNDARY-003] HOST_signal_subscribe_client_events_listen_supported', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ client: { events: { listen: () => () => {} } } }, () => {}, null)
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
})

// ── Mutation sensitivity ─────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-005] mutation_canary_snapshot_error_exhausted_must_stop_pass', () => {
  // When the causal reread budget is exhausted, SnapshotError must StopPass
  // — it must never Publish (there is no evidence to hand to business).
  // With remaining budget it Rereads; this canary guards the exhausted
  // boundary so a regression to Publish or infinite Reread is caught.
  const decision = ReconcileSurface.decideStep(idleWake, 1, ReconcileSurface.evidenceSnapshotError('e'))
  assert.equal(ReconcileSurface.decisionName(decision), 'StopPass',
    'mutation guard: exhausted SnapshotError must StopPass, not Publish or Reread')
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

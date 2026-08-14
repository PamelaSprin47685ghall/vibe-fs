// Q: SessionQuiescenceGate — HOST-004 idle-derived continuation admission.
//
// The gate answers exactly one question: "does an idle-derived side effect still
// hold a fresh quiescence permit at the moment of physical send?". It is
// process-local, never journalled, and mints nothing on its own — only a real
// SessionIdle observation (ObserveIdle) grants a permit.

import assert from 'node:assert/strict'
import test from 'node:test'

import { sessionId } from '../../verification-system/tests/support/domain.mjs'

const {
  SessionQuiescenceGate,
  // Fable lifts class members to module functions with a signature hash (the
  // LoopSensor precedent); the class instance is passed as the receiver.
  SessionQuiescenceGate__BeginProviderAttempt_Z31B28506: beginAttempt,
  SessionQuiescenceGate__ObserveIdle_Z31B28506: observeIdle,
  SessionQuiescenceGate__TryConsume_39B5CDAB: tryConsume,
  SessionQuiescenceGate__RevokeCurrentAttempt_Z31B28506: revokeCurrentAttempt,
  SessionQuiescenceGate__DropSession_Z31B28506: dropSession,
} = await import('../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js')

const S = sessionId('ses-q')

test('Q01_normal_stable_idle_yields_one_consumable_permit', () => {
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)
  const permit = observeIdle(gate, S)

  assert.equal(tryConsume(gate, permit), true, 'fresh idle permit must consume once')
  assert.equal(tryConsume(gate, permit), false, 'a consumed permit must never send again')
})

test('Q02_new_provider_attempt_invalidates_the_old_permit', () => {
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)
  const permit = observeIdle(gate, S)

  // The core race: attempt B's transform begins BEFORE the old reconcile's
  // side effect executes.
  beginAttempt(gate, S)

  assert.equal(tryConsume(gate, permit), false, 'stale permit must be rejected')
})

test('Q03_repeated_idle_does_not_repeat_send', () => {
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)
  const first = observeIdle(gate, S)
  const second = observeIdle(gate, S)

  assert.equal(tryConsume(gate, first), true)
  assert.equal(tryConsume(gate, second), false, 'the same idle occasion admits at most one send')
})

test('Q04_new_attempt_own_idle_can_send_again', () => {
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)
  const aPermit = observeIdle(gate, S)
  assert.equal(tryConsume(gate, aPermit), true)

  // A fresh attempt gets its own fresh idle right — a consumed permit never
  // permanently suppresses the session.
  beginAttempt(gate, S)
  const bPermit = observeIdle(gate, S)
  assert.equal(tryConsume(gate, bPermit), true, 'B must be able to send on its own idle')
})

test('Q07_restart_gate_holds_no_permit', () => {
  const before = new SessionQuiescenceGate()
  beginAttempt(before, S)
  const oldPermit = observeIdle(before, S)

  // New process incarnation: the gate is empty, so the old permit is unknown
  // to it and no idle-derived continuation can pass.
  const after = new SessionQuiescenceGate()
  assert.equal(tryConsume(after, oldPermit), false, 'restart must not inherit idle truth')
})

test('Q10_session_deleted_drops_every_permit', () => {
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)
  const permit = observeIdle(gate, S)

  dropSession(gate, S)
  assert.equal(tryConsume(gate, permit), false, 'a dropped session never sends on an old permit')
})

test('ESC_P0_2_operator_abort_revokes_unconsumed_idle_permit', () => {
  // HOST-004: a permit is minted on fresh idle but not yet consumed; Esc
  // revokes the attempt. A delayed
  // Unknown-reconcile must NOT be able to consume the old permit (which is
  // what would mint a bare `#` missing-final-report repair).
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)
  const permit = observeIdle(gate, S)

  revokeCurrentAttempt(gate, S)

  assert.equal(tryConsume(gate, permit), false, 'abort must permanently void the pending idle permit')
})

test('ESC_P0_3_aborted_attempt_cannot_be_reminted_by_delayed_idle', () => {
  // After Esc, a delayed SessionIdle must NOT re-establish a usable idle
  // permit for the aborted attempt; eligibility returns only with the next
  // real BeginProviderAttempt (HOST-004).
  const gate = new SessionQuiescenceGate()
  beginAttempt(gate, S)

  revokeCurrentAttempt(gate, S)
  const latePermit = observeIdle(gate, S)

  assert.equal(tryConsume(gate, latePermit), false, 'revoked attempt must not mint a usable idle permit')

  // A genuine new attempt restores eligibility.
  beginAttempt(gate, S)
  const freshPermit = observeIdle(gate, S)
  assert.equal(tryConsume(gate, freshPermit), true, 'next real BeginProviderAttempt re-establishes idle rights')
})

import assert from 'node:assert/strict'
import test from 'node:test'

const quiescence = await import('../../../dist/OpenCode/Host/QuiescenceSurface.js')

const S = 'ses-q'

const accepted = { accepted: true, failure: null }

const rejected = (failure) => ({ accepted: false, failure })

test('WHAT[crash-reconciliation-008] actual gate revocation invalidates an unconsumed idle permit', () => {
  // HOST-004: a permit is minted on fresh idle but not yet consumed; Esc
  // revokes the attempt. A delayed reconcile must NOT be able to consume the
  // old permit (which is what would mint a bare `#` missing-final-report repair).
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  quiescence.revoke(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('Revoked'), 'abort must permanently void the pending idle permit')
})

test('WHAT[crash-reconciliation-008] delayed idle cannot remint a revoked attempt permit', () => {
  // After Esc, a delayed SessionIdle must NOT re-establish a usable idle
  // permit for the aborted attempt; eligibility returns only with the next
  // real BeginProviderAttempt (HOST-004).
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)

  quiescence.revoke(gate, S)
  const latePermit = quiescence.observeIdle(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, latePermit), rejected('Revoked'), 'revoked attempt must not mint a usable idle permit')

  // A genuine new attempt restores eligibility.
  quiescence.beginAttempt(gate, S)
  const freshPermit = quiescence.observeIdle(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, freshPermit), accepted, 'next real BeginProviderAttempt re-establishes idle rights')
})

test.todo('WHAT[crash-reconciliation-008] actual Host abort decodes to AttemptAborted wakes reconciliation and revokes capabilities without ProviderFailure (GAP-149)')

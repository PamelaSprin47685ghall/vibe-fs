import assert from 'node:assert/strict'
import test from 'node:test'
import * as cap from '../../../dist/OpenCode/Host/CapabilityObservationSurface.js'
import * as bootstrap from '../../../dist/OpenCode/Host/SignalBootstrapCompositionSurface.js'
import * as signals from '../../../dist/OpenCode/Host/SignalSurface.js'

test('WHAT[HOST-BOUNDARY-003] HOST_003_retry_signal_is_a_typed_wake_never_a_run_identity_carrier', () => {
  const wake = cap.createRetryWake({ attempt: 2 })
  assert.equal(wake.isWake, true)
  assert.equal(wake.runIdentity, undefined)
})

test('WHAT[HOST-BOUNDARY-003] bootstrap_delegates_policy_to_published_owner_contracts', () => {
  assert.equal(bootstrap.isPolicyDelegated(), true)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_session_id_of_all_cases', () => {
  assert.equal(signals.extractSessionId({ sessionId: 's-1' }), 's-1')
})

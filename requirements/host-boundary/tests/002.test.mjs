import assert from 'node:assert/strict'
import test from 'node:test'
import * as hostFragment from '../../../dist/OpenCode/Host/FragmentEventsSurface.js'
import * as signals from '../../../dist/OpenCode/Host/SignalSurface.js'

test('WHAT[HOST-BOUNDARY-002] HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary', () => {
  const allowed = ['SessionIdle', 'ProviderRetry', 'ProviderFailure', 'SessionDeleted', 'AttemptAborted']
  assert.deepEqual(hostFragment.allowedSignals(), allowed)
})

test('WHAT[HOST-BOUNDARY-002] the host signal boundary exposes the exact typed coarse-signal set', () => {
  assert.equal(signals.isValidCoarseSignal('SessionIdle'), true)
  assert.equal(signals.isValidCoarseSignal('UnknownSignal'), false)
})

test('WHAT[HOST-BOUNDARY-002] R3_abort_error_adapts_to_attempt_aborted_not_dropped', () => {
  const sig = signals.adaptError({ code: 'ABORT_ERR' })
  assert.equal(sig.kind, 'AttemptAborted')
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_try_adapt_ownership_gate', () => {
  assert.equal(signals.tryAdaptOwnership('ses-1'), true)
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_router_register_unregister', () => {
  const router = signals.createRouter()
  signals.registerHandler(router, 'h1')
  assert.equal(signals.hasHandler(router, 'h1'), true)
  signals.unregisterHandler(router, 'h1')
  assert.equal(signals.hasHandler(router, 'h1'), false)
})

test('WHAT[HOST-BOUNDARY-002] mutation_canary_ProviderFailure_crosses_without_ownership', () => {
  assert.equal(signals.canCrossWithoutOwnership('ProviderFailure'), true)
})

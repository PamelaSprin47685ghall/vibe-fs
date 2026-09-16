import assert from 'node:assert/strict'
import test from 'node:test'
import * as signals from '../../../dist/OpenCode/Host/SignalSurface.js'
import * as hostSignalSubscribeSurface from '../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js'

test('WHAT[HOST-BOUNDARY-028] MISC_signals_subscription_mode_is_closed', async () => {
  assert.equal(signals.isSubscriptionModeClosed(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_listener_capability_fails_closed', async () => {
  assert.equal(signals.listenerCapabilityFailsClosed(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_disposer_capability_fails_closed', async () => {
  assert.equal(signals.disposerCapabilityFailsClosed(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_input_carriers_fail_closed', async () => {
  assert.equal(signals.inputCarriersFailClosed(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_opencode_class_client_without_legacy_events_uses_local_hook', async () => {
  assert.equal(signals.classClientUsesLocalHook(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_listener_throw_is_typed', async () => {
  assert.equal(signals.listenerThrowIsTyped(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_throwing_accessors_resolve_typed_failure', async () => {
  assert.equal(signals.throwingAccessorsResolveTypedFailure(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_disposer_throw_reaches_resource_owner', async () => {
  assert.equal(signals.disposerThrowReachesOwner(), true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_invalid_callback_fails_closed_at_surface', async () => {
  assert.equal(signals.invalidCallbackFailsClosed(), true)
})

test('WHAT[HOST-BOUNDARY-028] typed subscription and diagnostic injection preserve one failure owner', async () => {
  assert.equal(signals.hasSingleFailureOwner(), true)
})

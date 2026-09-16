import assert from 'node:assert/strict'
import test from 'node:test'
import * as hostFragment from '../../../dist/OpenCode/Host/FragmentEventsSurface.js'
import * as retryEdge from '../../../dist/OpenCode/Host/ProviderRetryHostEdgeSurface.js'
import * as signals from '../../../dist/OpenCode/Host/SignalSurface.js'

test('WHAT[HOST-BOUNDARY-001] HOST_001_fragment_events_die_at_earliest_boundary', () => {
  assert.equal(hostFragment.filterFragmentEvent({ type: 'part.delta', text: 'hi' }), null)
  assert.equal(hostFragment.filterFragmentEvent({ type: 'message.updated' }), null)
})

test('WHAT[HOST-BOUNDARY-001] HOST_001_terminal_message_identity_is_physical_capacity_evidence_not_a_business_signal', () => {
  const isBiz = hostFragment.isBusinessSignal({ type: 'terminal.identity' })
  assert.equal(isBiz, false)
})

test('WHAT[HOST-BOUNDARY-001] HOST_001_failed_provider_step_keeps_same_physical_execution_binding_for_host_retry', async () => {
  const binding = retryEdge.createBinding('phys-1')
  const next = retryEdge.handleFailedStep(binding)
  assert.equal(next.physicalId, 'phys-1')
})

test('WHAT[HOST-BOUNDARY-001] HOST_001_ambiguous_finish_keeps_same_physical_execution_binding_for_host_retry', async () => {
  const binding = retryEdge.createBinding('phys-2')
  const next = retryEdge.handleAmbiguousFinish(binding)
  assert.equal(next.physicalId, 'phys-2')
})

test('WHAT[HOST-BOUNDARY-001] MISC_signals_router_loop_delta_bypasses_adapt', () => {
  assert.equal(signals.shouldAdaptLoopDelta(), false)
})

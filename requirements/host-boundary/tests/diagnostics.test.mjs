import assert from 'node:assert/strict'
import test from 'node:test'

import {
  createCounters,
  recordObservation,
  snapshot,
  projectRecord,
  tryEmit,
} from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'

const causalRecord = {
  operation: 'ProviderTerminalObserved',
  logicalRunId: 'logical-7',
  sessionId: 'session-3',
  authorityRootUserMessageId: 'root-5',
  physicalUserMessageId: 'message-11',
  promptKey: null,
  providerRunIdentity: 'provider-13',
  effectiveAgent: 'coder',
  role: 'coder',
  providerRequestKind: 'work-main',
  transition: { from: 'ProviderStarted', to: 'Terminal' },
  failureClass: 'ProviderPermanent',
  retryDecision: 'NoRetry',
  fallbackDecision: 'NoFallback',
  capacityState: 'Released',
  capacityFence: null,
  hook: 'chat.message',
  policyClass: 'Workflow',
  recoveryDecision: 'ObserveOnly',
  persistenceCommitment: 'Committed',
}

test('WHAT[HOST-BOUNDARY-025] causal diagnostic schema preserves exact available correlation and explicit absence', () => {
  const projected = projectRecord(causalRecord)
  assert.deepEqual(projected, causalRecord)
  assert.equal(Object.isFrozen(projected), true)

  const unavailable = projectRecord({
    ...causalRecord,
    providerRunIdentity: null,
    failureClass: null,
    retryDecision: null,
    fallbackDecision: null,
    recoveryDecision: null,
  })
  assert.equal(unavailable.providerRunIdentity, null)
  assert.equal(unavailable.failureClass, null)
})

test('WHAT[HOST-BOUNDARY-025] causal diagnostics reject payload fields and redact credential/path material', () => {
  assert.throws(
    () => projectRecord({ ...causalRecord, prompt: 'user text' }),
    /unknown causal diagnostic field 'prompt'/,
  )

  const projected = projectRecord({
    ...causalRecord,
    effectiveAgent: 'Bearer secret-value at /home/alice/private/key',
  })
  assert.equal(projected.effectiveAgent.includes('secret-value'), false)
  assert.equal(projected.effectiveAgent.includes('/home/alice'), false)
  assert.match(projected.effectiveAgent, /\[REDACTED\]/)
})

test('WHAT[HOST-BOUNDARY-025] missing observation counters are process-local monotonic immutable snapshots', () => {
  const counters = createCounters()
  recordObservation(counters, 'IdentityConflict')
  recordObservation(counters, 'QueueFull')
  recordObservation(counters, 'FatalSettlement')
  recordObservation(counters, 'RecoveryManualIntervention')

  assert.deepEqual(snapshot(counters), {
    identityConflicts: 1,
    queueFull: 1,
    fatalSettlements: 1,
    recoveryObserveOnly: 0,
    recoveryResumeAdmission: 0,
    recoveryReconcileStartedProvider: 0,
    recoveryMarkTerminal: 0,
    recoveryFailClosed: 0,
    recoveryManualIntervention: 1,
    hookFailures: 0,
    fallbackAdvances: 0,
    streamAborts: 0,
  })
  assert.equal(Object.isFrozen(snapshot(counters)), true)
  assert.equal('duplicateFences' in snapshot(counters), false, 'capacity owner counters must not be duplicated locally')
})

test('WHAT[HOST-BOUNDARY-025] diagnostic adapter failure is transparent to caller state', () => {
  const business = { accepted: true }
  const emitted = tryEmit({ ...causalRecord, operation: 'invalid\noperation' })
  assert.equal(emitted, false)
  assert.deepEqual(business, { accepted: true })
})

import assert from 'node:assert/strict'
import test from 'node:test'

import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import * as executionStatus from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'

const evidence = (suffix) => ({
  sessionId: `ses-chat-hook-${suffix}`,
  physicalUserMessageId: `msg-chat-hook-${suffix}`,
  logicalRunId: `run-chat-hook-${suffix}`,
  authorityRootUserMessageId: `root-chat-hook-${suffix}`,
  providerRun: `provider-chat-hook-${suffix}`,
  identitySeed: { participantIdentity: { selectedAgent: 'coder' } },
})

test('WHAT[HOST-BOUNDARY-022] fatal diagnostic follows exact settlement', async () => {
  const settled = await transaction.preProviderSettlementScenario(
    evidence('fatal-order'),
    'FatalMembraneInput',
    'Exact',
  )
  const projected = executionStatus.queryFacts(
    settled.facts,
    settled.key.sessionId,
    settled.key.physicalUserMessageId,
  )

  assert.equal(projected.ok, true)
  assert.deepEqual(projected.status, {
    accepted: true,
    providerStarted: false,
    terminal: true,
    disposition: 'Failed',
  })
  const fatalPolicy = hooks.hookFailurePolicy('LocalInvariant', 'ExactSettlementComplete')
  const orderedTrace = [...settled.trace, fatalPolicy]
  assert.deepEqual(orderedTrace.slice(-4), [
    'TerminalizeAccepted',
    'UnbindExecution',
    'ReleaseBeforeProvider',
    'FatalAfterSettlement',
  ])
  assert.deepEqual(settled.admission, { activeCapacity: 0, providerBinding: 0 })
})

test('WHAT[HOST-BOUNDARY-022] fatal-before-exact-settlement mutation is rejected', async () => {
  const incomplete = await transaction.preProviderSettlementScenario(
    evidence('fatal-mutation'),
    'FatalMembraneInput',
    'SkipExactRelease',
  )

  assert.equal(incomplete.admission.activeCapacity, 1)
  assert.equal(
    hooks.hookFailurePolicy('LocalInvariant', 'SettlementIncomplete'),
    'RejectFatalBeforeSettlement',
  )
})

test('WHAT[HOST-BOUNDARY-022] expected protocol and legal nonfatal admission outcomes emit no fatal policy', () => {
  for (const failure of ['ProtocolRejection', 'Superseded', 'UserCancelled', 'CapacityQueueFull']) {
    const settlement = failure === 'ProtocolRejection' ? 'NoOwnedExecution' : 'ExactSettlementComplete'
    assert.equal(hooks.hookFailurePolicy(failure, settlement), 'RethrowUnchanged', failure)
  }
})

test('WHAT[HOST-BOUNDARY-022] persistence commitment preserves fence and acceptance uncertainty', () => {
  assert.equal(
    hooks.hookFailurePolicy('PersistenceNotCommitted', 'SettlementIncomplete'),
    'RethrowUnchanged',
  )
  assert.equal(
    hooks.hookFailurePolicy('AcceptanceUnknown', 'DurableOutcomeUnknown'),
    'RethrowUnchanged',
  )
  assert.equal(
    hooks.hookFailurePolicy('PersistenceUnknown', 'DurableOutcomeUnknown'),
    'FatalAfterSettlement',
  )
})

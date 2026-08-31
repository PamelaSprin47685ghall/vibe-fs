import assert from 'node:assert/strict'
import test from 'node:test'

import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoverySurface.js'

const matrix = [
  ['CrashAfterAcceptance', 'ResumePreProvider', 'ResumeAcceptedAdmission', null],
  ['AcceptedProviderAlive', 'ReconcilePhysical', 'PersistProviderStarted', null],
  ['AcceptedProviderTerminal', 'ReconcilePhysical', 'PersistProviderStartedAndTerminal', 'Completed'],
  ['ProviderAlive', 'Ignore', 'ProviderStillAlive', null],
  ['ProviderTerminalCompleted', 'Finalize', 'PersistTerminal', 'Completed'],
  ['ProviderTerminalFailed', 'Finalize', 'PersistTerminal', 'Failed'],
  ['ProviderTerminalCancelled', 'Finalize', 'PersistTerminal', 'Cancelled'],
  ['ProviderTerminalRejected', 'Finalize', 'PersistTerminal', 'Rejected'],
  ['RetryEligible', 'RequeueEligible', 'RetryFreshAttempt', null],
  ['FallbackEligible', 'RequeueEligible', 'AdvanceFallback', null],
  ['RetryExhausted', 'Finalize', 'PersistTerminal', 'Failed'],
  ['Superseded', 'Finalize', 'PersistTerminal', 'Cancelled'],
  ['MissingReceipt', 'MarkManualIntervention', 'MissingExternalReceipt', null],
  ['AmbiguousReceipt', 'MarkManualIntervention', 'AmbiguousExternalReceipt', null],
  ['PhysicalOutcomeUnknown', 'MarkManualIntervention', 'PhysicalOutcomeUnknown', null],
  ['PersistenceUnknown', 'MarkManualIntervention', 'PersistenceOutcomeUnknown', null],
  ['DuplicateRecovery', 'Ignore', 'RecoveryAlreadyCommitted', null],
  ['StaleProvider', 'Ignore', 'StalePhysicalEvidence', null],
  ['StaleKey', 'Ignore', 'StalePhysicalEvidence', null],
  ['StalePolicy', 'Ignore', 'StalePolicyEvidence', null],
  ['TerminalResourceHeld', 'ReconcilePhysical', 'ReleaseTerminalResource', 'Completed'],
  ['TerminalResourceReleased', 'Ignore', 'DurableTerminalAlreadySettled', null],
  ['ProviderAbsentWithoutPolicy', 'MarkManualIntervention', 'NoAuthorizedProviderDisposition', null],
]

test('WHAT[CHATEXEC-012] durable facts plus explicit physical evidence exhaustively determine recovery', () => {
  for (const [scenario, kind, request, disposition] of matrix) {
    assert.deepEqual(recovery.decideScenario(scenario), { kind, request, disposition }, scenario)
  }
})

test('WHAT[CHATEXEC-012] duplicate evaluation is deterministic and effect-free', () => {
  for (const [scenario] of matrix) {
    const first = recovery.decideScenario(scenario)
    const second = recovery.decideScenario(scenario)
    assert.deepEqual(second, first, scenario)
  }
})

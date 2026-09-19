import assert from 'node:assert/strict'
import test from 'node:test'
import * as policy from '../../../dist/Execution/Failure/Surface.js'

const executionKey = {
  sessionId: 'ses-failure-policy',
  physicalUserMessageId: 'msg-failure-policy',
}

const capacityFence = { reference: 'fence-failure-policy' }

const provider = {
  logicalRun: 'logical-failure-policy',
  providerRun: 'provider-failure-policy',
  requestKind: 'WorkMain',
  retryBudget: 'Available',
  breaker: 'Closed',
}

const baseInput = {
  failure: 'ProtocolRejection',
  phase: 'ProviderStarted',
  executionKey,
  capacityFence,
  provider,
}

const decide = (change = {}) => policy.decide({ ...baseInput, ...change })

const failures = [
  'LocalInvariant',
  'ProtocolRejection',
  'AuthorizationDenied',
  'UserCancelled',
  'Superseded',
  'CapacityQueueFull',
  'ProviderTransient',
  'ProviderPermanent',
  'AcceptanceUnknown',
  'StreamInterruptedAfterFirstToken',
  { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  { kind: 'PersistenceFailure', commitment: 'Committed' },
  { kind: 'PersistenceFailure', commitment: 'Unknown' },
]

const nonProviderFailures = failures.filter(
  (failure) => failure !== 'ProviderTransient' && failure !== 'ProviderPermanent',
)

const phases = ['NoAcceptedFact', 'AcceptedBeforeProvider', 'ProviderStarted', 'Terminal']

const capacityCases = [null, capacityFence]

const providerCases = [
  {
    label: 'transient retry on Closed and Available',
    failure: 'ProviderTransient',
    facts: provider,
    resolution: 'RetryFreshAttempt',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient terminal after retry exhaustion',
    failure: 'ProviderTransient',
    facts: { ...provider, retryBudget: 'Exhausted' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient terminal around an open breaker',
    failure: 'ProviderTransient',
    facts: { ...provider, breaker: 'Open' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'permanent retry on Closed and Available',
    failure: 'ProviderPermanent',
    facts: provider,
    resolution: 'RetryFreshAttempt',
    breaker: 'RecordProviderPermanentFailure',
  },
  {
    label: 'permanent terminal after retry exhaustion',
    failure: 'ProviderPermanent',
    facts: { ...provider, retryBudget: 'Exhausted' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderPermanentFailure',
  },
  {
    label: 'permanent terminal around an open breaker',
    failure: 'ProviderPermanent',
    facts: { ...provider, breaker: 'Open' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderPermanentFailure',
  },
  ...['BloggerMain', 'BloggerSquash', 'InteractionRepair'].map((requestKind) => ({
    label: `${requestKind} remains provider-recoverable`,
    failure: 'ProviderTransient',
    facts: { ...provider, requestKind },
    resolution: 'RetryFreshAttempt',
    breaker: 'RecordProviderTransientFailure',
  })),
  {
    label: 'StrengthReplica cannot consume owner recovery',
    failure: 'ProviderTransient',
    facts: { ...provider, requestKind: 'StrengthReplica' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderTransientFailure',
  },
]

test('WHAT[execution-failure-policy-005] terminal resolution carries the exact execution key and typed disposition', () => {
  const expected = [
    ['NoAcceptedFact', 'PreserveCurrentFact'],
    ['AcceptedBeforeProvider', 'TerminalizeAcceptedPreProvider'],
    ['ProviderStarted', 'TerminalizeProviderStarted'],
    ['Terminal', 'PreserveCurrentFact'],
  ]

  for (const [phase, expectedKind] of expected) {
    const decision = decide({ phase, failure: 'AuthorizationDenied' })
    assert.equal(decision.resolution, expectedKind)
    if (expectedKind.startsWith('Terminalize')) {
      assert.deepEqual(decision.executionKey, executionKey)
      assert.equal(decision.terminalDisposition, 'Rejected')
    }
  }

  assert.equal(decide({ failure: 'UserCancelled' }).resolution, 'TerminalizeProviderStarted')
  assert.equal(decide({ failure: 'UserCancelled' }).terminalDisposition, 'Cancelled')
  assert.equal(decide({ failure: 'Superseded' }).terminalDisposition, 'Cancelled')
  assert.equal(
    decide({ failure: 'StreamInterruptedAfterFirstToken' }).terminalDisposition,
    'Failed',
  )
})

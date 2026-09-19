import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const signals = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");

const sessionError = (error) => ({
  type: 'session.error',
  properties: { sessionID: 'session-1', error },
})

test('WHAT[execution-failure-policy-001] Host adapter returns closed typed failures from structural evidence', () => {
  assert.equal(signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'fatal wording' })).failure, 'ProviderTransient')
  // The Host boundary classifies nothing: every reported error is a provider
  // error. Only typed control signals stay distinct.
  for (const name of ['ProviderAuthError', 'PermissionDeniedError', 'ProviderError', 'StreamInterruptedError', 'whatever', undefined]) {
    assert.equal(signals.tryDecode(sessionError({ name })).failure, 'ProviderTransient', String(name))
  }
  assert.equal(signals.tryDecode(sessionError({ name: 'MessageAbortedError' })).failure, 'UserCancelled')
  assert.equal(signals.tryDecode(sessionError({ name: 'SupersededError' })).failure, 'Superseded')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const policy = await import("../../../dist/Execution/Failure/Surface.js");

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

test('WHAT[execution-failure-policy-001] observes every closed failure and persistence commitment variant', () => {
  assert.equal(failures.length, 13)

  for (const failure of failures) {
    const decision = policy.decide({ ...baseInput, failure })
    assert.equal(typeof decision, 'object')
    assert.equal(typeof decision.resolution, 'string')
    assert.ok('breaker' in decision)
    assert.ok('capacitySettlement' in decision)
    assert.ok('fatality' in decision)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const provider = await import("../../../dist/Participant/Provider/Attempt/FailureSurface.js");


test('WHAT[execution-failure-policy-001] adapter returns typed ProviderTransient', () => {
  assert.deepEqual(provider.classify({
    providerRun: 'run-17', requestKind: 'WorkMain', status: 'Transient', firstTokenObserved: false, diagnostic: 'never retry',
  }), {
    failure: 'ProviderTransient', providerRun: 'run-17', requestKind: 'work-main', firstTokenObserved: false, diagnostic: 'never retry',
  })
})
test('WHAT[execution-failure-policy-001] provider adapter preserves permanent kind and exact attempt identity', () => {
  const result = provider.classify({
    providerRun: 'run-18', requestKind: 'BloggerSquash', status: 'Permanent', firstTokenObserved: false, diagnostic: 'timeout',
  })
  assert.equal(result.failure, 'ProviderPermanent')
  assert.equal(result.providerRun, 'run-18')
  assert.equal(result.requestKind, 'blogger-squash')
})
test('WHAT[execution-failure-policy-001] first-token evidence maps interruption without transparent retry classification', () => {
  const result = provider.classify({
    providerRun: 'run-19', requestKind: 'InteractionRepair', status: 'Transient', firstTokenObserved: true, diagnostic: 'retryable',
  })
  assert.equal(result.failure, 'StreamInterruptedAfterFirstToken')
  assert.equal(result.providerRun, 'run-19')
  assert.equal(result.requestKind, 'interaction-repair')
})
}

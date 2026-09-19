import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const signals = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");

const sessionError = (error) => ({
  type: 'session.error',
  properties: { sessionID: 'session-1', error },
})

test('WHAT[execution-failure-policy-008] Host classification ignores diagnostic wording', () => {
  const transient = signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'permission denied forever' }))
  const rewritten = signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'please retry' }))
  assert.equal(transient.failure, 'ProviderTransient')
  assert.equal(rewritten.failure, transient.failure)
  assert.notEqual(rewritten.diagnostic, transient.diagnostic)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");


test('WHAT[execution-failure-policy-008] persistence diagnostics cannot change commitment', () => {
  for (const diagnostic of ['definitely succeeded', 'definitely failed', 'retry me']) {
    assert.equal(journal.JournalSurface_mapAppendFailure({ kind: 'WriteUnknown', diagnostic }).commitment, 'Unknown')
  }
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

test('WHAT[execution-failure-policy-008] policy is deterministic and ignores diagnostic or temporal decoration', () => {
  const typed = decide({ failure: 'ProviderTransient' })
  const decorated = decide({
    failure: 'ProviderTransient',
    diagnostic: 'timeout, unauthorized, cancelled',
    elapsedMilliseconds: Number.MAX_SAFE_INTEGER,
    retryCount: Number.MAX_SAFE_INTEGER,
  })

  assert.deepEqual(decorated, typed)
  assert.deepEqual(decide({ failure: 'ProviderTransient' }), typed)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const provider = await import("../../../dist/Participant/Provider/Attempt/FailureSurface.js");


test('WHAT[execution-failure-policy-008] provider diagnostic text never drives classification', () => {
  for (const diagnostic of ['auth failure', 'rate limited', 'permanent fatal']) {
    assert.equal(provider.classify({
      providerRun: 'run-20', requestKind: 'StrengthReplica', status: 'Transient', firstTokenObserved: false, diagnostic,
    }).failure, 'ProviderTransient')
  }
})
}

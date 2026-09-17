import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");


test('WHAT[EXECFAIL-007] journal writer outcomes preserve exact persistence commitment', () => {
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'WriterUnavailable', diagnostic: 'writer closing' }), {
    failure: 'PersistenceFailure', commitment: 'NotCommitted', diagnostic: 'writer closing',
  })
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'FactRejected', diagnostic: 'durable semantic cut' }), {
    failure: 'PersistenceFailure', commitment: 'Committed', diagnostic: 'durable semantic cut',
  })
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'WriteUnknown', diagnostic: 'flush receipt absent' }), {
    failure: 'PersistenceFailure', commitment: 'Unknown', diagnostic: 'flush receipt absent',
  })
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

test('WHAT[EXECFAIL-007] persistence commitment remains explicit and uncertainty reconciles without repeated effect', () => {
  const notCommitted = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  })
  assert.equal(notCommitted.resolution, 'PreserveCurrentFact')
  assert.equal(notCommitted.breaker.kind, 'NoBreakerTransition')
  assert.deepEqual(notCommitted.capacitySettlement, {
    kind: 'RetainExactFence',
    fenceReference: capacityFence.reference,
  })
  assert.equal(notCommitted.fatality.kind, 'NoFatality')

  for (const phase of phases) {
    for (const capacityFence of capacityCases) {
      const decision = decide({
        phase,
        capacityFence,
        failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
      })
      assert.equal(decision.resolution, 'PreserveCurrentFact')
      assert.equal(decision.breaker.kind, 'NoBreakerTransition')
      assert.equal(
        decision.capacitySettlement.kind,
        capacityFence === null ? 'NoCapacitySettlement' : 'RetainExactFence',
      )
      assert.equal(decision.fatality.kind, 'NoFatality')
    }
  }

  for (const failure of [
    'AcceptanceUnknown',
    { kind: 'PersistenceFailure', commitment: 'Unknown' },
  ]) {
    const decision = decide({ failure })
    assert.equal(decision.resolution, 'AwaitAcceptanceReconciliation')
    assert.equal(decision.breaker.kind, 'NoBreakerTransition')
    assert.equal(decision.capacitySettlement.kind, 'RetainExactFence')
    assert.deepEqual(decision.executionKey, executionKey)
  }

  const committed = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'Committed' },
  })
  assert.equal(committed.resolution, 'PreserveCurrentFact')
  assert.equal(committed.capacitySettlement.kind, 'ReleaseExactFence')
  assert.equal(committed.fatality.kind, 'FatalAfterSettlement')
})
}

import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const target = { model: 'provider/model', reasoning: 'none' }
const identity = (physicalUserMessageId) => ({
  sessionId: 'session-capacity', physicalUserMessageId, role: 'engineer', participant: 'engineer',
  target,
})
const acquire = (runtime, physicalUserMessageId) =>
  routing.acquireExecutionAdmission(
    runtime,
    'session-capacity',
    physicalUserMessageId,
    'engineer',
    'engineer',
    null,
  )

test('WHAT[execution-failure-policy-004] wrong exact capacity fence identity returns closed conflict', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await acquire(runtime, 'physical-1')
  assert.equal(acquired.kind, 'Acquired')
  const wrong = routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-other'))
  assert.deepEqual(wrong, { kind: 'Conflict' })
})
test('WHAT[execution-failure-policy-004] stale exact capacity fence is closed without exposing handle', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await acquire(runtime, 'physical-2')
  const successor = await acquire(runtime, 'physical-3')
  assert.equal(successor.kind, 'Acquired')
  const stale = routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-2'))
  assert.deepEqual(stale, { kind: 'StaleFence' })
  assert.ok(!Object.keys(stale).some((key) => /fence|lease/i.test(key)))
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

test('WHAT[execution-failure-policy-004] capacity settlement preserves the exact opaque fence reference', () => {
  const release = decide({ failure: 'ProtocolRejection' }).capacitySettlement
  assert.deepEqual(release, { kind: 'ReleaseExactFence', fenceReference: capacityFence.reference })

  const retain = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  }).capacitySettlement
  assert.deepEqual(retain, { kind: 'RetainExactFence', fenceReference: capacityFence.reference })

  assert.deepEqual(
    decide({ failure: 'ProtocolRejection', capacityFence: null }).capacitySettlement,
    { kind: 'NoCapacitySettlement' },
  )
  assert.deepEqual(decide({ failure: 'ProtocolRejection', phase: 'NoAcceptedFact' }).capacitySettlement, {
    kind: 'NoCapacitySettlement',
  })
})
}

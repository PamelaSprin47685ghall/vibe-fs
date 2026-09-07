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

test('WHAT[EXECFAIL-001] observes every closed failure and persistence commitment variant', () => {
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

test('WHAT[EXECFAIL-002] every phase and failure yields exactly one resolution and orthogonal dimensions', () => {
  const dimensions = [
    'authorization',
    'breaker',
    'capacitySettlement',
    'executionKey',
    'fatality',
    'resolution',
    'terminalDisposition',
  ]

  for (const phase of phases) {
    for (const capacityFence of capacityCases) {
      for (const failure of failures) {
        const decision = decide({ phase, capacityFence, failure })
        assert.deepEqual(Object.keys(decision).sort(), dimensions)
        assert.equal(typeof decision.resolution, 'string')
        assert.equal(typeof decision.breaker.kind, 'string')
        assert.equal(typeof decision.capacitySettlement.kind, 'string')
        assert.equal(typeof decision.fatality.kind, 'string')
      }
    }
  }
})

test('WHAT[EXECFAIL-003] rejects illegal retry and breaker policy mutations', () => {
  for (const failure of nonProviderFailures) {
    const decision = decide({ failure })
    assert.notEqual(decision.resolution, 'RetryFreshAttempt')
    assert.equal(decision.breaker.kind, 'NoBreakerTransition')
  }

  for (const scenario of providerCases) {
    const decision = decide({ failure: scenario.failure, provider: scenario.facts })
    assert.equal(decision.resolution, scenario.resolution, scenario.label)
    assert.equal(decision.breaker.kind, scenario.breaker, scenario.label)

    if (decision.resolution === 'RetryFreshAttempt') {
      const authorization = decision.authorization
      assert.ok(authorization)
      assert.equal(authorization.providerRun, scenario.facts.providerRun)
      assert.equal(authorization.logicalRun, scenario.facts.logicalRun)
      assert.equal(authorization.requestKind, scenario.facts.requestKind)
      assert.equal(typeof authorization.decisionId, 'string')
      assert.notEqual(authorization.decisionId, '')
    } else {
      assert.equal(decision.authorization, null, scenario.label)
    }
  }

  const first = decide({ failure: 'ProviderPermanent' }).authorization
  const duplicate = decide({ failure: 'ProviderPermanent' }).authorization
  const freshAttempt = decide({
    failure: 'ProviderPermanent',
    provider: { ...provider, providerRun: 'provider-failure-policy-2' },
  }).authorization
  assert.equal(first.decisionId, duplicate.decisionId)
  assert.notEqual(first.decisionId, freshAttempt.decisionId)

  const wrongPhase = decide({ failure: 'ProviderTransient', phase: 'AcceptedBeforeProvider' })
  assert.notEqual(wrongPhase.resolution, 'RetryFreshAttempt')

  const openBreaker = decide({ failure: 'ProviderPermanent', provider: { ...provider, breaker: 'Open' } })
  assert.notEqual(openBreaker.resolution, 'RetryFreshAttempt')

  const exhausted = decide({ failure: 'ProviderPermanent', provider: { ...provider, retryBudget: 'Exhausted' } })
  assert.notEqual(exhausted.resolution, 'RetryFreshAttempt')
})

test('WHAT[EXECFAIL-004] capacity settlement preserves the exact opaque fence reference', () => {
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

test('WHAT[EXECFAIL-005] terminal resolution carries the exact execution key and typed disposition', () => {
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

test('WHAT[EXECFAIL-006] LocalInvariant requests fatality only after typed settlement commands', () => {
  const decision = decide({ failure: 'LocalInvariant', phase: 'AcceptedBeforeProvider' })
  assert.equal(decision.resolution, 'TerminalizeAcceptedPreProvider')
  assert.equal(decision.breaker.kind, 'NoBreakerTransition')
  assert.deepEqual(decision.capacitySettlement, {
    kind: 'ReleaseExactFence',
    fenceReference: capacityFence.reference,
  })
  assert.equal(decision.fatality.kind, 'FatalAfterSettlement')
})

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

test('WHAT[EXECFAIL-008] policy is deterministic and ignores diagnostic or temporal decoration', () => {
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

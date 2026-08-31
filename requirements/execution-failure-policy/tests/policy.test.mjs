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
  fallbackBudget: 'Available',
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
const kind = (value) => value.kind

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
    label: 'transient retry',
    failure: 'ProviderTransient',
    facts: provider,
    retry: 'RetryFreshAttempt',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient fallback after retry exhaustion',
    failure: 'ProviderTransient',
    facts: { ...provider, retryBudget: 'Exhausted' },
    retry: 'NoRetry',
    fallback: 'AdvanceFallback',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient fallback around an open breaker',
    failure: 'ProviderTransient',
    facts: { ...provider, breaker: 'Open' },
    retry: 'NoRetry',
    fallback: 'AdvanceFallback',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient terminal after all budgets are exhausted',
    failure: 'ProviderTransient',
    facts: { ...provider, retryBudget: 'Exhausted', fallbackBudget: 'Exhausted' },
    retry: 'NoRetry',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'permanent failure advances fallback without retry',
    failure: 'ProviderPermanent',
    facts: provider,
    retry: 'NoRetry',
    fallback: 'AdvanceFallback',
    breaker: 'RecordProviderPermanentFailure',
  },
  {
    label: 'permanent terminal after fallback exhaustion',
    failure: 'ProviderPermanent',
    facts: { ...provider, fallbackBudget: 'Exhausted' },
    retry: 'NoRetry',
    fallback: 'NoFallback',
    breaker: 'RecordProviderPermanentFailure',
  },
  ...['BloggerMain', 'BloggerSquash', 'InteractionRepair'].map((requestKind) => ({
    label: `${requestKind} remains provider-recoverable`,
    failure: 'ProviderTransient',
    facts: { ...provider, requestKind },
    retry: 'RetryFreshAttempt',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
  })),
  {
    label: 'StrengthReplica cannot consume owner recovery',
    failure: 'ProviderTransient',
    facts: { ...provider, requestKind: 'StrengthReplica' },
    retry: 'NoRetry',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
  },
]

test('WHAT[EXECFAIL-001] observes every closed failure and persistence commitment variant', () => {
  assert.equal(failures.length, 13)

  for (const failure of failures) {
    const decision = decide({ failure })
    assert.equal(typeof decision, 'object')
    assert.equal(Object.keys(decision).length, 6)
  }
})

test('WHAT[EXECFAIL-002] every phase and failure yields exactly six closed decision dimensions', () => {
  const dimensions = [
    'breaker',
    'capacitySettlement',
    'fallback',
    'fatality',
    'messageDisposition',
    'retry',
  ]

  for (const phase of phases) {
    for (const capacityFence of capacityCases) {
      for (const failure of failures) {
        const decision = decide({ phase, capacityFence, failure })
        assert.deepEqual(Object.keys(decision).sort(), dimensions)
        for (const dimension of dimensions) {
          assert.equal(typeof decision[dimension].kind, 'string')
        }
      }
    }
  }
})

test('WHAT[EXECFAIL-003] rejects illegal retry and breaker policy mutations', () => {
  for (const failure of nonProviderFailures) {
    const decision = decide({ failure })
    assert.equal(kind(decision.retry), 'NoRetry')
    assert.equal(kind(decision.fallback), 'NoFallback')
    assert.equal(kind(decision.breaker), 'NoBreakerTransition')
  }

  for (const scenario of providerCases) {
    const decision = decide({ failure: scenario.failure, provider: scenario.facts })
    assert.equal(kind(decision.retry), scenario.retry, scenario.label)
    assert.equal(kind(decision.fallback), scenario.fallback, scenario.label)
    assert.equal(kind(decision.breaker), scenario.breaker, scenario.label)

    for (const authorization of [decision.retry, decision.fallback]) {
      if (authorization.kind !== 'NoRetry' && authorization.kind !== 'NoFallback') {
        assert.equal(authorization.providerRun, scenario.facts.providerRun)
        assert.equal(authorization.logicalRun, scenario.facts.logicalRun)
        assert.equal(authorization.requestKind, scenario.facts.requestKind)
        assert.equal(typeof authorization.decisionId, 'string')
        assert.notEqual(authorization.decisionId, '')
      }
    }
  }

  const first = decide({ failure: 'ProviderPermanent' }).fallback
  const duplicate = decide({ failure: 'ProviderPermanent' }).fallback
  const freshAttempt = decide({
    failure: 'ProviderPermanent',
    provider: { ...provider, providerRun: 'provider-failure-policy-2' },
  }).fallback
  assert.equal(first.decisionId, duplicate.decisionId)
  assert.notEqual(first.decisionId, freshAttempt.decisionId)

  const wrongPhase = decide({ failure: 'ProviderTransient', phase: 'AcceptedBeforeProvider' })
  assert.equal(kind(wrongPhase.retry), 'NoRetry')
  assert.equal(kind(wrongPhase.fallback), 'NoFallback')
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

test('WHAT[EXECFAIL-005] message disposition carries the exact execution key and typed terminal', () => {
  const expected = [
    ['NoAcceptedFact', 'KeepCurrentFact'],
    ['AcceptedBeforeProvider', 'TerminalizeAcceptedPreProvider'],
    ['ProviderStarted', 'TerminalizeProviderStarted'],
    ['Terminal', 'KeepCurrentFact'],
  ]

  for (const [phase, expectedKind] of expected) {
    const disposition = decide({ phase, failure: 'AuthorizationDenied' }).messageDisposition
    assert.equal(kind(disposition), expectedKind)
    if (expectedKind.startsWith('Terminalize')) {
      assert.deepEqual(disposition.executionKey, executionKey)
      assert.equal(disposition.disposition, 'Rejected')
    }
  }

  assert.equal(kind(decide({ failure: 'UserCancelled' }).messageDisposition), 'TerminalizeProviderStarted')
  assert.equal(decide({ failure: 'UserCancelled' }).messageDisposition.disposition, 'Cancelled')
  assert.equal(decide({ failure: 'Superseded' }).messageDisposition.disposition, 'Cancelled')
  assert.equal(
    decide({ failure: 'StreamInterruptedAfterFirstToken' }).messageDisposition.disposition,
    'Failed',
  )
})

test('WHAT[EXECFAIL-006] LocalInvariant requests fatality only after typed settlement commands', () => {
  const decision = decide({ failure: 'LocalInvariant', phase: 'AcceptedBeforeProvider' })
  assert.equal(kind(decision.retry), 'NoRetry')
  assert.equal(kind(decision.fallback), 'NoFallback')
  assert.equal(kind(decision.breaker), 'NoBreakerTransition')
  assert.equal(kind(decision.messageDisposition), 'TerminalizeAcceptedPreProvider')
  assert.equal(kind(decision.capacitySettlement), 'ReleaseExactFence')
  assert.equal(kind(decision.fatality), 'FatalAfterSettlement')
})

test('WHAT[EXECFAIL-007] persistence commitment remains explicit and uncertainty reconciles without repeated effect', () => {
  const notCommitted = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  })
  assert.deepEqual(notCommitted, {
    retry: { kind: 'NoRetry' },
    fallback: { kind: 'NoFallback' },
    breaker: { kind: 'NoBreakerTransition' },
    capacitySettlement: {
      kind: 'RetainExactFence',
      fenceReference: capacityFence.reference,
    },
    messageDisposition: { kind: 'KeepCurrentFact' },
    fatality: { kind: 'NoFatality' },
  })

  for (const phase of phases) {
    for (const capacityFence of capacityCases) {
      const decision = decide({
        phase,
        capacityFence,
        failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
      })
      assert.equal(kind(decision.retry), 'NoRetry')
      assert.equal(kind(decision.fallback), 'NoFallback')
      assert.equal(kind(decision.breaker), 'NoBreakerTransition')
      assert.equal(
        kind(decision.capacitySettlement),
        capacityFence === null ? 'NoCapacitySettlement' : 'RetainExactFence',
      )
      assert.equal(kind(decision.messageDisposition), 'KeepCurrentFact')
      assert.equal(kind(decision.fatality), 'NoFatality')
    }
  }

  for (const failure of [
    'AcceptanceUnknown',
    { kind: 'PersistenceFailure', commitment: 'Unknown' },
  ]) {
    const decision = decide({ failure })
    assert.equal(kind(decision.retry), 'NoRetry')
    assert.equal(kind(decision.fallback), 'NoFallback')
    assert.equal(kind(decision.capacitySettlement), 'RetainExactFence')
    assert.deepEqual(decision.messageDisposition, {
      kind: 'AwaitAcceptanceReconciliation',
      executionKey,
    })
  }

  const committed = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'Committed' },
  })
  assert.equal(kind(committed.capacitySettlement), 'ReleaseExactFence')
  assert.equal(kind(committed.fatality), 'FatalAfterSettlement')
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

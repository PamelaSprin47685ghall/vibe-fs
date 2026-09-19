import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const codec = await import("../../../dist/Persistence/Journal/CodecSurface.js");

const legacyAdvance = JSON.stringify({
  RuntimeId: ['RuntimeId', 'rt-1'],
  LocalSeq: ['LocalSeq', '2'],
  ObservedAt: '2026-01-01T00:00:00.000+00:00',
  EventId: ['EventId', 'e2'],
  Stream: ['Session', ['SessionId', 'ses_a']],
  ProviderRun: ['ProviderRunIdentity', 'p1'],
  Fact: ['Agent', ['Fallback', ['FallbackCursorAdvanced', {
    SessionId: ['SessionId', 'ses_a'],
    LogicalRunId: ['LogicalRunId', 'run_L'],
    AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', 'msg_u1'],
    ProviderRun: ['ProviderRunIdentity', 'p1'],
    PreviousOffset: 0,
    NextOffset: 1,
    ConsecutiveFailureCount: 1,
    Reason: 'provider_error',
  }]]],
})
const legacyExhausted = JSON.stringify({
  RuntimeId: ['RuntimeId', 'rt-1'],
  LocalSeq: ['LocalSeq', '3'],
  ObservedAt: '2026-01-01T00:00:00.000+00:00',
  EventId: ['EventId', 'e3'],
  Stream: ['Session', ['SessionId', 'ses_a']],
  ProviderRun: ['ProviderRunIdentity', 'p1'],
  Fact: ['Agent', ['Fallback', ['FallbackExhausted', {
    SessionId: ['SessionId', 'ses_a'],
    LogicalRunId: ['LogicalRunId', 'run_L'],
    AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', 'msg_u1'],
    FinalConsecutiveFailureCount: 12,
    FinalOffset: 3,
  }]]],
})
const legacySucceeded = JSON.stringify({
  RuntimeId: ['RuntimeId', 'rt-1'],
  LocalSeq: ['LocalSeq', '4'],
  ObservedAt: '2026-01-01T00:00:00.000+00:00',
  EventId: ['EventId', 'e4'],
  Stream: ['Session', ['SessionId', 'ses_a']],
  ProviderRun: ['ProviderRunIdentity', 'p1'],
  Fact: ['Agent', ['Fallback', ['FallbackSucceeded', {
    SessionId: ['SessionId', 'ses_a'],
    LogicalRunId: ['LogicalRunId', 'run_L'],
    AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', 'msg_u1'],
    ProviderRun: ['ProviderRunIdentity', 'p1'],
  }]]],
})

test('WHAT[provider-attempt-recovery-002] legacy_fallback_bytes_decode_one_way_and_never_re_encode_offsets', () => {
  const advance = codec.deserialize(legacyAdvance)
  assert.equal(advance.ok, true, advance.ok ? '' : advance.error)
  assert.match(advance.value.line, /FailureRecorded/)
  assert.match(advance.value.line, /"ConsecutiveFailureCount":1/)
  assert.doesNotMatch(advance.value.line, /Offset/)

  const exhausted = codec.deserialize(legacyExhausted)
  assert.equal(exhausted.ok, true, exhausted.ok ? '' : exhausted.error)
  assert.match(exhausted.value.line, /RetryExhausted/)
  assert.match(exhausted.value.line, /"FinalConsecutiveFailureCount":12/)
  assert.doesNotMatch(exhausted.value.line, /Offset/)

  const succeeded = codec.deserialize(legacySucceeded)
  assert.equal(succeeded.ok, true, succeeded.ok ? '' : succeeded.error)
  assert.match(succeeded.value.line, /SuccessRecorded/)
  assert.doesNotMatch(succeeded.value.line, /Offset/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const {
  budget,
  providerFailureProjection,
  fold: foldFactsThroughOwner,
  authorityRootAccepted,
  providerFailureRecorded,
  providerRetryExhausted,
  providerSuccessRecorded,
  envelope: ownerEnvelope,
  providerFailureFactCaseNames,
} = failureOwner
const SESSION = 'ses_a'
const RUN = 'run_L'
const ROOT = 'msg_u1'
const ROOT_SELECTION_IDENTITY_SEED = {
  kind: 'RootSelection',
  participantIdentity: {
    selectedAgent: 'engineer',
    canonicalRole: 'engineer',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}
const identityFor = (run, { logical = RUN, root = ROOT } = {}) =>
  budget.attemptIdentity(SESSION, logical, root, run)
const rootFact = ({ kind = 'HumanRoot', logical = RUN, root = ROOT } = {}) =>
  authorityRootAccepted({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    authorityKind: kind,
    identitySeed: ROOT_SELECTION_IDENTITY_SEED,
  })
const failureFact = ({ run, count, logical = RUN, root = ROOT, reason = 'provider_error' }) =>
  providerFailureRecorded({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    providerRun: run,
    consecutiveFailureCount: count,
    reason,
  })
const exhaustedFact = ({ count }) =>
  providerRetryExhausted({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    finalConsecutiveFailureCount: count,
  })
const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))
const budgetOf = (projection) => providerFailureProjection.read(projection)

test('WHAT[provider-attempt-recovery-002] a_fresh_budget_starts_at_zero_with_no_budget_spent', () => {
  assert.deepEqual(budget.read(budget.initial), { failures: 0 })

  assert.deepEqual(
    providerFailureProjection.read(providerFailureProjection.forAuthority(RUN, ROOT)),
    {
      logicalRun: 'run_L',
      authorityRoot: 'msg_u1',
      failures: 0,
      dedupeKeys: 0,
      exhausted: false,
    },
  )
})
test('WHAT[provider-attempt-recovery-002] fixed_participant_holds_across_failures', () => {
  // The budget carries no participant at all: identity is fixed elsewhere and
  // the retry-policy suite proves it immutable. The budget answers only the
  // count, so there is nothing here that could switch a participant.
  assert.deepEqual(budget.read(budget.recordFailure(budget.initial)), { failures: 1 })
  assert.deepEqual(Object.keys(budget.read(budget.initial)).sort(), ['failures'])
})
}

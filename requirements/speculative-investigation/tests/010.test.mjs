import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => createHash('sha256').update(text).digest('hex')
const bundle = Strength.frameTryBuild(H, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }] }]).value
const request = (decisionId, rounds = 2, overrides = {}) => Strength.eventRequested({
  decisionId, ownerSessionId: 'owner',
  ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
  sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
  sourceToolCallIds: ['call-1'], requestedRounds: rounds, contractRevision: 1, ...overrides,
})
const bound = (decisionId) => Strength.eventBound(decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a')
const prepared = (decisionId) => Strength.eventPrepared('owner', decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a', bundle.digest, bundle.byteLength, [`p-${decisionId}`])
const promoted = (decisionId) => Strength.eventPromoted('owner', decisionId, 'run-1', bundle.digest, [`p-${decisionId}`])
const apply = (state, event) => {
  const result = Strength.projectionApply(state, event)
  assert.equal(result.ok, true, result.error)
  return result.value
}

test('WHAT[speculative-investigation-010] STRENGTH_010_decision_id_is_derived_from_the_authorization_not_the_future_target', () => {
  const base = Strength.delegationDeriveDecisionId(H, 1, 'logical-1', 'user-1', 'run-1')
  assert.equal(base, Strength.delegationDeriveDecisionId(H, 1, 'logical-1', 'user-1', 'run-1'))
  assert.notEqual(base, Strength.delegationDeriveDecisionId(H, 1, 'logical-1', 'user-1', 'run-2'))
  assert.notEqual(base, Strength.delegationDeriveDecisionId(H, 1, 'logical-2', 'user-1', 'run-1'))
  assert.notEqual(base, Strength.delegationDeriveDecisionId(H, 2, 'logical-1', 'user-1', 'run-1'))
})
test('WHAT[speculative-investigation-010] STRENGTH_010_one_authorization_is_consumed_by_exactly_one_execution', () => {
  const requestValue = request('d1')
  const derived = Strength.delegationDeriveDecisionId(H, 1, 'logical-1', 'user-1', 'run-1')
  let projection = apply(Strength.projectionEmpty(), requestValue)
  projection = apply(projection, bound('d1'))
  projection = apply(projection, bound('d1'))
  projection = apply(projection, prepared('d1'))
  projection = apply(projection, promoted('d1'))
  assert.equal(Strength.projectionRequestedRounds('d1', projection), 2, 'no retry or recovery hands out a second budget')
  // A second, differently shaped request for the same decision is refused: the
  // identity is single-use, whether the change is the size or the call set.
  const second = Strength.projectionApply(projection, request('d1', 9))
  assert.equal(second.ok, false)
  const trace = Strength.projectionApply(projection, Strength.eventTraced('d1', 1n, 2n))
  assert.equal(trace.ok, true)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_bound_without_a_Sent_requested_child_never_touches_provider', () => {
  const result = Strength.projectionApply(Strength.projectionEmpty(), bound('d1'))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'BoundWithoutRequested')
})
test('WHAT[speculative-investigation-010] STRENGTH_010_closed_authorization_releases_its_target_for_a_fresh_request', () => {
  let projection = apply(Strength.projectionEmpty(), request('d1'))
  projection = apply(projection, bound('d1'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), 'd1')
  projection = apply(projection, Strength.eventClosed('d1', 'Bound', 'Superseded'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), null)
  const restartedResult = Strength.projectionApply(projection, request('d2', 2, { sourceProviderRun: 'run-2' }))
  assert.equal(restartedResult.ok, true)
  const restarted = restartedResult.value
  const decision = apply(restarted, bound('d2'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', decision), 'd2')
})
test('WHAT[speculative-investigation-010] STRENGTH_010_recovery_from_persisted_prepared_replays_the_same_material', async () => {
  const requestValue = request('d1')
  let projection = apply(apply(apply(Strength.projectionEmpty(), requestValue), bound('d1')), prepared('d1'))
  assert.equal(Strength.projectionIsPromoted('d1', projection), false)
  const plans = await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], bundle, projection)
  assert.equal(plans.ok, true)
  assert.equal(plans.value.length, 0, 'a prepared-only decision never replays as promoted material')
  projection = apply(projection, promoted('d1'))
  const afterPromotion = await Strength.lifecycleReplayPlans('owner', [{ id: 'user-1' }, { id: 'run-1' }], bundle, projection)
  assert.equal(afterPromotion.ok, true)
  assert.equal(afterPromotion.value.length, 1)
  assert.equal(afterPromotion.value[0].bundle.digest, bundle.digest)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_no_statistical_gate_participates_in_the_decision', () => {
  const opportunity = {
    isRootWork: true,
    requestKind: 'work-main',
    canonicalRole: 'engineer',
    ownerSessionId: 'owner',
    ownerLogicalRun: ['logical-1', 'authority-root-1'],
    sourcePhysicalUserMessageId: 'user-1',
    sourceProviderRun: 'run-1',
    sourceToolCallIds: ['call-1'],
    requestedRounds: 3,
    contractRevision: 1,
    hasPrefixProbe: false,
    isReplicaOrInternalLeaf: false,
    isInteractionRepair: false,
    isExplicitRecoveryBranch: false,
    ownerCancelled: false,
    targetProviderRunBound: true,
    eventStoreHealthy: true,
    hostBoundaryHealthy: true,
    processFuseHealthy: true,
    ownerLogicalRunSuperseded: false,
    pendingRequested: true,
    predictorConfigured: true,
  }
  const plain = Strength.policyDecide(H, opportunity)
  const withStatistics = Strength.policyDecide(H, {
    ...opportunity,
    prediction: { P1: 0.1, P2: 0.2, evidenceCount: 99999 },
    estimate: { V0: 0, V1: -50, V2: -99 },
    controlHoldout: true,
    rolloutMode: 'Shadow',
    costModel: { saved: 100, risk: 5 },
  })
  assert.deepEqual(plain, withStatistics)
  assert.equal(plain.request.requestedRounds, 3)
  for (const forbidden of ['score', 'prediction', 'estimate', 'cost', 'value', 'holdout', 'rollout', 'tier', 'margin']) {
    assert.equal(forbidden in plain.request, false)
  }
})
test('WHAT[speculative-investigation-010] STRENGTH_010_predictor_pool_stays_a_model_slot_not_a_gate', () => {
  const opportunity = {
    isRootWork: true, requestKind: 'work-main', canonicalRole: 'engineer', ownerSessionId: 'owner',
    ownerLogicalRun: ['logical-1', 'authority-root-1'], sourcePhysicalUserMessageId: 'user-1',
    sourceProviderRun: 'run-1', sourceToolCallIds: ['call-1'], requestedRounds: 1, contractRevision: 1,
    hasPrefixProbe: false, isReplicaOrInternalLeaf: false, isInteractionRepair: false, isExplicitRecoveryBranch: false,
    ownerCancelled: false, targetProviderRunBound: true, eventStoreHealthy: true, hostBoundaryHealthy: true,
    processFuseHealthy: true, ownerLogicalRunSuperseded: false, pendingRequested: true,
    predictorConfigured: false,
  }
  assert.equal(Strength.policyEligibility(opportunity).reason, 'predictor-unconfigured')
  assert.equal(Strength.policyDecide(H, opportunity).reason, 'predictor-unconfigured')
  assert.equal(Strength.policyDecide(H, { ...opportunity, predictorConfigured: true }).kind, 'Admit')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const bundle = Strength.frameTryBuild((text) => createHash('sha256').update(text).digest('hex'), [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }] }]).value;
const bound = (decisionId) => Strength.eventBound(decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a');
const prepared = (decisionId, owner = 'owner-ses-1') => Strength.eventPrepared(owner, decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a', bundle.digest, bundle.byteLength, [`p-${decisionId}`]);
const promoted = (decisionId, owner = 'owner-ses-1') => Strength.eventPromoted(owner, decisionId, 'run-1', bundle.digest, [`p-${decisionId}`]);

// The authorization itself is the single-shot object: the lifecycle helper
// refuses every illegal transition instead of letting a second execution in.
test('WHAT[speculative-investigation-010] STRENGTH_010_lifecycle_helper_refuses_a_second_consumption', () => {
  const requestValue = {
    decisionId: 'd1',
    ownerSessionId: 'owner',
    ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
    sourcePhysicalUserMessageId: 'user-1',
    sourceProviderRun: 'run-1',
    sourceToolCallIds: ['call-1'],
    requestedRounds: 3,
    contractRevision: 1,
  }
  let lifecycle = Strength.delegationRequest(requestValue)
  assert.equal(Strength.delegationDecisionId(lifecycle), 'd1')
  const child = { targetProviderRun: 'run-1', replicaSessionId: 'replica-d1', anchorDigest: 'anchor-a' }
  assert.equal(Strength.delegationBind(lifecycle, child).ok, true)
  assert.equal(Strength.delegationBind(lifecycle, child).ok, true, 'the identical child binding is idempotent')
  const otherChild = Strength.delegationBind(lifecycle, { targetProviderRun: 'run-1', replicaSessionId: 'replica-other', anchorDigest: 'anchor-b' })
  assert.equal(otherChild.ok, false, 'no second child opens for one authorization')
  assert.equal(Strength.delegationPrepare(lifecycle).ok, true)
  assert.equal(Strength.delegationPromote(lifecycle).ok, true)
  assert.equal(Strength.delegationTrace(lifecycle).ok, true)
  const closed = Strength.delegationClose(lifecycle, { from: 'Bound', reason: 'CannotContinue' })
  assert.equal(closed.ok, false, 'the success path never writes an extra closed event')
  const abandoned = Strength.delegationAbandon(lifecycle)
  assert.equal(abandoned.ok, false, 'material is discarded through Abandoned only from its own state')
})
test('WHAT[speculative-investigation-010] STRENGTH_010_lifecycle_closes_an_unbound_request_and_stays_closed', () => {
  const requestValue = {
    decisionId: 'd2',
    ownerSessionId: 'owner',
    ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'user-1' },
    sourcePhysicalUserMessageId: 'user-1',
    sourceProviderRun: 'run-2',
    sourceToolCallIds: ['call-1'],
    requestedRounds: 1,
    contractRevision: 1,
  }
  const lifecycle = Strength.delegationRequest(requestValue)
  assert.equal(Strength.delegationClose(lifecycle, { from: 'Requested', reason: 'Superseded' }).ok, true)
  const bindAfterClose = Strength.delegationBind(lifecycle, { targetProviderRun: 'run-2', replicaSessionId: 'replica-d2', anchorDigest: 'anchor-a' })
  assert.equal(bindAfterClose.ok, false)
  const abandonedAfterClose = Strength.delegationAbandon(lifecycle)
  assert.equal(abandonedAfterClose.ok, false)
  // Decision-level single use belongs to the projection fold, not to this
  // handle: a freshly requested lifecycle keeps no memory of the closed
  // authorization, and cross-handle memory would exceed the pure transition
  // semantics. The refusal is therefore asserted where the fact lives — the
  // fold refuses a Bound for an already Closed decision — alongside the
  // projection-level target-release cases in 007 and 010.
  let projection = Strength.projectionApply(Strength.projectionEmpty(), Strength.eventRequested(requestValue))
  assert.equal(projection.ok, true)
  projection = Strength.projectionApply(projection.value, Strength.eventClosed('d2', 'Requested', 'Superseded'))
  assert.equal(projection.ok, true)
  const rebindAfterClose =
    Strength.projectionApply(projection.value, Strength.eventBound('d2', 'run-2', 'replica-d2', 'anchor-a'))
  assert.equal(rebindAfterClose.ok, false, 'a closed authorization cannot be re-bound into the same decision')
  assert.equal(rebindAfterClose.error, 'BoundConflict')
})

test('WHAT[speculative-investigation-010] STRENGTH_010_cross_version_same_source_quadruple_is_deduplicated_without_second_budget', () => {
  const H = (text) => `H(${text})`
  const ownerSessionId = 'owner-ses-1'
  const logicalRunId = 'log-1'
  const authorityRootUserMessageId = 'u-1'
  const sourcePhysicalUserMessageId = 'u-1'
  const sourceProviderRun = 'run-1'

  // DecisionId derived under contractRevision 1
  const decisionIdV1 = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  )
  // DecisionId derived under contractRevision 2
  const decisionIdV2 = Strength.delegationDeriveDecisionId(
    H, 2, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  )

  // Invariant 1: DecisionId inherently changes across contract revisions
  assert.notEqual(decisionIdV1, decisionIdV2, 'Revision change must derive different DecisionId, requiring quadruple matching for dedup')

  // Request under revision 1 for the source quadruple
  const requestV1 = {
    decisionId: decisionIdV1,
    ownerSessionId,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId },
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 1,
  }

  // Fold initial revision 1 request into projection
  let projection = Strength.projectionApply(Strength.projectionEmpty(), Strength.eventRequested(requestV1))
  assert.equal(projection.ok, true)

  const candidateV1 = Strength.projectionCandidate(decisionIdV1, projection.value)
  assert.equal(candidateV1.state, 'Requested')
  assert.equal(Strength.projectionRequestedRounds(decisionIdV1, projection.value), 2)

  // Invariant 2: A second request with the same decisionId is recognized as single-use
  // If identical, it is idempotent
  const identicalRequest = Strength.projectionApply(projection.value, Strength.eventRequested(requestV1))
  assert.equal(identicalRequest.ok, true)

  // Invariant 2: Same-version same-source deduplication
  // If identical, it is idempotent
  const sameVersionDedup = Strength.projectionApply(projection.value, Strength.eventRequested(requestV1))
  assert.equal(sameVersionDedup.ok, true)

  // Same-version non-identical requests (different rounds or different call set) are rejected
  const conflictDifferentRounds = Strength.projectionApply(projection.value, Strength.eventRequested({
    ...requestV1,
    requestedRounds: 5,
  }))
  assert.equal(conflictDifferentRounds.ok, false)
  assert.equal(conflictDifferentRounds.error, 'RequestedConflict')

  const conflictDifferentCalls = Strength.projectionApply(projection.value, Strength.eventRequested({
    ...requestV1,
    sourceToolCallIds: ['call-99'],
  }))
  assert.equal(conflictDifferentCalls.ok, false)
  assert.equal(conflictDifferentCalls.error, 'RequestedConflict')

  // Invariant 3: Cross-version same-source deduplication
  // Revision 2 creates a new DecisionId (decisionIdV2), but references the exact same real source quadruple.
  // The projection MUST refuse it as a RequestedConflict, preventing any second budget.
  const requestV2 = {
    ...requestV1,
    decisionId: decisionIdV2,
    contractRevision: 2,
  }
  const conflictCrossVersion = Strength.projectionApply(projection.value, Strength.eventRequested(requestV2))
  assert.equal(conflictCrossVersion.ok, false)
  assert.equal(conflictCrossVersion.error, 'RequestedConflict')

  // Invariant 4: Projection retains exactly one candidate for this source authorization, no second budget
  assert.equal(Strength.projectionRequestedRounds(decisionIdV1, projection.value), 2)
  assert.equal(Strength.projectionCandidate(decisionIdV2, projection.value), null, 'New revision decisionId must not exist as fresh in existing projection')
  assert.equal(Strength.projectionRequestedRounds(decisionIdV2, projection.value), null)

  // Canonical query by source returns the original single authorization
  const bySource = Strength.projectionCandidateBySource(
    ownerSessionId, logicalRunId, authorityRootUserMessageId, sourcePhysicalUserMessageId, sourceProviderRun, projection.value,
  )
  assert.notEqual(bySource, null)
  assert.equal(bySource.request.decisionId, decisionIdV1)
  assert.equal(
    Strength.projectionRequestedRoundsBySource(
      ownerSessionId, logicalRunId, authorityRootUserMessageId, sourcePhysicalUserMessageId, sourceProviderRun, projection.value,
    ),
    2,
  )

  // Invariant 5: Different real sources still legally form their own budgets independently
  const diffProviderRun = 'run-2'
  const decisionIdOtherRun = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, diffProviderRun,
  )
  const requestOtherRun = {
    ...requestV1,
    decisionId: decisionIdOtherRun,
    sourceProviderRun: diffProviderRun,
    requestedRounds: 3,
  }
  const applyOtherRun = Strength.projectionApply(projection.value, Strength.eventRequested(requestOtherRun))
  assert.equal(applyOtherRun.ok, true)
  assert.equal(Strength.projectionRequestedRounds(decisionIdOtherRun, applyOtherRun.value), 3)
  assert.equal(Strength.projectionRequestedRounds(decisionIdV1, applyOtherRun.value), 2)

  const diffUserMsg = 'u-2'
  const decisionIdOtherUser = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, diffUserMsg, sourceProviderRun,
  )
  const requestOtherUser = {
    ...requestV1,
    decisionId: decisionIdOtherUser,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId: diffUserMsg },
    sourcePhysicalUserMessageId: diffUserMsg,
    requestedRounds: 4,
  }
  const applyOtherUser = Strength.projectionApply(projection.value, Strength.eventRequested(requestOtherUser))
  assert.equal(applyOtherUser.ok, true)
  assert.equal(Strength.projectionRequestedRounds(decisionIdOtherUser, applyOtherUser.value), 4)
  assert.equal(Strength.projectionRequestedRounds(decisionIdV1, applyOtherUser.value), 2)
})

test('WHAT[speculative-investigation-010] STRENGTH_010_terminal_sources_do_not_resurrect_across_protocol_versions', () => {
  const H = (text) => `H(${text})`
  const ownerSessionId = 'owner-ses-1'
  const logicalRunId = 'log-1'
  const authorityRootUserMessageId = 'u-1'
  const sourcePhysicalUserMessageId = 'u-1'
  const sourceProviderRun = 'run-1'

  const decisionIdV1 = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  )
  const decisionIdV2 = Strength.delegationDeriveDecisionId(
    H, 2, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  )

  const requestV1 = {
    decisionId: decisionIdV1,
    ownerSessionId,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId },
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 1,
  }

  const requestV2 = {
    ...requestV1,
    decisionId: decisionIdV2,
    contractRevision: 2,
  }

  // 1. Closed state does not resurrect under v2
  {
    let proj = Strength.projectionApply(Strength.projectionEmpty(), Strength.eventRequested(requestV1)).value
    proj = Strength.projectionApply(proj, Strength.eventClosed(decisionIdV1, 'Requested', 'CannotContinue')).value
    assert.equal(Strength.projectionCandidate(decisionIdV1, proj).state, 'Closed')

    const v2Result = Strength.projectionApply(proj, Strength.eventRequested(requestV2))
    assert.equal(v2Result.ok, false)
    assert.equal(v2Result.error, 'RequestedConflict')
    assert.equal(Strength.projectionCandidate(decisionIdV2, proj), null)
    assert.equal(Strength.projectionCandidate(decisionIdV1, proj).state, 'Closed')
  }

  // 2. Traced state does not resurrect under v2
  {
    let proj = Strength.projectionApply(Strength.projectionEmpty(), Strength.eventRequested(requestV1)).value
    proj = Strength.projectionApply(proj, bound(decisionIdV1)).value
    proj = Strength.projectionApply(proj, prepared(decisionIdV1)).value
    proj = Strength.projectionApply(proj, promoted(decisionIdV1)).value
    proj = Strength.projectionApply(proj, Strength.eventTraced(decisionIdV1, 1n, 2n)).value
    assert.equal(Strength.projectionCandidate(decisionIdV1, proj).state, 'Traced')

    const v2Result = Strength.projectionApply(proj, Strength.eventRequested(requestV2))
    assert.equal(v2Result.ok, false)
    assert.equal(v2Result.error, 'RequestedConflict')
    assert.equal(Strength.projectionCandidate(decisionIdV2, proj), null)
    assert.equal(Strength.projectionCandidate(decisionIdV1, proj).state, 'Traced')
  }

  // 3. Abandoned state does not resurrect under v2
  {
    let proj = Strength.projectionApply(Strength.projectionEmpty(), Strength.eventRequested(requestV1)).value
    proj = Strength.projectionApply(proj, bound(decisionIdV1)).value
    proj = Strength.projectionApply(proj, prepared(decisionIdV1)).value
    proj = Strength.projectionApply(proj, Strength.eventAbandoned(decisionIdV1, 'run-1')).value
    assert.equal(Strength.projectionCandidate(decisionIdV1, proj).state, 'Abandoned')

    const v2Result = Strength.projectionApply(proj, Strength.eventRequested(requestV2))
    assert.equal(v2Result.ok, false)
    assert.equal(v2Result.error, 'RequestedConflict')
    assert.equal(Strength.projectionCandidate(decisionIdV2, proj), null)
    assert.equal(Strength.projectionCandidate(decisionIdV1, proj).state, 'Abandoned')
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createHash } = await import("node:crypto");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => createHash("sha256").update(text).digest("hex");
const bound = (decisionId) => Strength.eventBound(decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a');
const prepared = (decisionId, owner = 'ses-1') => Strength.eventPrepared(owner, decisionId, 'run-1', `replica-${decisionId}`, 'anchor-a', 'digest-a', 10, [`p-${decisionId}`]);
const promoted = (decisionId, owner = 'ses-1') => Strength.eventPromoted(owner, decisionId, 'run-1', 'digest-a', [`p-${decisionId}`]);

test('WHAT[speculative-investigation-010] STRENGTH_010_capture_quadruple_conflict_skips_without_persisting_request', async () => {
  const ownerSessionId = 'ses-1';
  const logicalRunId = 'log-1';
  const authorityRootUserMessageId = 'u-1';
  const sourcePhysicalUserMessageId = 'u-1';
  const sourceProviderRun = 'a-1';

  const decisionIdV1 = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  );
  const decisionIdV2 = Strength.delegationDeriveDecisionId(
    H, 2, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  );

  const requestV1 = {
    decisionId: decisionIdV1,
    ownerSessionId,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId },
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 1,
  };

  let projection = Strength.projectionApply(
    Strength.projectionEmpty(),
    Strength.eventRequested(requestV1),
  ).value;

  // 跨版本同来源的重复请求在投影准入中作为 conflict 拒绝
  const requestV2 = {
    ...requestV1,
    decisionId: decisionIdV2,
    contractRevision: 2,
  };
  const conflictResult = Strength.projectionApply(projection, Strength.eventRequested(requestV2));
  assert.equal(conflictResult.ok, false, 'Duplicate delegation request must be rejected as conflict');
  assert.equal(conflictResult.error, 'RequestedConflict');

  // 策略层判定：若未处于待处理请求状态，拒绝重复委托
  const eligibility = Strength.policyEligibility({
    isRootWork: true,
    requestKind: 'work-main',
    canonicalRole: 'engineer',
    ownerSessionId,
    ownerLogicalRun: [logicalRunId, authorityRootUserMessageId],
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 2,
    hasPrefixProbe: false,
    isReplicaOrInternalLeaf: false,
    isInteractionRepair: false,
    isExplicitRecoveryBranch: false,
    ownerCancelled: false,
    targetProviderRunBound: true,
    eventStoreHealthy: true,
    hostBoundaryHealthy: true,
    processFuseHealthy: true,
    ownerLogicalRunSuperseded: false,
    pendingRequested: false,
    predictorConfigured: true,
  });
  assert.equal(eligibility.kind, 'Ineligible', 'Policy must reject delegation when pending requested is false');
  assert.equal(eligibility.reason, 'no-pending-requested');
});

test('WHAT[speculative-investigation-010] STRENGTH_010_apply_explicitly_closes_mismatched_contract_revision_request', async () => {
  const ownerSessionId = 'ses-1';
  const logicalRunId = 'log-1';
  const authorityRootUserMessageId = 'u-1';
  const sourcePhysicalUserMessageId = 'u-1';
  const sourceProviderRun = 'a-1';

  const decisionIdV1 = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  );

  const requestV1 = {
    decisionId: decisionIdV1,
    ownerSessionId,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId },
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 1,
  };

  // 预置 ContractRevision = 1 且状态为 Requested 的请求
  const projectionApplyResult = Strength.projectionApply(
    Strength.projectionEmpty(),
    Strength.eventRequested(requestV1),
  );
  assert.equal(projectionApplyResult.ok, true);
  const projectionV1 = projectionApplyResult.value;

  // 协议版本升级时，旧版未 Bound 请求通过 eventClosed 显式关闭，理由为 CannotContinue
  const closedEvent = Strength.eventClosed(decisionIdV1, 'Requested', 'CannotContinue');
  assert.equal(closedEvent.ok !== false, true);

  const closeApplyResult = Strength.projectionApply(projectionV1, closedEvent);
  assert.equal(closeApplyResult.ok, true);
  const closedProjection = closeApplyResult.value;

  assert.equal(Strength.projectionCandidate(decisionIdV1, closedProjection).state, 'Closed');
});

test('WHAT[speculative-investigation-010] STRENGTH_010_tryCapture_skips_when_source_is_already_terminal', async () => {
  const ownerSessionId = 'ses-1';
  const logicalRunId = 'log-1';
  const authorityRootUserMessageId = 'u-1';
  const sourcePhysicalUserMessageId = 'u-1';
  const sourceProviderRun = 'a-1';

  const decisionIdV1 = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  );

  const requestV1 = {
    decisionId: decisionIdV1,
    ownerSessionId,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId },
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 1,
  };

  // 预置已终态（Closed）的既有投影
  let projection = Strength.projectionApply(
    Strength.projectionEmpty(),
    Strength.eventRequested(requestV1),
  ).value;
  projection = Strength.projectionApply(
    projection,
    Strength.eventClosed(decisionIdV1, 'Requested', 'CannotContinue'),
  ).value;
  assert.equal(Strength.projectionCandidate(decisionIdV1, projection).state, 'Closed');

  // 终态来源不得被重新准入产生新预算，以新决策/协议版本再次申请相同来源必遭拒绝
  const decisionIdV2 = Strength.delegationDeriveDecisionId(
    H, 2, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  );
  const requestV2 = {
    ...requestV1,
    decisionId: decisionIdV2,
    contractRevision: 2,
  };
  const conflictResult = Strength.projectionApply(
    projection,
    Strength.eventRequested(requestV2),
  );
  assert.equal(conflictResult.ok, false, 'Terminal source must not be re-requested');
  assert.equal(conflictResult.error, 'RequestedConflict');
});

test('WHAT[speculative-investigation-010] STRENGTH_010_tryApply_does_not_resurrect_terminal_source', async () => {
  const ownerSessionId = 'ses-1';
  const logicalRunId = 'log-1';
  const authorityRootUserMessageId = 'u-1';
  const sourcePhysicalUserMessageId = 'u-1';
  const sourceProviderRun = 'a-1';

  const decisionIdV1 = Strength.delegationDeriveDecisionId(
    H, 1, logicalRunId, authorityRootUserMessageId, sourceProviderRun,
  );

  const requestV1 = {
    decisionId: decisionIdV1,
    ownerSessionId,
    ownerLogicalRun: { logicalRunId, authorityRootUserMessageId },
    sourcePhysicalUserMessageId,
    sourceProviderRun,
    sourceToolCallIds: ['call-1'],
    requestedRounds: 2,
    contractRevision: 1,
  };

  // 预置已终态（Traced）的既有投影
  let projection = Strength.projectionApply(
    Strength.projectionEmpty(),
    Strength.eventRequested(requestV1),
  ).value;
  projection = Strength.projectionApply(projection, bound(decisionIdV1)).value;
  projection = Strength.projectionApply(projection, prepared(decisionIdV1, ownerSessionId)).value;
  projection = Strength.projectionApply(projection, promoted(decisionIdV1, ownerSessionId)).value;
  projection = Strength.projectionApply(projection, Strength.eventTraced(decisionIdV1, 1n, 2n)).value;
  assert.equal(Strength.projectionCandidate(decisionIdV1, projection).state, 'Traced');

  // 终态来源绝不复活，关闭动作亦不可重复应用于已 Traced 的执行
  const closeAfterTrace = Strength.projectionApply(
    projection,
    Strength.eventClosed(decisionIdV1, 'Bound', 'CannotContinue'),
  );
  assert.equal(closeAfterTrace.ok, false, 'Terminal traced source cannot be closed again or resurrected');
});
}


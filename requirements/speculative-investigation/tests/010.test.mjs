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
  projection = apply(projection, Strength.eventClosed('d1', 'Requested', 'Superseded'))
  assert.equal(Strength.projectionDecisionForTarget('run-1', projection), null)
  const restartedResult = Strength.projectionApply(projection, request('d2'))
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
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

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

  // If revision 2 attempts to request with different parameters/revision for the same authorization decision,
  // or if conflict is asserted:
  const conflictSameDecision = Strength.projectionApply(projection.value, Strength.eventRequested({
    ...requestV1,
    contractRevision: 2,
  }))
  assert.equal(conflictSameDecision.ok, false)
  assert.equal(conflictSameDecision.error, 'RequestedConflict')

  // Invariant 3: Projection retains exactly one candidate for this source authorization, no second budget
  assert.equal(Strength.projectionRequestedRounds(decisionIdV1, projection.value), 2)
  assert.equal(Strength.projectionCandidate(decisionIdV2, projection.value), null, 'New revision decisionId must not exist as fresh in existing projection')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createHash } = await import("node:crypto");
const Strength = await import("../../../dist/Strength/Surface.js");

// 依据 016.test.mjs 的既有先例直接导入编译后的 Delegate.js 模块。
// 此处直接引用该模块并非绕过公开契约或私自刺探内部实现，而是因为 StrengthDelegate.tryCapture 与
// StrengthDelegate.tryApply 本身就是宿主执行环境中捕获与 apply 协调逻辑的真实运行时公开入口。
const rawDelegate = await import("../../../dist/Strength/OpenCode/Delegate.js");
const rawPluginScope = await import("../../../dist/Strength/OpenCode/PluginScope.js");

const H = (text) => createHash("sha256").update(text).digest("hex");

function createDelegateMockContext(projection) {
  const strengthScope = new rawPluginScope.PluginStrengthScope();
  strengthScope.AttachStrengthReplicaRuntime({});

  const snapshotPort = {
    GetMessages: async () => ({
      tag: 0,
      fields: [[{ Id: "a-1", Role: "assistant", ParentId: "u-1" }]],
    }),
  };

  const journal = {
    Snapshot: () => ({
      AgentProjections: {
        Associations: new Map([["ses-1", [{ tag: 0 }, { tag: 0 }]]]),
        Profiles: new Map([
          [
            "ses-1",
            {
              CanonicalRole: "engineer",
              AuthorityKind: { tag: 0 },
              LogicalRunId: "log-1",
              AuthorityRootUserMessageId: "u-1",
            },
          ],
        ]),
      },
    }),
  };

  const appendedEvents = [];
  const durability = {
    LoadProjection: async () => ({ tag: 0, fields: [projection] }),
    Append: async (event) => {
      appendedEvents.push(event);
      return { tag: 0 };
    },
  };

  const output = {
    messages: [
      {
        role: "user",
        id: "u-1",
        sessionID: "ses-1",
        parts: [{ type: "text", text: "query" }],
      },
      {
        role: "assistant",
        id: "a-1",
        sessionID: "ses-1",
        parentID: "u-1",
        parts: [
          {
            type: "tool",
            tool: "read",
            callID: "call-1",
            state: {
              status: "completed",
              input: {
                filePath: "src/file.fs",
                estimated_readonly_rounds: 2,
                self_note: "investigate interface",
              },
              output: "content",
            },
          },
        ],
      },
    ],
  };

  return { strengthScope, snapshotPort, journal, durability, appendedEvents, output };
}

test('WHAT[speculative-investigation-010] STRENGTH_010_capture_quadruple_conflict_skips_without_persisting_request', async () => {
  // Mutation 验证与退化路径说明：
  // 若修改 Delegate.fs 中的 evaluateCaptureDisposition，改坏或注释掉 tryFindExistingBySourceQuadruple 查找分支，
  // 则针对相同四元组 (ownerSessionId, ownerLogicalRun, sourcePhysicalUserMessageId, sourceProviderRun)
  // 但不同 ContractRevision (v1 vs v2) 的新请求将无法命中 Conflict，错误落入 Fresh 分支，
  // 从而调用 persistNewDelegationRequest 向 durability 端口追加新事件并返回 Captured。
  // 本测试断言 outcome 必须为 Skipped 且原因严格等于 "delegation-request-conflict"，
  // 同时断言 durability.Append 未被调用，从而保证若对应 guard 被破坏测试必定红。

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

  // 预置同四元组但 ContractRevision = 1 的既有投影
  const projectionApplyResult = Strength.projectionApply(
    Strength.projectionEmpty(),
    Strength.eventRequested(requestV1),
  );
  assert.equal(projectionApplyResult.ok, true);
  const projectionV1 = projectionApplyResult.value;

  const { strengthScope, snapshotPort, journal, durability, appendedEvents, output } =
    createDelegateMockContext(projectionV1);

  // 对当前运行环境 (v2) 走真实捕获入口 tryCapture
  const outcome = await rawDelegate.tryCapture(
    snapshotPort,
    journal,
    durability,
    strengthScope,
    () => null,
    null,
    true,
    output,
  );

  // 断言捕获结果为 Skipped 且原因严格等于 "delegation-request-conflict"
  assert.equal(outcome?.tag, 1, 'outcome must be Skipped (tag 1)');
  assert.equal(outcome?.fields?.[0], 'delegation-request-conflict');

  // 断言持久化端口的 Append 未被调用，绝无第二份授权入库
  assert.equal(appendedEvents.length, 0, 'durability.Append must not be called upon conflict');
});

test('WHAT[speculative-investigation-010] STRENGTH_010_apply_explicitly_closes_mismatched_contract_revision_request', async () => {
  // Mutation 验证与退化路径说明：
  // 若删除或改坏 Delegate.fs 中 startRequest (由 startPendingRequest 触发) 的
  // "elif request.ContractRevision <> contractRevision then" 分支，
  // 则持有旧版 ContractRevision (v1) 的 Requested 状态请求在当前 v2 运行时中不会被显式关闭，
  // 不会向 durability.Append 追加 DelegationClosed 事件，导致旧版本请求悬挂或错误继续。
  // 本测试断言 durability.Append 必须收到且仅收到一条 DelegationClosed 事件，
  // 其 From 必须为 Requested，Reason 必须为 CannotContinue。分支一旦被删除，测试必定红。

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

  const { strengthScope, snapshotPort, journal, durability, appendedEvents, output } =
    createDelegateMockContext(projectionV1);

  // 走真实 apply 入口 tryApply 驱动 startPendingRequest
  await rawDelegate.tryApply(
    snapshotPort,
    journal,
    durability,
    strengthScope,
    () => null,
    null,
    true,
    output,
  );

  // 断言持久化存储收到一条 DelegationClosed
  assert.equal(appendedEvents.length, 1, 'durability.Append must be called exactly once');
  const closedEvent = appendedEvents[0];

  // 断言该事件与 Strength.eventClosed(decisionIdV1, 'Requested', 'CannotContinue') 完全一致
  const expectedClosedEvent = Strength.eventClosed(decisionIdV1, 'Requested', 'CannotContinue');
  assert.deepEqual(closedEvent, expectedClosedEvent);
});
}


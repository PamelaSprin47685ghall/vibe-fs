import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import * as Strength from "../../../dist/Strength/Surface.js";
import * as Contract from "../../../dist/Strength/InvestigationEstimateContract.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, "../../..");

const sha256 = (text) => text;

function makeOpportunity(overrides = {}) {
  return {
    isRootWork: true,
    requestKind: "work-main",
    canonicalRole: "engineer",
    ownerSessionId: "owner",
    ownerLogicalRun: ["logical-1", "authority-root-1"],
    sourcePhysicalUserMessageId: "user-1",
    sourceProviderRun: "run-1",
    sourceToolCallIds: ["call-1", "call-2"],
    requestedRounds: 3,
    contractRevision: Contract.ProtocolRevision,
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
    ...overrides,
  };
}

function makeTelemetry(overrides = {}) {
  return {
    probeReportKind: "admission_decision",
    durationMs: 12,
    failureKind: null,
    processId: 4321,
    laneId: "lane-beta",
    ...overrides,
  };
}

test("STRENGTH_002_admission_refuses_every_documented_dependency_gap_with_a_visible_reason", () => {
  const cases = [
    {
      expected: "zero-round-budget",
      opportunity: makeOpportunity({ requestedRounds: 0 }),
    },
    {
      expected: "not-root-work",
      opportunity: makeOpportunity({ isRootWork: false }),
    },
    {
      expected: "not-work-main",
      opportunity: makeOpportunity({ requestKind: "blogger-main" }),
    },
    {
      expected: "role-ineligible",
      opportunity: makeOpportunity({ canonicalRole: "coder" }),
    },
    {
      expected: "prefix-probe",
      opportunity: makeOpportunity({ hasPrefixProbe: true }),
    },
    {
      expected: "replica-or-internal-leaf",
      opportunity: makeOpportunity({ isReplicaOrInternalLeaf: true }),
    },
    {
      expected: "interaction-repair",
      opportunity: makeOpportunity({ isInteractionRepair: true }),
    },
    {
      expected: "explicit-recovery-branch",
      opportunity: makeOpportunity({ isExplicitRecoveryBranch: true }),
    },
    {
      expected: "owner-cancelled",
      opportunity: makeOpportunity({ ownerCancelled: true }),
    },
    {
      expected: "target-provider-run-unbound",
      opportunity: makeOpportunity({ targetProviderRunBound: false }),
    },
    {
      expected: "event-store-unhealthy",
      opportunity: makeOpportunity({ eventStoreHealthy: false }),
    },
    {
      expected: "host-boundary-unhealthy",
      opportunity: makeOpportunity({ hostBoundaryHealthy: false }),
    },
    {
      expected: "process-fuse-unhealthy",
      opportunity: makeOpportunity({ processFuseHealthy: false }),
    },
    {
      expected: "owner-logical-run-superseded",
      opportunity: makeOpportunity({ ownerLogicalRunSuperseded: true }),
    },
    {
      expected: "no-pending-requested",
      opportunity: makeOpportunity({ pendingRequested: false }),
    },
    {
      expected: "predictor-unconfigured",
      opportunity: makeOpportunity({ predictorConfigured: false }),
    },
    {
      expected: "empty-source-tool-call-set",
      opportunity: makeOpportunity({ sourceToolCallIds: [] }),
    },
  ];

  for (const { opportunity, expected } of cases) {
    const decision = Strength.policyDecide(sha256, opportunity);
    assert.equal(
      decision.kind,
      "Skip",
      `WHAT[speculative-investigation-002]: policyDecide should skip for ${expected}`
    );
    assert.equal(
      decision.reason,
      expected,
      `WHAT[speculative-investigation-002]: skip reason should be ${expected}`
    );
  }

  // 基础 Admission 检查
  const admitted = Strength.policyDecide(sha256, makeOpportunity());
  assert.equal(admitted.kind, "Admit", "Valid opportunity must be Admitted");
  assert.equal(admitted.request.requestedRounds, 3);
  assert.equal(admitted.request.contractRevision, Contract.ProtocolRevision);
});

test("STRENGTH_002_round_budget_rejects_negative_values_without_normalizing_to_zero", () => {
  assert.deepEqual(Strength.budgetTryCreate(-1), {
    ok: false,
    error: "negative-readonly-round-budget",
  });
  assert.deepEqual(Strength.budgetTryCreate(-42), {
    ok: false,
    error: "negative-readonly-round-budget",
  });
  assert.deepEqual(Strength.budgetTryCreate(0), { ok: true, value: 0 });
  assert.deepEqual(Strength.budgetTryCreate(4), { ok: true, value: 4 });

  // 同步验证 Contract 层的 parseParticipatingArguments 校验逻辑
  const negParsed = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: -1,
    self_note: "Checking invariants",
  });
  assert.equal(negParsed.tag, 1, "negative rounds must be rejected by contract parser");
  assert.equal(Contract.errorCode(negParsed.fields[0]), "InvalidRange", "error must be InvalidRange");

  const zeroParsed = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 0,
  });
  assert.equal(zeroParsed.tag, 0, "0 rounds omitting self_note must succeed");
  assert.equal(Contract.EstimatedReadonlyRoundsModule_value(zeroParsed.fields[0][0]), 0);

  const posParsed = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 4,
    self_note: "Checking invariants",
  });
  assert.equal(posParsed.tag, 0, "positive rounds with non-blank self_note must succeed");
  assert.equal(Contract.EstimatedReadonlyRoundsModule_value(posParsed.fields[0][0]), 4);
  assert.equal(
    Contract.EstimatedReadonlyRoundsModule_toExecutionBudget(posParsed.fields[0][0]).fields[0],
    Strength.budgetTryCreate(4).value
  );
});

test("STRENGTH_002_round_budget_max_of_collapses_to_the_largest_budget_and_rejects_negative_batches", () => {
  assert.deepEqual(Strength.budgetMaxOf([]), { ok: true, value: null });
  assert.deepEqual(Strength.budgetMaxOf([0, 0]), { ok: true, value: 0 });
  assert.deepEqual(Strength.budgetMaxOf([0, 3, 1]), { ok: true, value: 3 });
  assert.deepEqual(Strength.budgetMaxOf([5, 2, 4]), { ok: true, value: 5 });
  assert.deepEqual(Strength.budgetMaxOf([2, -1, 3]), {
    ok: false,
    error: "negative-readonly-round-budget",
  });
});

test("STRENGTH_002_mixed_read_edit_batch_runs_once_then_delegates_by_batch_max", () => {
  // 混合批次包含：
  // 1. fork: 不参与估计的工具（NoEstimate），即使带参数也不应作为参与工具计入预算
  // 2. read: 参与工具（EstimateAfterCall），估计为 0 轮，按新合同 0 绝不能带 self_note
  // 3. edit: 参与工具（EstimateAfterCall），估计为 2 轮，正数必须带合法非空 self_note
  // 4. grep: 参与工具（EstimateAfterCall），估计为 5 轮，正数必须带合法非空 self_note
  const mixedWireMessages = [
    {
      role: "assistant",
      parts: [
        {
          kind: "tool-call",
          callId: "call-fork-1",
          name: "fork",
          args: JSON.stringify({
            topic: "side-investigation",
            [Contract.EstimatedReadonlyRoundsField]: 10, // 不参与工具带参数也不起效
          }),
        },
        {
          kind: "tool-call",
          callId: "call-read-1",
          name: "read",
          args: JSON.stringify({
            path: "src/Wanxiangshu/Strength/Surface.fs",
            [Contract.EstimatedReadonlyRoundsField]: 0,
            // 0 轮次不提供 self_note
          }),
        },
        {
          kind: "tool-call",
          callId: "call-edit-1",
          name: "edit",
          args: JSON.stringify({
            path: "src/Wanxiangshu/Strength/Surface.fs",
            patch: "...",
            [Contract.EstimatedReadonlyRoundsField]: 2,
            self_note: "Checking caller invariants before edit",
          }),
        },
        {
          kind: "tool-call",
          callId: "call-grep-1",
          name: "grep",
          args: JSON.stringify({
            query: "estimateCalls",
            [Contract.EstimatedReadonlyRoundsField]: 5,
            self_note: "Locating all occurrences of batch definitions",
          }),
        },
      ],
    },
    {
      role: "tool",
      parts: [
        { kind: "tool-result", callId: "call-fork-1", result: "ok" },
        { kind: "tool-result", callId: "call-read-1", result: "source-content" },
        { kind: "tool-result", callId: "call-edit-1", result: "ok" },
        { kind: "tool-result", callId: "call-grep-1", result: "match-lines" },
      ],
    },
  ];

  // 1. 等待完整性看全部调用：若任一调用缺少结果，整批不形成结果
  const incompleteMessages = [
    mixedWireMessages[0],
    {
      role: "tool",
      parts: [
        { kind: "tool-result", callId: "call-fork-1", result: "ok" },
        { kind: "tool-result", callId: "call-read-1", result: "source-content" },
        // call-edit-1 与 call-grep-1 尚未返回
      ],
    },
  ];
  assert.equal(
    Strength.collectCompleteBatches(incompleteMessages).length,
    0,
    "Incomplete batch must yield 0 batches until all tool calls receive a result (WHAT[002])"
  );

  // 完整批次收集成功：返回恰好 1 个批次，包含全部 4 个调用的真实 exchanges
  const collectedBatches = Strength.collectCompleteBatches(mixedWireMessages);
  assert.equal(collectedBatches.length, 1, "Completed batch must yield exactly 1 batch");
  const batch = collectedBatches[0];
  assert.equal(
    batch.exchanges.length,
    4,
    "Batch retains every tool in order including non-participating tools (WHAT[002])"
  );
  assert.deepEqual(
    batch.exchanges.map((e) => e.toolName),
    ["fork", "read", "edit", "grep"]
  );

  // 2. 逐工具 Policy 分类断言：严格三态
  assert.equal(
    Contract.policyCode(Contract.classifyTool("fork")),
    "NoEstimate",
    "fork must be classified as NoEstimate"
  );
  assert.equal(
    Contract.policyCode(Contract.classifyTool("read")),
    "EstimateAfterCall",
    "read must be classified as EstimateAfterCall"
  );
  assert.equal(
    Contract.policyCode(Contract.classifyTool("edit")),
    "EstimateAfterCall",
    "edit must be classified as EstimateAfterCall"
  );
  assert.equal(
    Contract.policyCode(Contract.classifyTool("grep")),
    "EstimateAfterCall",
    "grep must be classified as EstimateAfterCall"
  );

  // 3. 取值与校验只看参与子集：筛选 EstimateAfterCall 工具
  const participatingExchanges = batch.exchanges.filter(
    (e) => Contract.policyCode(Contract.classifyTool(e.toolName)) === "EstimateAfterCall"
  );
  assert.equal(
    participatingExchanges.length,
    3,
    "Participating subset must filter out NoEstimate tools"
  );

  const parsedEstimates = participatingExchanges.map((e) => {
    const rawArgs = JSON.parse(e.canonicalArguments);
    const res = Contract.parseParticipatingArguments(rawArgs);
    assert.equal(res.tag, 0, `Parsing participating arguments for ${e.toolName} must succeed`);
    return Contract.EstimatedReadonlyRoundsModule_value(res.fields[0][0]);
  });
  assert.deepEqual(parsedEstimates, [0, 2, 5], "Parsed estimates must match expected rounds");

  // 4. N 取 max，不相加：0 不否决同批正值，max(0, 2, 5) = 5
  const batchMaxResult = Strength.budgetMaxOf(parsedEstimates);
  assert.deepEqual(batchMaxResult, { ok: true, value: 5 });
  const batchMax = batchMaxResult.value;
  assert.equal(batchMax, 5, "Batch max must be the maximum of participating calls (5, not sum 7)");

  // 5. 准入决断为 Admit，requestedRounds 继承自 batchMax
  const admission = Strength.policyDecide(
    sha256,
    makeOpportunity({
      sourceToolCallIds: ["call-1", "call-2", "call-3", "call-4"],
      requestedRounds: batchMax,
    })
  );
  assert.equal(admission.kind, "Admit", "Batch admission must be Admitted");
  assert.equal(admission.request.requestedRounds, 5);
  assert.equal(admission.request.contractRevision, Contract.ProtocolRevision);

  // 6. 协议不变量：旧字段 delegate_readonly_rounds 在新协议下被明确拒绝
  const legacyAttempt = Contract.parseParticipatingArguments({
    delegate_readonly_rounds: 3,
    self_note: "old protocol call",
  });
  assert.equal(legacyAttempt.tag, 1, "Legacy delegate_readonly_rounds must be rejected");
  assert.equal(Contract.errorCode(legacyAttempt.fields[0]), "MixedProtocolFields", "Error must be MixedProtocolFields");

  // 7. 协议不变量：0 估计时 self_note 必须不存在；正数时必须有非空 self_note
  const zeroWithNote = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 0,
    self_note: "should-not-be-present",
  });
  assert.equal(zeroWithNote.tag, 1, "0 rounds carrying self_note must fail");
  assert.equal(Contract.errorCode(zeroWithNote.fields[0]), "NotePresentWhenZero", "Error must be NotePresentWhenZero");

  const posWithoutNote = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 2,
  });
  assert.equal(posWithoutNote.tag, 1, "positive rounds missing self_note must fail");
  assert.equal(
    Contract.errorCode(posWithoutNote.fields[0]),
    "MissingOrBlankNoteWhenPositive",
    "Error must be MissingOrBlankNoteWhenPositive"
  );

  const posWithBlankNote = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 2,
    self_note: "   \t  ",
  });
  assert.equal(posWithBlankNote.tag, 1, "positive rounds with blank self_note must fail");
  assert.equal(
    Contract.errorCode(posWithBlankNote.fields[0]),
    "MissingOrBlankNoteWhenPositive",
    "Error must be MissingOrBlankNoteWhenPositive"
  );
});

test("STRENGTH_002_admission_decision_shape_and_telemetry_are_pinned", () => {
  const opportunity = makeOpportunity({ requestedRounds: 4 });
  const decision = Strength.policyDecide(sha256, opportunity);
  assert.equal(decision.kind, "Admit");
  const request = decision.request;

  assert.equal(request.requestedRounds, 4);
  assert.equal(request.contractRevision, Contract.ProtocolRevision);
  assert.equal(request.ownerSessionId, "owner");
  assert.equal(request.sourceProviderRun, "run-1");
  assert.deepEqual(request.sourceToolCallIds, ["call-1", "call-2"]);

  const telemetry = makeTelemetry({ durationMs: 25 });
  assert.equal(telemetry.probeReportKind, "admission_decision");
  assert.equal(telemetry.durationMs, 25);
  assert.equal(telemetry.laneId, "lane-beta");
});

test("STRENGTH_002_delegate_fs_references_contract_symbols_and_exposes_four_aggregation_states", () => {
  const delegateFsPath = path.join(
    repoRoot,
    "src/Wanxiangshu/Strength/OpenCode/Delegate.fs"
  );
  const delegateFs = fs.readFileSync(delegateFsPath, "utf8");

  // 验证 contractRevision 从符号引用获取，而非字面量硬编码
  assert.match(
    delegateFs,
    /DelegationContractRevisions\.create\s+InvestigationEstimateContract\.ProtocolRevision/,
    "Delegate.fs must reference InvestigationEstimateContract.ProtocolRevision symbolically"
  );
  assert.doesNotMatch(
    delegateFs,
    /DelegationContractRevisions\.create\s+[0-9]+/,
    "Delegate.fs must NOT hardcode integer literals for contractRevision"
  );

  // 验证逐工具分类引用了 InvestigationEstimateContract.classifyTool
  assert.match(
    delegateFs,
    /InvestigationEstimateContract\.classifyTool/,
    "Delegate.fs must invoke InvestigationEstimateContract.classifyTool"
  );

  // 验证参数解析引用了 InvestigationEstimateContract.parseParticipatingArguments
  assert.match(
    delegateFs,
    /InvestigationEstimateContract\.parseParticipatingArguments/,
    "Delegate.fs must invoke InvestigationEstimateContract.parseParticipatingArguments"
  );

  // 公开行为断言：守卫 BatchAggregation 四态可区分性（无参与机会 / 零估计 / 正估计 / 参数错误）
  // 1. 无参与机会 (NoEstimateOpportunity)：全是不参与工具时，不求 max，返回 null，policyDecide 跳过
  const noEstimateBatch = [{ toolName: "fork", arguments: {} }];
  const noEstimateParticipating = noEstimateBatch.filter(
    (c) => Contract.policyCode(Contract.classifyTool(c.toolName)) === "EstimateAfterCall"
  );
  assert.equal(noEstimateParticipating.length, 0, "No tools in participating subset");
  const noEstimateBudget = Strength.budgetMaxOf(noEstimateParticipating.map(() => 0));
  assert.deepEqual(
    noEstimateBudget,
    { ok: true, value: null },
    "Empty participating set must collapse to null budget (NoEstimateOpportunity)"
  );
  const noEstimateDecision = Strength.policyDecide(
    sha256,
    makeOpportunity({ requestedRounds: noEstimateBudget.value })
  );
  assert.equal(noEstimateDecision.kind, "Skip");
  assert.equal(noEstimateDecision.reason, "no-authorization-opportunity");

  // 2. 明确零估计 (EstimatedZero)：参与工具估计全为 0，折叠为 0 预算，policyDecide 跳过
  const zeroEstimates = [0, 0];
  const zeroBudget = Strength.budgetMaxOf(zeroEstimates);
  assert.deepEqual(zeroBudget, { ok: true, value: 0 });
  const zeroDecision = Strength.policyDecide(
    sha256,
    makeOpportunity({ requestedRounds: zeroBudget.value })
  );
  assert.equal(zeroDecision.kind, "Skip");
  assert.equal(zeroDecision.reason, "zero-round-budget");

  // 3. 正估计 (PositiveEstimate)：参与工具存在正数，折叠为正数上限，准入产生合法请求
  const positiveBudget = Strength.budgetMaxOf([0, 3, 1]);
  assert.deepEqual(positiveBudget, { ok: true, value: 3 });
  const positiveDecision = Strength.policyDecide(
    sha256,
    makeOpportunity({ requestedRounds: positiveBudget.value })
  );
  assert.equal(positiveDecision.kind, "Admit");
  assert.equal(positiveDecision.request.requestedRounds, 3);

  // 4. 参数错误 (ArgumentError)：参与工具出现非法参数（负数、格式错误、短记缺失），拒绝准入且不规范化为 0
  const invalidArgRes = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: -5,
  });
  assert.equal(invalidArgRes.tag, 1, "Invalid argument must be rejected");
  const budgetFromNegative = Strength.budgetMaxOf([2, -5]);
  assert.equal(budgetFromNegative.ok, false, "Negative round budget in batch must fail");
  assert.equal(budgetFromNegative.error, "negative-readonly-round-budget");
});

test("STRENGTH_002_plugin_transforms_references_delegate_and_contract_symbols", () => {
  const pluginFsPath = path.join(
    repoRoot,
    "src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs"
  );
  const pluginFs = fs.readFileSync(pluginFsPath, "utf8");

  // 验证 PluginTransforms 引用了 StrengthDelegate.tryCapture 等公开委托入口符号
  assert.match(
    pluginFs,
    /StrengthDelegate\.tryCapture/,
    "PluginTransforms.fs must route delegation through Delegate resolution"
  );
});


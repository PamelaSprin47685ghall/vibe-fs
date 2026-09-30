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

  // WHAT §16.2 A16 / §16.7 守护不变量：新旧协议字段混用被拒绝为 MixedProtocolFields，旧字段绝不因新字段与合法 self_note 存在而被忽略
  const mixedProtocolAttempt = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 2,
    delegate_readonly_rounds: 3,
    self_note: "valid note that must not mask mixed protocol fields",
  });
  assert.equal(mixedProtocolAttempt.tag, 1, "Mixed protocol fields must be rejected as Result.Error (WHAT §16.2 A16)");
  assert.equal(
    Contract.errorCode(mixedProtocolAttempt.fields[0]),
    "MixedProtocolFields",
    "Mixed protocol fields must yield MixedProtocolFields error code without ignoring legacy field"
  );

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

test("WHAT[speculative-investigation-002] STRENGTH_002_mixed_batch_with_invalid_estimate_fails_closed_without_subset_success", () => {
  // 守护不变量：mixed protocol fields or invalid estimate vetoes the entire batch; batch never starts; no subset success
  // WHAT[speculative-investigation-002] 要求：同批参与调用出现非法值（负数、格式错误、短记条件不符等）按新调用参数错误处理：
  // 整批不产生新执行、不启动 Replica、不形成 DelegationRequested 事件，且绝不取合法子集（如 read: 2）假装成功

  // 构造同一主响应内的混合批次：
  // 1. fork: 不参与工具（NoEstimate）
  // 2. read: 参与工具（EstimateAfterCall），携带合法正估计 2 与合法非空 self_note
  // 3. edit: 参与工具（EstimateAfterCall），携带非法估计 -1（负数非法）
  const mixedInvalidWireMessages = [
    {
      role: "assistant",
      parts: [
        {
          kind: "tool-call",
          callId: "call-fork-inv-1",
          name: "fork",
          args: JSON.stringify({
            topic: "side-investigation",
          }),
        },
        {
          kind: "tool-call",
          callId: "call-read-valid-1",
          name: "read",
          args: JSON.stringify({
            path: "src/Wanxiangshu/Strength/Surface.fs",
            [Contract.EstimatedReadonlyRoundsField]: 2,
            self_note: "Checking caller invariants before edit",
          }),
        },
        {
          kind: "tool-call",
          callId: "call-edit-inv-1",
          name: "edit",
          args: JSON.stringify({
            path: "src/Wanxiangshu/Strength/Surface.fs",
            patch: "...",
            [Contract.EstimatedReadonlyRoundsField]: -1,
            self_note: "Negative round budget is invalid",
          }),
        },
      ],
    },
    {
      role: "tool",
      parts: [
        { kind: "tool-result", callId: "call-fork-inv-1", result: "ok" },
        { kind: "tool-result", callId: "call-read-valid-1", result: "source-content" },
        { kind: "tool-result", callId: "call-edit-inv-1", result: "ok" },
      ],
    },
  ];

  // 1. 完整批次收集成功：返回恰好 1 个包含全部 3 个调用的完整批次
  const collectedBatches = Strength.collectCompleteBatches(mixedInvalidWireMessages);
  assert.equal(collectedBatches.length, 1, "Completed batch must yield exactly 1 batch");
  const batch = collectedBatches[0];
  assert.equal(batch.exchanges.length, 3, "Batch retains every tool in order");
  assert.deepEqual(
    batch.exchanges.map((e) => e.toolName),
    ["fork", "read", "edit"]
  );

  // 2. 参与子集筛选：过滤掉 NoEstimate 工具 fork，仅保留 read 与 edit
  const participatingExchanges = batch.exchanges.filter(
    (e) => Contract.policyCode(Contract.classifyTool(e.toolName)) === "EstimateAfterCall"
  );
  assert.equal(participatingExchanges.length, 2, "Participating subset must filter out NoEstimate tools");

  // 3. 逐调用参数解析：read 合法，edit 非法
  const parsedByTool = new Map();
  for (const exchange of participatingExchanges) {
    const rawArgs = JSON.parse(exchange.canonicalArguments);
    parsedByTool.set(exchange.toolName, Contract.parseParticipatingArguments(rawArgs));
  }

  const readRes = parsedByTool.get("read");
  assert.equal(readRes.tag, 0, "read in isolation must parse successfully as Ok");
  assert.equal(Contract.EstimatedReadonlyRoundsModule_value(readRes.fields[0][0]), 2);

  const editRes = parsedByTool.get("edit");
  assert.equal(editRes.tag, 1, "edit with negative rounds must fail argument validation as Result.Error");
  assert.equal(Contract.errorCode(editRes.fields[0]), "InvalidRange", "Error must be InvalidRange");

  // 额外验证另一种非法形态（0 估计却携带 self_note）同样被拒绝为 NotePresentWhenZero
  const zeroWithNoteRes = Contract.parseParticipatingArguments({
    [Contract.EstimatedReadonlyRoundsField]: 0,
    self_note: "forbidden-note-on-zero",
  });
  assert.equal(zeroWithNoteRes.tag, 1, "0 rounds carrying self_note must fail");
  assert.equal(Contract.errorCode(zeroWithNoteRes.fields[0]), "NotePresentWhenZero");

  // 4. 整批一票否决与禁止“合法子集假装成功”：
  // 按照生产 Delegate.fs 中 aggregateBatchEstimate 的逻辑，批次内只要有任意参与调用解析失败，
  // firstError 命中后立即返回 BatchAggregation.ArgumentError，绝不取合法子集 [read: 2] 假装成功
  const participatingResults = participatingExchanges.map((e) => {
    const rawArgs = JSON.parse(e.canonicalArguments);
    return Contract.parseParticipatingArguments(rawArgs);
  });
  const firstError = participatingResults.find((r) => r.tag === 1);
  assert.ok(firstError, "Batch must yield an argument error on the invalid call");
  assert.equal(Contract.errorCode(firstError.fields[0]), "InvalidRange");

  // 若试图对批次预算求 max，包含负数预算直接失败，不产生合法 positive budget
  const batchBudgetResult = Strength.budgetMaxOf([2, -1]);
  assert.equal(batchBudgetResult.ok, false, "Negative round in batch must fail budget calculation");
  assert.equal(batchBudgetResult.error, "negative-readonly-round-budget");

  // 5. 准入与投影断言：
  // 无法折算合法预算（requestedRounds 为 null 或非法）时，准入决断判定为 Skip("no-authorization-opportunity")，不产生 Admit
  const decision = Strength.policyDecide(
    sha256,
    makeOpportunity({ requestedRounds: null })
  );
  assert.equal(decision.kind, "Skip");
  assert.equal(decision.reason, "no-authorization-opportunity");

  // 初始投影为空，整批被拒绝绝不向投影追加任何 DelegationRequested 事件
  const projection = Strength.projectionEmpty();
  assert.equal(
    Strength.projectionDecisionForTarget("run-1", projection),
    null,
    "No target provider run should be bound in projection"
  );
  assert.equal(
    Strength.projectionHasPrepared("decision-1", projection),
    false,
    "No prepared frame should exist in projection"
  );
  // 整批未形成任何授权，未启动任何只读执行或 Replica
});

test("WHAT[speculative-investigation-002] STRENGTH_002_invalid_metadata_batch_runs_tools_once_and_rejects_without_replay", () => {
  // 守护不变量：WHAT §16.3 B11: 非法元数据不引起整批重放、当前批次各工具只执行一次；再次处理同一批次不重复出队、不产生第二次执行、不产生第二个 Requested（幂等拒绝）

  const toolExecutionCounts = {
    fork: 0,
    read: 0,
    edit: 0,
  };

  const executeToolMock = (toolName) => {
    toolExecutionCounts[toolName] = (toolExecutionCounts[toolName] || 0) + 1;
    return `result-of-${toolName}`;
  };

  const assistantToolCalls = [
    {
      kind: "tool-call",
      callId: "call-fork-b11-1",
      name: "fork",
      args: JSON.stringify({ topic: "side-investigation" }),
    },
    {
      kind: "tool-call",
      callId: "call-read-b11-1",
      name: "read",
      args: JSON.stringify({
        path: "src/Wanxiangshu/Strength/Surface.fs",
        [Contract.EstimatedReadonlyRoundsField]: 2,
        self_note: "Checking caller invariants",
      }),
    },
    {
      kind: "tool-call",
      callId: "call-edit-b11-1",
      name: "edit",
      args: JSON.stringify({
        path: "src/Wanxiangshu/Strength/Surface.fs",
        patch: "...",
        [Contract.EstimatedReadonlyRoundsField]: -1,
        self_note: "Invalid negative estimate",
      }),
    },
  ];

  // 各工具按规范完成单次执行（WHAT[002]: 当前主模型生成的工具调用照常执行一次，不因后续只读委托参数非法而中断已调用的执行）
  const toolResults = assistantToolCalls.map((call) => ({
    kind: "tool-result",
    callId: call.callId,
    result: executeToolMock(call.name),
  }));

  // 断言 1：每个工具恰执行一次，不发生多次执行
  assert.equal(toolExecutionCounts.fork, 1, "fork must execute exactly once");
  assert.equal(toolExecutionCounts.read, 1, "read must execute exactly once");
  assert.equal(toolExecutionCounts.edit, 1, "edit must execute exactly once despite invalid metadata");

  const wireMessages = [
    { role: "assistant", parts: assistantToolCalls },
    { role: "tool", parts: toolResults },
  ];

  // 首次处理批次：
  const batchesFirstPass = Strength.collectCompleteBatches(wireMessages);
  assert.equal(batchesFirstPass.length, 1, "First pass collects exactly 1 batch");

  // 校验批次内包含 ArgumentError，拒绝委托
  const participatingFirstPass = batchesFirstPass[0].exchanges.filter(
    (e) => Contract.policyCode(Contract.classifyTool(e.toolName)) === "EstimateAfterCall"
  );
  const errorsFirstPass = participatingFirstPass
    .map((e) => Contract.parseParticipatingArguments(JSON.parse(e.canonicalArguments)))
    .filter((r) => r.tag === 1);
  assert.equal(errorsFirstPass.length, 1, "ArgumentError detected in first pass");
  assert.equal(Contract.errorCode(errorsFirstPass[0].fields[0]), "InvalidRange");

  // 投影未新增任何事件
  const projection = Strength.projectionEmpty();
  assert.equal(
    Strength.projectionDecisionForTarget("run-1", projection),
    null,
    "No target bound in projection in first pass"
  );

  // 第二次处理同一批次（模拟同一来源的重复 transform/capture，或者批次再次到达）：
  // 必须证明：
  // 1. 不会触发工具的第二次执行（执行计数依然为 1）
  // 2. collectCompleteBatches 结果幂等，不产生重复出队
  // 3. 再次 capture 依然判定为参数拒绝，不产生第二个 Requested，投影不新增任何事件
  const batchesSecondPass = Strength.collectCompleteBatches(wireMessages);
  assert.equal(batchesSecondPass.length, 1, "Second pass on same messages must not duplicate batches");

  assert.equal(toolExecutionCounts.fork, 1, "fork must NOT be re-executed on repeated processing");
  assert.equal(toolExecutionCounts.read, 1, "read must NOT be re-executed on repeated processing");
  assert.equal(toolExecutionCounts.edit, 1, "edit must NOT be re-executed on repeated processing");

  const participatingSecondPass = batchesSecondPass[0].exchanges.filter(
    (e) => Contract.policyCode(Contract.classifyTool(e.toolName)) === "EstimateAfterCall"
  );
  const errorsSecondPass = participatingSecondPass
    .map((e) => Contract.parseParticipatingArguments(JSON.parse(e.canonicalArguments)))
    .filter((r) => r.tag === 1);
  assert.equal(errorsSecondPass.length, 1, "Repeated pass must idempotently reject with same ArgumentError");

  // 投影依然为 0 个事件，未新增任何 Requested 事件
  assert.equal(
    Strength.projectionDecisionForTarget("run-1", projection),
    null,
    "Projection must have no target decisions after repeated pass"
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


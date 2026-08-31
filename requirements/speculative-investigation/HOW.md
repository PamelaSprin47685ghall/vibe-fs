# speculative-investigation — HOW

## 架构模型与执行流

`speculative-investigation` 在主模型调用管线中以非侵入方式运行：

```text
主模型请求机会 (Opportunity)
  ↓
决策评估 (Eligibility 校验 → 确定性 Control 分流 → 价值方程与预算判定)
  ↓
[K0 / Shadow / 熔断]: 不启动投机，直接执行主请求
[DryRun]: 异步启动真实 Replica 子会话，记录审计日志，主路径立即无等待继续
[Treatment (K1/K2)]:
  启动短生命周期 Replica (fast peer + 只读工具约束)
  ↓
  批量收割只读 call/result → 校验完整配对与字节上限
  ↓
  EventStore.append(StrengthCandidatePrepared) (持久化候选帧引用)
  ↓
  向主模型目标运行注入 Candidate Frames
  ↓
  主模型产出真实输出 → EventStore.append(StrengthCandidatePromoted)
  ↓
  下一轮变换中确定性重建 Promoted Frames 并纳入 XTrace 时间线
```

## 核心机制

### 1. 机会评估与预算分配 (Opportunity & Budgeting)

- **资格准入**：严格校验会话类、主请求类型、角色、未取消状态与模型健康度；任一条件缺失直接降级为 K0。
- **价值方程**：基于预测准确率、主/从模型边际成本、响应延迟与风险项计算期望净收益。仅当收益显著为正且样本量达标时方可批准 K1/K2 预算。
- **确定性对照组**：通过确定性哈希将固定比例的请求分配给 control holdout，保证评估不受运行时随机扰动影响。

### 2. 副本生命周期与权限控制 (Replica Lifecycle & Gate)

- **轻量会话**：创建为附属于 Owner 的内部叶节点子会话，继承 Owner 的 Persona 与 Language，仅将执行绑定重定向至快速模型。
- **严格只读**：工具清单与底层门禁严格限制为 `read`、`glob`、`grep`，任何写操作或越权调用直接被拦截并终止副本。
- **请求预算拦截**：按 provider request 计数，达到预算 K 后立即切断后续外发，防止成本失控。

### 3. 候选物化、持久化与晋升 (Frame Canonicalization & Promotion)

- **Strength-owned 校验与帧展开**：`StrengthProjectionIntent` 校验 decision/target、frame digest、只读工具集合与完整 call/result 配对，再把通过校验的 `ProjectionMessageRow` 交给 `ProjectionIntent.replaceMessageBase` 或 `ProjectionIntent.insertMessageRows`。provider-projection 只负责通用 rows 的规划与渲染，不暴露 Strength 专用构造器。
- **Native tool adapter**：`Strength.Replica.Transform` 消费 `ProjectionMessageEdit.HostWireEncoding.tryEncodeNonToolParts`、`completedToolPart` 与 `rawMessage`，把已验证的 call/result 对编码为 OpenCode completed-tool part；工具配对、角色折叠与 Strength policy 仍归 Strength。
- **在线/重放等价**：`Strength.OpenCode.Speculate` 与 `Replay` 都消费 `ProjectionRenderer.renderMessagesWithHostIds`，并通过 `ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase` 写回；既有 Host object 保持不变。Replica transform 对 renderer rows 使用上述 native adapter，写回后仍由 generic capture 解码为相同 wire semantics。
- **两阶段提交**：
  - 候选帧就绪后，先写入 `StrengthCandidatePrepared` 并将大对象存入 payload_refs；
  - 仅当主模型在目标运行中产生可用输出后，触发 `StrengthCandidatePromoted`；
  - 若目标运行失败、被取消或未产生输出，候选帧自然废弃，不污染语义历史。

### 4. 影子与 DryRun 机制 (Shadow & DryRun)

- **Shadow 模式**：仅执行预测计算并记录特征日志，不启动物理副本，用于收集真实基准数据。
- **DryRun 模式**：启动真实物理子会话执行只读请求以供宿主观测，但完全解耦主路径等待与因果提交逻辑。
- **时间无关 lifecycle**：`StrengthReplicaRuntime` 不注入 timer，也没有 latency/deadline race。Treatment 显式开启后等待 Replica 的 `Completion` 因果终态；DryRun 启动后立即返回，后台只观察 `Completion`。DryRun 若未先由 K gate/Replica terminal 收口，则 `HostTurnObserver` 在 exact Owner `TargetProviderRun` terminal 上调用 `CloseDryRunAtTargetTerminal` 收口。Owner cancel/delete 仍沿现有级联取消路径生效。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| SPEC-INV-001 | `requirements/speculative-investigation/tests/host-canary-k0.test.mjs::WHAT[SPEC-INV-001] STRENGTH_002_011_policy_k0_default_when_host_canary_or_cost_is_unproven` |
| SPEC-INV-002 | `requirements/speculative-investigation/tests/authority-policy.test.mjs::WHAT[SPEC-INV-002] STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_deep_opportunities`；`requirements/speculative-investigation/tests/strength-speculate-surface.test.mjs::WHAT[SPEC-INV-002] StrengthSpeculate owns tryApply entry point for transform speculation` |
| SPEC-INV-003 | `requirements/speculative-investigation/tests/batch-collector.test.mjs::WHAT[SPEC-INV-003] STRENGTH_003_005_collector_preserves_provider_request_batches_and_concurrent_order` |
| SPEC-INV-004 | `requirements/speculative-investigation/tests/authority-policy.test.mjs::WHAT[SPEC-INV-004] STRENGTH_004_019_replica_is_never_owner_fallback_or_prefix_probe_evidence` |
| SPEC-INV-005 | `requirements/speculative-investigation/tests/frame-projection.test.mjs::WHAT[SPEC-INV-005] STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches` |
| SPEC-INV-006 | `requirements/speculative-investigation/tests/commit-promotion.test.mjs::WHAT[SPEC-INV-006] STRENGTH_006_prepared_commit_unknown_is_resolved_without_guessing` |
| SPEC-INV-007 | `requirements/speculative-investigation/tests/turn-evidence.test.mjs::WHAT[SPEC-INV-007] STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping` |
| SPEC-INV-008 | `requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs::WHAT[SPEC-INV-008] STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor`；`requirements/speculative-investigation/tests/strength-replay-surface.test.mjs::WHAT[SPEC-INV-008] StrengthReplay owns applyBeforeXTrace entry point for replay before xtrace` |
| SPEC-INV-009 | `requirements/speculative-investigation/tests/projection-algebra.test.mjs::WHAT[SPEC-INV-009] STRENGTH_006_009_candidate_wrong_target_and_promoted_replica_reflection_conflict`；`requirements/speculative-investigation/tests/projection-adapter.test.mjs::WHAT[SPEC-INV-009] STRENGTH_009_rendered_message_adapter_roundtrips_wire_semantics_with_host_only_ids`；`requirements/speculative-investigation/tests/projection-adapter.test.mjs::WHAT[SPEC-INV-009] STRENGTH_009_host_adapter_encodes_strength_tool_pairs_as_native_completed_OpenCode_parts`；`requirements/speculative-investigation/tests/replica-transform.test.mjs::WHAT[SPEC-INV-009] STRENGTH_003_004_replica_initial_transform_replaces_bootstrap_with_frozen_owner_mirror` |
| SPEC-INV-010 | `requirements/speculative-investigation/tests/authority-policy.test.mjs::WHAT[SPEC-INV-010] STRENGTH_010_value_equations_charge_fast_bytes_delay_and_risk` |
| SPEC-INV-011 | `requirements/speculative-investigation/tests/host-policy.test.mjs::WHAT[SPEC-INV-011] STRENGTH_011_dry_run_is_an_explicit_non_default_host_canary_mode` |
| SPEC-INV-012 | `requirements/speculative-investigation/tests/invisibility.test.mjs::WHAT[SPEC-INV-012] STRENGTH_012_candidate_and_promoted_semantic_bytes_have_no_mechanism_provenance` |
| SPEC-INV-013 | `requirements/speculative-investigation/tests/dry-run-shadow.test.mjs::WHAT[SPEC-INV-013] SPEC_INV_013_DryRun_owner_path_starts_shadow_and_does_not_await_replica_terminal` |

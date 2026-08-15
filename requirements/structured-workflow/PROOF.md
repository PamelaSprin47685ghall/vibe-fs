# structured-workflow — PROOF（测试落点表）

## 1. 命题 → 测试

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| STRUCTURED-WORKFLOW-001（业务流程由宿主语言结构直接表达） | `tests/direct-ce-contract.test.mjs`：`FLOW_001_direct_task_workflow_is_allowed`；`tests/workflow-surface.test.mjs`：`SW_001_workflow_entrypoints_are_the_exported_surface`；REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_005_AgentProgram_publishes_its_flow_entrypoints`、`VERIFY_005_CompanionProgram_publishes_its_flow_entrypoints`、`VERIFY_005_OrchestratorProgram_publishes_exactly_one_entrypoint` | MOVE + NEW + REUSE | `node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs requirements/structured-workflow/tests/workflow-surface.test.mjs` |
| STRUCTURED-WORKFLOW-002（禁止第二业务运行时） | `tests/direct-ce-contract.test.mjs`：`FLOW_006_second_runtime_patterns_are_rejected`；`tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_006`（Domain surface 无 Command/Reply/Trace AST 导出）；REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_second-runtime-protocol_goes_red`、`DSL_OWNERSHIP_negative_business-interpreter_goes_red`、`DSL_OWNERSHIP_negative_flow-lift_goes_red` | MOVE + REUSE | `node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-003（状态标签只表示物理/领域真实事物；无持久程序计数器） | `tests/workflow-surface.test.mjs`：`SW_002_workflow_modules_export_no_program_counter_shaped_names`、`SW_003_domain_flow_and_outcome_types_are_domain_facts`；REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_program-counter_goes_red`、`DSL_OWNERSHIP_negative_behaviour-bool_goes_red`、`DSL_OWNERSHIP_control_state_class_is_a_program_counter` | NEW + REUSE | `node --test requirements/structured-workflow/tests/workflow-surface.test.mjs` |
| STRUCTURED-WORKFLOW-004（ARCH-008 禁止词不作程序计数器） | REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_business_stage_bool_suffix_still_fires_behaviour_bool`（`*Stage/*Phase/*Running/*Spent` 后缀判红）、`DSL_OWNERSHIP_session_mutable_requires_physical_annotation`（business token 表含 State/Phase/Stage/Mode/RunState/Handoff）；`DSL_OWNERSHIP_pascal_member_Pending_still_fires_behaviour_bool` | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-005（组合状态必须可证明合法） | REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_renamed_record_state_axes_are_reported`、`DSL_OWNERSHIP_mutable_record_program_counter_fires_state_product`、`DSL_OWNERSHIP_ref_record_program_counter_fires_mutable_record_field`、`DSL_OWNERSHIP_physical_state_record_mutable_fields_are_allowed`、`DSL_OWNERSHIP_domain_state_combination_is_explicitly_allowed`（fixtures `state-axes-{illegal,domain,physical}.fs`、`mutable-record-program-counter.fs`、`ref-record-program-counter.fs`） | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-006（单一真理源：同构 DU 单一定义） | REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_cross_file_duplicate_case_set_is_violation`、`DSL_OWNERSHIP_single_file_duplicate_case_set_is_not_cross_file`、`DSL_OWNERSHIP_cross_file_duplicate_case_set_exemption_stays_clean` | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-007（纯决策与效果分层） | `tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_001/003/004`（Domain 纯决策面：isTerminalOutcome / decideStep / publishDecision）；REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_negative_infrastructure-leak_goes_red`、`DSL_OWNERSHIP_qualified_infrastructure_reference_is_leak_outside_infra`、`DSL_OWNERSHIP_host_boundary_open_is_not_gate_red`（Host 边界白名单机制）；REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_005_Domain_ReconcileProgram_publishes_pure_decisions` | MOVE + REUSE | `node --test requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-008（mutable/ref 只承载物理资源或局部纯实现） | REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_mutable_requires_dsl_mutable_declaration`、`DSL_OWNERSHIP_unknown_mutable_category_is_rejected`、`DSL_OWNERSHIP_mutable_record_program_counter_fires_mutable_record_field`、`DSL_OWNERSHIP_physical_state_record_mutable_fields_are_allowed`、`DSL_OWNERSHIP_infrastructure_bare_mutable_still_fires`、`DSL_OWNERSHIP_journal_bare_mutable_still_fires` | REUSE | `node --test requirements/structured-workflow/tests/dsl-ownership.test.mjs` |
| STRUCTURED-WORKFLOW-009（恢复重入普通流程） | `tests/recovery-reentry.test.mjs`：`SW_009_reconcile_domain_is_observation_stabilization_not_a_program`、`SW_009_recovery_surface_drives_ordinary_workflow_entrypoints`；`tests/reconcile-program.test.mjs`：`RECONCILE_PROGRAM_005/007`（TurnUnknown 结构性降级，业务边界稳定）；REUSE `requirements/crash-reconciliation/tests/session-recovery-combine.test.mjs`（crash-reconciliation 交叉：恢复组合） | NEW + MOVE + REUSE | `node --test requirements/structured-workflow/tests/recovery-reentry.test.mjs requirements/structured-workflow/tests/reconcile-program.test.mjs` |
| STRUCTURED-WORKFLOW-010（有界循环与有界扇出） | `tests/parallel.test.mjs`：12 个 `ARCH_009_*` 锚点（结果序按输入下标 / 并发上限 / 空输入短路 / 取消 / 拒绝 / 非正 max 拒绝）；REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_005_the_Parallel_kernel_publishes_only_bounded_parallelism`（无 unbounded `Parallel.map*` 旁路） | MOVE + REUSE | `node --test requirements/structured-workflow/tests/parallel.test.mjs` |
| STRUCTURED-WORKFLOW-011（Semantic Vocabulary 是领域事实词汇） | `tests/semantic-vocabulary.test.mjs`：`SW_011_named_vocabulary_surface_exists_in_Application`、`SW_011_vocabulary_names_declare_business_promises_not_implementation_actions` | NEW | `node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-012（Semantic Compression 必须有 proof） | `tests/semantic-vocabulary.test.mjs`：`SW_011_named_vocabulary_surface_exists_in_Application`（被压缩 Vocabulary 的存在面）；proof 义务表见 HOW.md §3.4（每个高阶 Vocabulary 必须有 temporal/behavioral proof；正交组合人工证明见 §3.4.1） | NEW + HOW | `node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-013（Decorator 边界：transparent vs semantic） | `tests/semantic-vocabulary.test.mjs`：`SW_015_no_anonymous_middleware_framework_in_workflow_vocabulary` | NEW | `node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs` |
| STRUCTURED-WORKFLOW-014（流程正确性由可观察效果证明） | REUSE `requirements/verification-system/tests/guide-contract.test.mjs`：`VERIFY_008_every_emitted_module_actually_loads`（导出面即契约）；REUSE `tests/unit/temporal/**`（finality-cohort-law / fallback-aabb-confluence / manager-unhappy-exactly-once / join-guard-wakeup / orchestrator-conflict-confluence / until-signal-or-deadline：可观察效果轨迹证明，无解释器节点指针）；REUSE `requirements/structured-workflow/tests/dsl-ownership.test.mjs`：`DSL_OWNERSHIP_threshold_freeze_semantics` | REUSE | `node --test requirements/verification-system/tests/guide-contract.test.mjs` |
| STRUCTURED-WORKFLOW-015（取消是控制面，不是业务数据） | REUSE `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs`：`P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal`、`P0_RECOVERY_JOIN_001_joinable_completion_has_no_fromAborted_export`（effect-accounting 拥有 outcome 代数；本命题钉控制面/数据面分离） | REUSE | `node --test requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs` |
| STRUCTURED-WORKFLOW-016（控制决策不得形成 lexical pyramid） | `tests/fsharp-control-pyramid.test.mjs`：nested match RED、match→if→try RED、flat/tuple/if-elif GREEN、comment/string lexical shielding、per-file ratchet、production baseline exact、单次 repair manual + 教程篇幅下限；`tests/error-handling-vocabulary.test.mjs`：FsToolkit Result vocabulary、Fable-compatible TaskResult CE、WriterStreamSync 代表性糖化 | NEW | `node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs` |

## 2. 本包拥有的测试文件（全部单跑绿）

| 文件 | 来源 | 状态 |
|---|---|---|
| `tests/direct-ce-contract.test.mjs` | MOVE `requirements/structured-workflow/tests/direct-ce-contract.test.mjs` | 已跑绿（2 pass） |
| `tests/parallel.test.mjs` | MOVE `requirements/structured-workflow/tests/parallel.test.mjs` | 已跑绿（12 pass） |
| `tests/reconcile-program.test.mjs` | MOVE `requirements/structured-workflow/tests/reconcile-program.test.mjs` | 已跑绿（6 pass） |
| `tests/workflow-surface.test.mjs` | NEW | 已跑绿（3 pass） |
| `tests/recovery-reentry.test.mjs` | NEW | 已跑绿（2 pass） |
| `tests/semantic-vocabulary.test.mjs` | NEW | 已跑绿（3 pass） |
| `tests/fsharp-control-pyramid.test.mjs` | NEW | 已跑绿（11 pass；production baseline=742） |
| `tests/error-handling-vocabulary.test.mjs` | NEW | 已跑绿（3 pass；Fable build 同步通过） |

## 3. 单跑命令

```text
node --test requirements/structured-workflow/tests/direct-ce-contract.test.mjs
node --test requirements/structured-workflow/tests/parallel.test.mjs
node --test requirements/structured-workflow/tests/reconcile-program.test.mjs
node --test requirements/structured-workflow/tests/workflow-surface.test.mjs
node --test requirements/structured-workflow/tests/recovery-reentry.test.mjs
node --test requirements/structured-workflow/tests/semantic-vocabulary.test.mjs
node --test requirements/structured-workflow/tests/fsharp-control-pyramid.test.mjs
node --test requirements/structured-workflow/tests/error-handling-vocabulary.test.mjs
```

## 4. REUSE 落点（留在原处，SPLIT@cutover）

| 现有测试 | 本包锚点 | cutover 计划 |
|---|---|---|
| `requirements/structured-workflow/tests/dsl-ownership.test.mjs`（726 行） | 全部 `DSL_OWNERSHIP_*` 锚点（第二运行时、program-counter、behaviour-bool、mutable、state-product、dup-cases、infrastructure-leak、bool-loop、registry-joint-branch、ControlState reason、大 DU 分类、Host 边界白名单） | SPLIT@cutover：positive 结构门锚点归本包；`program-counter` 词表与 `behaviour-bool` 名称正则的 **legacy symbol blacklist 部分 DELETE**（migration ratchet，见 PROOF-MAP dsl-ownership SPLIT 行） |
| `requirements/structured-workflow/tests/dsl-ownership-ratchet.test.mjs`（258 行） | `DSL_OWNERSHIP_RATCHET_*` 锚点（per-file/gate 计数基线防回归） | SPLIT@cutover：ratchet 是 legacy violation 计数基线，随 legacy blacklist 一起 DELETE；positive 结构门由 dsl-ownership `--threshold=0` 承担 |
| `requirements/structured-workflow/tests/g4r-ce-vocabulary.test.mjs`（161 行） | `G4R_CE_S0_documents_obsolete_controller_paths`、`G4R_CE_S14_production_is_clean_in_hard_phase`（CE vocabulary absence ratchet = 本包）；`G4R_CE_S0_raw_time_*`（raw-time 扫描 = time-capability 交叉） | SPLIT@cutover：obsolete-controller absence ratchet 基线稳定后弱化/删除；raw-time 部分移交 time-capability |
| `requirements/verification-system/tests/guide-contract.test.mjs`（顶层） | `VERIFY_005_*`（直接 CE 入口导出面）、`VERIFY_005_the_Parallel_kernel_publishes_only_bounded_parallelism`、`VERIFY_005_Domain_ReconcileProgram_publishes_pure_decisions`、`VERIFY_005_the_outcome_kernel_publishes_the_two_commit_results`、`VERIFY_008_*` | SPLIT@cutover：verification-system 拥有 harness；各语义断言按导出面归属各包 |
| `requirements/crash-reconciliation/tests/session-recovery-combine.test.mjs` | 恢复组合（permit → 普通流程） | SPLIT@cutover：crash-reconciliation 拥有恢复协议；「无执行位置恢复」半边由本包 `tests/recovery-reentry.test.mjs` 承担 |
| `requirements/effect-accounting/tests/join-aborted-not-terminal.test.mjs` | `P0_RECOVERY_JOIN_001_*`（aborted 非终态） | SPLIT@cutover：effect-accounting 拥有 outcome 代数；本命题只钉控制面/数据面分离 |
| `tests/unit/temporal/**`（6 文件） | finality-cohort-law / fallback-aabb-confluence / manager-unhappy-exactly-once / join-guard-wakeup / orchestrator-conflict-confluence / until-signal-or-deadline（可观察效果轨迹 = FLOW-008 证明姿态） | SPLIT@cutover：time-capability（fake clock/virtual timer）+ causal-wait（until-signal-or-deadline）各取所属断言；本包保留「以可观察效果证明流程」的证明姿态 |
| `tests/unit/enforcer/blogger-convergence-gaps.test.mjs` | C0 断言：`HasFlight` 唯一 busy、无 shadow state API（DSL-005/009 人工 proof 的机器下限） | SPLIT@cutover：behavior-diagnosis 保留 enforcer 面；single-flight 物理事实断言归 structured-workflow（或由本包 NEW 测试接替） |

## 5. semantic anchor id

`scripts/checks/semantic-anchors.mjs` 中**没有**直接归本包的 semantic ID：
锚点目录只声明 `cognitive-environment` / `office-capability` / `action-affordance` /
`epistemic-reasoning` / `review-judgement` 五类 owner，本包语义由 F# 类型 + 上述
positive 结构测试承担。若 cutover 后需要散文 canary，建议新增 `STRUCTURED_WORKFLOW_*`
锚点并声明 owner 为本包。

## 6. cutover 待办

- [ ] 删除 `dsl-ownership` 的 legacy symbol blacklist 部分（`program-counter` 词表、
  `behaviour-bool` 名称正则）与其 ratchet 基线（PROOF-MAP DELETE 清单）；positive
  结构门保留为 `--threshold=0`。
- [ ] `g4r-ce-vocabulary` obsolete-controller absence ratchet 基线稳定后弱化；raw-time
  扫描移交 `time-capability`。
- [ ] `g4r-freeze`（migration freeze ratchet）不归本包，由 lead 按 PROOF-MAP DELETE 处理。
- [ ] `tests/unit/verify/*`、`guide-contract`、`temporal/**` 的 SPLIT@cutover 按 §4 表执行。

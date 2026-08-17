# delegation — 证明落点

规则：每条 WHAT 命题恰好一行落点。`MOVE` = 已物理移入本包 `tests/`；`REUSE` = 留在原处，
cutover 时按 `SPLIT@cutover` 拆分；`NEW` = 新写。运行命令：`node --test <file>`。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| DELEG-001 委托=charge+office+owner+returned consequence | `scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.manager`（`entrust-by-consequence`/`choose-by-return`/`no-omnipotent-charge` 双语言命中）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`manager_role_law_entrusts_by_consequence_not_persona`——真实 manager Role Law 双语文档锚点命中） | REUSE + NEW | `node scripts/checks/semantic-anchors.mjs`; `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-002 calling 只差 persona/depth 不差 authority | anchor `persona-not-authority`（fork 组，双语言命中）；`requirements/participant-identity/tests/catalog.test.mjs`（`participant-identity` 交叉，persona/binding 分离）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`calling_names_differ_in_persona_depth_not_authority`——真实 fork description 双语文档锚点命中） | REUSE + NEW | `node scripts/checks/semantic-anchors.mjs`; `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-003 独立 road vs same-road continuation | `requirements/delegation/tests/fork-tool.test.mjs::FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname` | NEW | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-004 commission ≠ fork | anchor `office-not-witness`（fork 组）；`scripts/checks/tool-referential-integrity.mjs`（same-name-same-contract）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`commission_and_fork_are_distinct_contracts_not_witness`——office-not-witness 锚点 + fork/commission 角色门不同名） | REUSE + NEW | `node scripts/checks/tool-referential-integrity.mjs`; `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-005 机器拓扑不进委托面 | `requirements/delegation/tests/join-v2-wire.test.mjs::JOIN_V2_completed_agent_is_natural_language_plus_work_record`; `requirements/delegation/tests/join-v2-wire.test.mjs::JOIN_V2_rendered_wire_is_parseable_without_legacy_fields` | REUSE | `node --test requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-006 fork 成功仅 Byname；续做沿用 binding | `requirements/delegation/tests/fork-tool.test.mjs::FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier` | NEW | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-007 SyncDelegate DAG 无环 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs`（嵌套 `DevOps→Coder→Inspector` 无 deadlock 场景；DAG 静态证明 gate `scripts/checks/dsl-ownership.mjs` 交叉）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`sync_delegate_edges_are_the_allowed_dag_only`——Roles 权限表决定委托边 Inquiry/Coder/DevOps→Inspector、DevOps→Coder，反向边拒绝，DFS 环检测证明无环） | REUSE + NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-008 batch 由 Host tool-call 集合决定 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_provider_tool_call_collection_preserves_role_order`、`SYNC_RUNTIME_unknown_role_and_outcome_fail_closed_at_every_entry` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-009 key=immediate caller ReuseScope；overlap fail closed | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-010 tier 确定性映射 | `requirements/delegation/tests/sync-delegate.test.mjs`（`EXEC_026_tierForOwner_is_identity_for_fast_and_deep`、`EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder`）；`requirements/delegation/tests/sync-delegate-runtime.test.mjs`（`EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names`、`EXEC_026_sync_delegate_reuse_keeps_deep_inspector_when_owner_later_fast`） | REUSE | `node --test requirements/delegation/tests/sync-delegate.test.mjs requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-011 无 return 通道；ordinary completion 收口 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_ordinary_completion_settles_batch_without_return_channel` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-012 canonical 得 WorkRecord、siblings 引用 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_first_provider_call_receives_canonical_record_and_sibling_receives_reference` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-013 Join 有界批次/稳定排序/逐项 CAS；子→父 LWR=`# LWR` | `requirements/delegation/tests/join-v2-mailbox.test.mjs`（`EXEC_018_max_join_batch_is_32`、`EXEC_018_thirty_three_completions_split_across_two_drains`、`EXEC_018_drained_batch_has_unique_agent_ids`）；`requirements/delegation/tests/join-v2-wire.test.mjs`（硬锁：`EXEC_004_child_to_parent_lwr_is_hashed_comment_not_toml_field`、`EXEC_004_work_record_lines_are_hash_prefixed_including_malicious`、`EXEC_004_work_record_is_not_a_toml_field_when_lwr_present`——子→父禁止 `work_record =`，必须 `SyntheticToml.comment`；`JOIN_V2_malformed_role_run_or_kind_is_rejected_without_success_wire`） | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-014 commission 批量 join 同界 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` `EXEC_019_verdict_mailbox_try_join_batch_preserves_publish_fifo`；`requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_019_orchestrator_batch_is_natural_language_only` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-015 join 中断 = Interrupted 非 ForkError | `requirements/delegation/tests/join-v2-mailbox.test.mjs`（`EXEC_017_wait_for_signal_user_message_returns_user_message_arrived`、`EXEC_017_user_message_interrupt_does_not_cancel_mailbox`、`EXEC_017_join_attempt_old_signal_does_not_bleed_into_next_join`）；`requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_017_interrupted_wire_is_natural_language_not_error` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-016 horizon pull-only snapshot | `requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_004_join_prefers_durable_byname_over_machine_agent_name`；horizon 无 watcher 断言见 `tests/unit/host/` horizon 面（cross-check） | REUSE | `node --test requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-017 返回只改认识不转 authority | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_work_record_is_evidence_and_does_not_transfer_authority` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-018 NEEDHELP consultation = 真实 child 委托 | `requirements/delegation/tests/assistance-host.test.mjs::ASSISTANCE_HOST_needhelp_escalation_keeps_the_same_authority_root`、`ASSISTANCE_HOST_needhelp_advice_is_not_a_provider_retry`（JS-native authority surface）；`requirements/host-boundary/tests/needhelp-sensor.test.mjs`（sentinel 识别，SPLIT：识别归 `interaction-authority`）；`requirements/verification-system/tests/e2e/entry.test.mjs`（真实 Long Stroke 的 consultation/advice 路由） | REUSE + NEW | `node --test requirements/delegation/tests/assistance-host.test.mjs requirements/host-boundary/tests/needhelp-sensor.test.mjs`；Long Stroke e2e |
| DELEG-019 fork child 首 prompt typed 载荷（父→子 LWR=TOML field） | `tests/fork-child-payload.test.mjs`（`FORK_CHILD_PAYLOAD_*` 全组；硬锁：`FORK_CHILD_PAYLOAD_commissioner_record_is_toml_data_field`、`FORK_CHILD_PAYLOAD_commissioner_lwr_is_toml_field_not_hashed_instructions`——父→子 LWR 在 `commissioner_record` 字段、禁止 `# Opening`/`# Chronicle`/裸 prose/`parent_work_record`）；`tests/handle-exe008-child-background.test.mjs` `EXEC_008_child_background_uses_latest_durable_snapshot`；方向互补见 DELEG-013 join `# LWR` 硬锁 | MOVE | `node --test requirements/delegation/tests/fork-child-payload.test.mjs requirements/delegation/tests/handle-exe008-child-background.test.mjs` |
| DELEG-020 语义不依赖工具名 | 命题结构本身（HOW.md「历史与弃权」）；`requirements/delegation/tests/delegation-structure-contract.test.mjs`（`delegation_semantics_do_not_depend_on_current_tool_names`——HOW 声明工具名是当前选择、改名不动 WHAT，且五个当前名字真实存在） | NEW | `node --test requirements/delegation/tests/delegation-structure-contract.test.mjs` |
| DELEG-021 fork attachment | `tests/fork-attachment.test.mjs`（background / TOML field / blank / anti-assignment）；`tests/fork-tool.test.mjs` `DELEG_021_*`（unknown/self 前置拒绝、fresh LWR attachment；physical-accepted ActiveLogicalRun 才可 busy nudge，Detached 尚未 `chat.message` 时立即 deferred、不等待 acceptance、不物化 attachment） | NEW + REUSE | `node --test requirements/delegation/tests/fork-attachment.test.mjs requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-022 delegated expected tool calls | `tests/delegated-tool-estimate-surface.test.mjs`（pure replace/decrement/idempotence/saturation + no scan/mutable，JSON-shaped state）；`tests/delegated-tool-estimate-facts.test.mjs`（durable fold）；`tests/delegation-tool-contract.test.mjs`（五个 surface + no maxSteps）；`tests/fork-tool.test.mjs` `DELEG_022_*`（invalid / replace / omitted retain）；`tests/sync-delegate-tools.test.mjs` `DELEG_022_*`（batch sum / reusable omission retain）；交叉 `requirements/guidance-delivery/tests/pair-calibration.test.mjs` | NEW + REUSE（FROZEN 2026-08-14，surface 迁移保持 oracle 语义） | 实现后不改 oracle |
| DELEG-023 委托失败仅在恢复路径耗尽后报告 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs::SYNC_RUNTIME_transient_turn_failure_stays_child_local_until_exhausted` | NEW | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |

## GAP

| GAP | 待建命题 | 缺口 | 状态 | 关闭条件 |
|---|---|---|---|---|
| GAP-011 | DELEG-021 fork attachment | 正式 WHAT + 独立 frozen oracle + production wiring 均已落地 | CLOSED | `tests/fork-attachment.test.mjs` + `tests/fork-tool.test.mjs`；按用户要求 frozen 后未执行；full build 被 unrelated Fission parse error 阻塞 |
| GAP-012 | DELEG-022 delegated expected tool calls | 正式 WHAT + 独立 frozen oracle + typed facts/fold/surfaces/HOST-013 wiring 均已落地；无 transcript/XTrace scan、业务 mutable counter、enforcement | CLOSED | `tests/delegated-tool-estimate*.test.mjs` + `tests/delegation-tool-contract.test.mjs` + fork/SyncDelegate reuse/batch oracle；按用户要求 frozen 后未执行；相关静态 gates 绿，full build 被 unrelated Fission parse error 阻塞 |

## 移动文件清单

| 源 | 目标 | 结果 |
|----|------|------|
| `requirements/delegation/tests/fork-child-payload.test.mjs` | `requirements/delegation/tests/fork-child-payload.test.mjs` | `node --test` 绿（21/21；import 深度已适配） |

## SPLIT@cutover 清单（REUSE 文件拆分计划）

- `requirements/delegation/tests/sync-delegate-runtime.test.mjs`：DELEG-008..012 断言归本包；create/reuse/abort/retire/
  级联断言 → `managed-session-lifecycle`；attachment/SessionOwnership 断言 → `session-ontology`。
- `requirements/delegation/tests/sync-delegate-ce-collapse.test.mjs`：EXEC-031 单栈断言归本包；dispose/cancel scope →
  `managed-session-lifecycle`。
- `requirements/delegation/tests/fork-tool.test.mjs`：FORK_* 语义断言归本包；tool spec/registry 断言 →
  `capability-enforcement`；office 后果表 → `office-capability`。
- `requirements/delegation/tests/sync-delegate-tools.test.mjs`：inspect/establish/repair 合同断言归本包；warm-start
  prepare 断言 → `knowledge-reuse`/`repository-investigation`。
- `tests/unit/execution/join-*.test.mjs`：join batch/wire 归本包；`join-aborted-not-terminal` →
  `effect-accounting`；`timer-port`/`devops-join-timeout` → `time-capability`；`handle*` →
  `managed-session-lifecycle`；`join-recovery-*` → `crash-reconciliation`；`join-guard` →
  `interaction-authority`/`finality`；join wire 的 WorkRecord 物化 → `work-record`。
- `tests/unit/host/{needhelp-sensor,assistance-host}.test.mjs`：consultation 委托语义归本包；sentinel 识别/
  assistance abort 分型 → `interaction-authority`；advice prompt 渲染 → `provider-projection`/`prefix-stability`。
- `tests/unit/orchestrator/{host,runtime}.test.mjs`：commission 委托面断言归本包；job/rebase/publish/
  CAS/recovery 断言 → `change-integration`；review barrier → `review-assurance`。

## 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.manager`：`entrust-by-consequence`、`choose-by-return`、`no-omnipotent-charge`、
`returned-record`。
`ROLE_SEMANTIC_ANCHORS.orchestrator`：`owns-roads`、`same-road-continuation`、`independent-destination`。
`TOOL_DESCRIPTION_ANCHORS.fork`：`office-not-witness`、`create-and-continue`（`persona-not-authority` 的
personhood 部分由 `participant-identity` 交叉声明）。
`TOOL_DESCRIPTION_ANCHORS.inspect`：`repository-fact`、`causal-readonly`、`no-code-changes`、
`no-behavioral-execution`、`no-implement-or-repair`。
`TOOL_DESCRIPTION_ANCHORS.commission`：`independent-road`、`not-lifecycle-stage`。
`TOOL_DESCRIPTION_ANCHORS.establish-behavior`：`coder-writes-source`、`not-execution-evidence`。
`TOOL_DESCRIPTION_ANCHORS.repair-behavior`：`meaning-decided`、`not-passing-proof`。
# delegation — 证明落点

规则：每条 WHAT 命题恰好一行落点。`MOVE` = 已物理移入本包 `tests/`；`REUSE` = 留在原处，
cutover 时按 `SPLIT@cutover` 拆分；`NEW` = 新写。运行命令：`node --test <file>`。

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| DELEG-001 委托=charge+office+owner+returned consequence | `scripts/checks/semantic-anchors.mjs` `ROLE_SEMANTIC_ANCHORS.manager`（`entrust-by-consequence`/`choose-by-return`/`no-omnipotent-charge` 双语言命中） | REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DELEG-002 calling 只差 persona/depth 不差 authority | anchor `persona-not-authority`（fork 组，双语言命中）；`requirements/participant-identity/tests/catalog.test.mjs`（`participant-identity` 交叉，persona/binding 分离） | REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DELEG-003 独立 road vs same-road continuation | anchors `same-road-continuation`/`independent-destination`/`independent-road`/`not-lifecycle-stage`；`requirements/delegation/tests/fork-tool.test.mjs` `FORK_orchestrator_resolves_continuation_by_road_byname` | REUSE | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-004 commission ≠ fork | anchor `office-not-witness`（fork 组）；`scripts/checks/tool-referential-integrity.mjs`（same-name-same-contract） | REUSE | `node scripts/checks/tool-referential-integrity.mjs` |
| DELEG-005 机器拓扑不进委托面 | `requirements/delegation/tests/fork-tool.test.mjs`（`FORK_calling_creates_machine_agent_but_returns_only_byname`、`FORK_unknown_byname_does_not_echo_internal_identity`）；`requirements/delegation/tests/join-v2-wire.test.mjs` | REUSE | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-006 fork 成功仅 Byname；续做沿用 binding | `requirements/delegation/tests/fork-tool.test.mjs`（`FORK_existing_person_is_resolved_by_byname_not_agent_id`、`FORK_engineer_continuation_keeps_deep_coder`、`FORK_same_byname_cannot_be_reborn_with_a_new_calling`） | REUSE | `node --test requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-007 SyncDelegate DAG 无环 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs`（嵌套 `DevOps→Coder→Inspector` 无 deadlock 场景；DAG 静态证明 gate `scripts/checks/dsl-ownership.mjs` 交叉） | REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-008 batch 由 Host tool-call 集合决定 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs`（`DELEG_008_provider_batch_observation_deduplicates_parts_and_preserves_host_order`、`EXEC_026_sync_delegate_provider_batch_coalesces_without_race_and_returns_once`）；`requirements/delegation/tests/sync-delegate-tools.test.mjs` `DELEG_008_inspect_batch_waits_for_complete_host_tool_call_set_before_dispatch`；物理交叉：唯一 Long Stroke `g2-inspector-batch-owner` + `assertG2InspectorBatchCoalescing` | NEW+REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs requirements/delegation/tests/sync-delegate-tools.test.mjs`; `PATH="$PWD/node_modules/.bin:$PATH" node requirements/verification-system/tests/e2e/entry.test.mjs` |
| DELEG-009 key=immediate caller ReuseScope；overlap fail closed | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` `EXEC_026_sync_delegate_different_run_overlap_is_rejected_not_queued`、`G2_inspector_Q1_Q2_Q3_same_session_serial_reuse` | REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-010 tier 确定性映射 | `requirements/delegation/tests/sync-delegate.test.mjs`（`EXEC_026_tierForOwner_is_identity_for_fast_and_deep`、`EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder`）；`requirements/delegation/tests/sync-delegate-runtime.test.mjs`（`EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names`、`EXEC_026_sync_delegate_reuse_keeps_deep_inspector_when_owner_later_fast`） | REUSE | `node --test requirements/delegation/tests/sync-delegate.test.mjs requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-011 无 return 通道；ordinary completion 收口 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs`（`EXEC_031_completed_without_bounded_work_record_fails_closed`）；`requirements/delegation/tests/sync-delegate-tools.test.mjs` | REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-012 canonical 得 WorkRecord、siblings 引用 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs` `EXEC_031_bounded_work_record_answers_in_recent_work_not_raw_message`；`requirements/delegation/tests/sync-delegate-tools.test.mjs`（merged-reference wire） | REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs` |
| DELEG-013 Join 有界批次/稳定排序/逐项 CAS | `requirements/delegation/tests/join-v2-mailbox.test.mjs`（`EXEC_018_max_join_batch_is_32`、`EXEC_018_thirty_three_completions_split_across_two_drains`、`EXEC_018_drained_batch_has_unique_agent_ids`）；`requirements/delegation/tests/join-v2-wire.test.mjs` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-014 commission 批量 join 同界 | `requirements/delegation/tests/join-v2-mailbox.test.mjs` `EXEC_019_verdict_mailbox_try_join_batch_preserves_publish_fifo`；`requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_019_orchestrator_batch_is_natural_language_only` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-015 join 中断 = Interrupted 非 ForkError | `requirements/delegation/tests/join-v2-mailbox.test.mjs`（`EXEC_017_wait_for_signal_user_message_returns_user_message_arrived`、`EXEC_017_user_message_interrupt_does_not_cancel_mailbox`、`EXEC_017_join_attempt_old_signal_does_not_bleed_into_next_join`）；`requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_017_interrupted_wire_is_natural_language_not_error` | REUSE | `node --test requirements/delegation/tests/join-v2-mailbox.test.mjs` |
| DELEG-016 horizon pull-only snapshot | `requirements/delegation/tests/join-v2-wire.test.mjs` `EXEC_004_join_prefers_durable_byname_over_machine_agent_name`；horizon 无 watcher 断言见 `tests/unit/host/` horizon 面（cross-check） | REUSE | `node --test requirements/delegation/tests/join-v2-wire.test.mjs` |
| DELEG-017 返回只改认识不转 authority | anchor `returned-record`（manager 组）；`requirements/delegation/tests/sync-delegate-tools.test.mjs`（`INSPECT_happy_path_invokes_inspector_and_returns_work_record`——返回 evidence 而非 mutation） | REUSE | `node --test requirements/delegation/tests/sync-delegate-tools.test.mjs` |
| DELEG-018 NEEDHELP consultation = 真实 child 委托 | `requirements/host-boundary/tests/needhelp-sensor.test.mjs`（sentinel 识别，SPLIT：识别归 `interaction-authority`）；`requirements/delegation/tests/assistance-host.test.mjs`（consultation 委托/advice 路由，本包部分）；历史 change（increase-strength）§8–10 为考古 | REUSE | `node --test requirements/host-boundary/tests/needhelp-sensor.test.mjs requirements/delegation/tests/assistance-host.test.mjs` |
| DELEG-019 fork child 首 prompt typed 载荷 | `tests/fork-child-payload.test.mjs`（`FORK_CHILD_PAYLOAD_*` 全组；硬锁：`FORK_CHILD_PAYLOAD_commissioner_record_is_prose_with_instruction`、`FORK_CHILD_PAYLOAD_commissioner_lwr_stays_body_prose_not_hashed_instructions`——LWR 在 body prose、禁止 `# Opening`/`# Chronicle`/`parent_work_record`）；`tests/handle-exe008-child-background.test.mjs` `EXEC_008_child_background_uses_latest_durable_snapshot` | MOVE | `node --test requirements/delegation/tests/fork-child-payload.test.mjs requirements/delegation/tests/handle-exe008-child-background.test.mjs` |
| DELEG-020 语义不依赖工具名 | 命题结构本身（HOW.md「历史与弃权」）；无独立断言（改名不破坏任何断言 = 命题的证明） | — | — |
| DELEG-021 fork attachment | `tests/fork-attachment.test.mjs`（`DELEG_021_attachment_is_background_between_commissioner_and_requirements`、`DELEG_021_attachment_lwr_stays_body_prose_not_hashed_instructions`、blank/anti-assignment）；`tests/fork-tool.test.mjs` `DELEG_021_*`（unknown/self 在 child 创建前拒绝、fresh LWR attachment、busy reuse 不物化） | NEW + REUSE | `node --test requirements/delegation/tests/fork-attachment.test.mjs requirements/delegation/tests/fork-tool.test.mjs` |
| DELEG-022 delegated expected tool calls | `tests/delegated-tool-estimate.test.mjs`（pure replace/decrement/idempotence/saturation + no scan/mutable）；`tests/delegated-tool-estimate-facts.test.mjs`（durable fold）；`tests/delegation-tool-contract.test.mjs`（五个 surface + no maxSteps）；`tests/fork-tool.test.mjs` `DELEG_022_*`（invalid / replace / omitted retain）；`tests/sync-delegate-tools.test.mjs` `DELEG_022_*`（batch sum / reusable omission retain）；交叉 `requirements/guidance-delivery/tests/pair-calibration.test.mjs` | NEW + REUSE（FROZEN 2026-08-14） | **按用户要求冻结后未执行**；实现后不改 oracle |
| DELEG-023 委托失败仅在恢复路径耗尽后报告 | `requirements/delegation/tests/sync-delegate-runtime.test.mjs`（`EXEC_033_*`）；`requirements/delegation/tests/sync-delegate-tools.test.mjs`（`INSPECT/CODER retry`）；`requirements/delegation/tests/assistance-host.test.mjs`（`AGENT_031 consultation child retry`） | REUSE | `node --test requirements/delegation/tests/sync-delegate-runtime.test.mjs requirements/delegation/tests/sync-delegate-tools.test.mjs requirements/delegation/tests/assistance-host.test.mjs` |

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
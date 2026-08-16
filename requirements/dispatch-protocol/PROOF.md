# PROOF —— 测试落点表（dispatch-protocol）

## 运行方式

```bash
node --test requirements/dispatch-protocol/tests/fire-and-forget.test.mjs   # MOVE（原 requirements/dispatch-protocol/tests/fire-and-forget.test.mjs）
node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs  # NEW
node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs  # NEW
# 全量：node requirements/verification-system/tests/run.mjs（自动包含 requirements/**/tests/*.test.mjs）
```

## 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| R1 | DISPATCH-PROTOCOL-002/003/004 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_002_submit_records_the_receipt_without_resolving_the_claim` + `DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone` + `DP_002_claim_records_payload_digest_and_effective_agent` + `DP_003_receipt_shape_distinguishes_admission_from_physical_identity`（`accepted-*` 只是 admission，`msg_*` 才是物理证据；recovery 侧见 R4 `DP_004_physical_acceptance_is_proven_only_by_physical_message`） | NEW | `node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs` |
| R2 | DISPATCH-PROTOCOL-005/006/010 | `claim-lifecycle.test.mjs::DP_005_prompt_key_is_deterministic_and_moves_with_every_component` + `DP_005_claim_scope_names_exactly_session_run_origin_and_payload` + `DP_010_authority_root_profile_cannot_express_a_model` | NEW | 同上 |
| R3 | DISPATCH-PROTOCOL-006/007 | `claim-lifecycle.test.mjs::DP_006_claim_sequence_advances_on_registration_not_on_resolution` + `DP_006_abandon_keeps_the_claim_sequence_consumed` + `DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority` | NEW | 同上 |
| R4 | DISPATCH-PROTOCOL-007/008 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::DP_008_unproven_outcome_stays_pending_never_resends`（StillPending + 绝不重发）+ `DP_004_physical_acceptance_is_proven_only_by_physical_message`（Proven）+ `DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool` | NEW | `node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs` |
| R5 | DISPATCH-PROTOCOL-009 | `requirements/dispatch-protocol/tests/fire-and-forget.test.mjs`：`PROMPT_007_detached_claims_and_persists_without_physical_accepted`、`PROMPT_007_detached_returns_even_when_session_send_task_never_settles`（连 `ISessionHostPort.SendPrompt` Task settle 都不等）、`PROMPT_007_detached_sdk_physical_id_does_not_race_chat_message_acceptance`（SDK early id 不抢写 PhysicalAccepted）、`PROMPT_007_detached_continuation_same_claim_path`；`requirements/execution-model-routing/tests/open-code-port-routing.test.mjs::EMR_004_sdk_prompt_async_enqueue_never_waits_for_the_host_run_promise`；`requirements/delegation/tests/fork-tool.test.mjs::FORK_deep_devops_returns_after_enqueue_even_when_host_prompt_promise_never_settles`（真实 fork 不等 slot/run） | MOVE + REUSE | `node --test requirements/dispatch-protocol/tests/fire-and-forget.test.mjs requirements/execution-model-routing/tests/open-code-port-routing.test.mjs requirements/delegation/tests/fork-tool.test.mjs` |
| R6 | DISPATCH-PROTOCOL-001/011 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime`（全部 user-shaped send 成员都是 `PromptDispatcher.Runtime` 成员，无 FireAndForget 旁路）+ `requirements/dispatch-protocol/tests/send-format.test.mjs::PROMPT_006_send_payload_carries_prompt_key_metadata`（Metadata/PromptKey 锚；Model=None 半边见 R2/R10） | REUSE | `node --test requirements/dispatch-protocol/tests/send-format.test.mjs` |
| R7 | DISPATCH-PROTOCOL-007（claim 释放可重试） | `requirements/dispatch-protocol/tests/join-guard.test.mjs::JNGD_nudge_releases_the_key_when_send_fails_and_retries`（send 失败 → Abandon(SendFailed) 释放 key；JoinGuard continuation 语义归 interaction-authority） | REUSE | `node --test requirements/dispatch-protocol/tests/join-guard.test.mjs` |
| R8 | DISPATCH-PROTOCOL-002（historical stamp） | `requirements/dispatch-protocol/tests/runtime-start-watermark.test.mjs::PROMPT_011_RuntimeStarted_advances_a_workspace_watermark_not_every_session`（纯 fold：stamp 仍可审计；restart-budget API 不存在） | REUSE | `node --test requirements/dispatch-protocol/tests/runtime-start-watermark.test.mjs` |
| R9 | DISPATCH-PROTOCOL-001/005/006（claim 唯一写入口 + 幂等身份） | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_005_claim_scope_names_exactly_session_run_origin_and_payload` + `DP_005_prompt_key_is_deterministic_and_moves_with_every_component` + `DP_006_claim_sequence_advances_on_registration_not_on_resolution` + `DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime` | REUSE | `node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs` |
| R10 | DISPATCH-PROTOCOL-007（restart 不产生 terminal） | `claim-lifecycle.test.mjs::DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority` + `recovery-at-most-one.test.mjs::DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool` | NEW | 两文件各自命令 |

统计：10 行落点；21 个 active test 全绿（claim-lifecycle 11 + recovery-at-most-one 3 + fire-and-forget 3 + send-format 2 + runtime-start-watermark 1 + join-guard 1）。

## authority.test.mjs SPLIT（本包半边）已执行（Wave 2a）：锚点并入 claim-lifecycle / send-format

双 owner 文件：**REUSE + SPLIT@cutover**（完整 22 测试归属表见
[`interaction-authority/PROOF.md`](../interaction-authority/PROOF.md)）。本包 cutover 时接收：

| authority.test.mjs 测试 | cutover 动作 |
|---|---|
| `PROMPT_001_a_transport_receipt_can_never_become_an_authority_root` | 接收 `isAdmissionShaped` 形态断言（crossing 缺席断言留 interaction-authority） |
| `PROMPT_002_authority_root_profile_cannot_express_a_model` | 接收（Model=None 无字段） |
| `PROMPT_005_submit_records_the_receipt_without_resolving_the_claim` | 接收 |
| `PROMPT_005_abandon_removes_the_claim_and_leaves_the_active_run_alone` | 接收（「active run 不变」断言移交 interaction-authority） |
| `PROMPT_011_claim_scope_names_exactly_session_run_origin_and_payload` | 接收 |
| `PROMPT_011_claim_sequence_advances_on_registration_not_on_resolution` | 接收 |
| `PROMPT_011_prompt_key_is_deterministic_and_moves_with_every_component` | 接收 |
| `PROMPT_011_recovery_budget_is_folded_from_plugin_starts_not_written` | 接收（精确常数断言 → HOW/弃权，cutover 收敛为「有界预算」） |

## semantic anchors

`scripts/checks/semantic-anchors.mjs` 只有 Role/tool/office catalog（owner =
`cognitive-environment` / `action-affordance` / `office-capability`）。**dispatch-protocol 拥有
0 个 semantic anchor id**：发送语义由类型（`PromptClaim`、`PromptKey`）与行为测试承载。

## GAP 与 cutover 待办

- 无 GAP：11 条命题全部有落点。
- SPLIT@cutover：`authority.test.mjs` 按上表物理拆分并删除原文件；`send-format.test.mjs`、
  `host/join-guard.test.mjs`、`journal/envelope.test.mjs` 的跨 owner 断言在文件级拆分时收敛。
- 已知限制：存储级 `loadJournalEnvelopes` 按 ObservedAt 排序整体重放，导致恢复预算的
  restart-counting 依赖 envelope 顺序；包内 recovery 测试用远未来 boot 日期固定顺序
  （见测试头注释）。纯 fold 级 budget 语义由 `journal/envelope.test.mjs`（REUSE）独立覆盖。

# PROOF —— 测试落点表（dispatch-protocol）

## 运行方式

```bash
node --test requirements/dispatch-protocol/tests/fire-and-forget.test.mjs   # MOVE（原 requirements/dispatch-protocol/tests/fire-and-forget.test.mjs）
node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs  # NEW
node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs  # NEW
# 全量：node tests/unit/run.mjs（自动包含 requirements/**/tests/*.test.mjs）
```

## 命题 → 落点

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| R1 | DISPATCH-PROTOCOL-002/003/004 | `requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs::DP_002_submit_records_the_receipt_without_resolving_the_claim` + `DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone` + `DP_002_claim_records_payload_digest_and_effective_agent` + `DP_003_receipt_shape_distinguishes_admission_from_physical_identity`（`accepted-*` 只是 admission，`msg_*` 才是物理证据；recovery 侧见 R4 `DP_011_recovery_never_resends_and_proves_acceptance_from_physical_message`） | NEW | `node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs` |
| R2 | DISPATCH-PROTOCOL-005/006/010 | `claim-lifecycle.test.mjs::DP_005_prompt_key_is_deterministic_and_moves_with_every_component` + `DP_005_claim_scope_names_exactly_session_run_origin_and_payload` + `DP_010_authority_root_profile_cannot_express_a_model` | NEW | 同上 |
| R3 | DISPATCH-PROTOCOL-006 | `claim-lifecycle.test.mjs::DP_006_claim_sequence_advances_on_registration_not_on_resolution` + `DP_007_recovery_budget_is_folded_from_plugin_starts_not_written` | NEW | 同上 |
| R4 | DISPATCH-PROTOCOL-007/008 | `requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs::DP_011_recovery_never_resends_and_proves_acceptance_from_physical_message`（StillPending 保持 + 绝不重发 + Proven）+ `DP_011_budget_exhausted_abandons_unresolved_claim_instead_of_resending`（GaveUp） | NEW | `node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs` |
| R5 | DISPATCH-PROTOCOL-009 | `requirements/dispatch-protocol/tests/fire-and-forget.test.mjs::PROMPT_007_detached_claims_and_persists_without_physical_accepted` + `PROMPT_007_detached_continuation_same_claim_path` + `PROMPT_007_await_mode_constructors_exist` | MOVE | `node --test requirements/dispatch-protocol/tests/fire-and-forget.test.mjs` |
| R6 | DISPATCH-PROTOCOL-001/011 | `tests/unit/prompt/authority.test.mjs::PROMPT_005_submit_records_the_receipt_without_resolving_the_claim` + `tests/unit/prompt/send-format.test.mjs::PROMPT_006_send_payload_carries_agent_and_no_model`（Metadata/PromptKey 锚 + Model=None 半边；agent 绑定半边归 participant-identity） | REUSE | `node --test tests/unit/prompt/authority.test.mjs` / `node --test tests/unit/prompt/send-format.test.mjs` |
| R7 | DISPATCH-PROTOCOL-007（claim 释放可重试） | `tests/unit/host/join-guard.test.mjs::JNGD_nudge_releases_the_key_when_send_fails_and_retries`（send 失败 → Abandon(SendFailed) 释放 key；JoinGuard continuation 语义归 interaction-authority） | REUSE | `node --test tests/unit/host/join-guard.test.mjs` |
| R8 | DISPATCH-PROTOCOL-002（budget 派生） | `tests/unit/journal/envelope.test.mjs::PROMPT_011_RuntimeStarted_advances_a_workspace_watermark_not_every_session`（纯 fold：stamp=折叠位置水印；attempts/budgetSpent） | REUSE | `node --test tests/unit/journal/envelope.test.mjs` |
| R9 | DISPATCH-PROTOCOL-001/005/006（claim 唯一写入口 + 幂等身份） | `tests/unit/prompt/authority.test.mjs::PROMPT_011_claim_scope_names_exactly_session_run_origin_and_payload` + `PROMPT_011_prompt_key_is_deterministic_and_moves_with_every_component` + `PROMPT_011_claim_sequence_advances_on_registration_not_on_resolution` | REUSE | `node --test tests/unit/prompt/authority.test.mjs` |
| R10 | DISPATCH-PROTOCOL-007（恢复预算语义） | `claim-lifecycle.test.mjs::DP_007_recovery_budget_is_folded_from_plugin_starts_not_written`（纯函数阈值）+ `recovery-at-most-one.test.mjs`（行为分支） | NEW | 两文件各自命令 |

统计：10 行落点；NEW 2 文件 11 断言 + MOVE 1 文件 3 断言全绿；REUSE 3 个既有文件（SPLIT@cutover 前留在原处）。

## `tests/unit/prompt/authority.test.mjs` SPLIT 计划（本包半边）

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

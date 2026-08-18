# output-distillation — 实现模型与约束

非 normative。WHAT 是唯一权威；本文件解释实现模型与历史裁决。

## 实现模型

### 蒸馏管线（`Infrastructure/OpenCode/Tools/Distillation.fs`）

```text
ProcessOutcome.Spooled(spoolPath, …)          // 物理采集（process-execution）
→ Spool.readChunks(spoolPath, 204800B)        // chunk 化
→ map: 每 chunk fork 一个 Distiller（distillFragmentPrompt）
→ 并行 map，按 chunk index 顺序 await（每 agent 恰好一次 AwaitAgentWithPermit）
→ 任一 map 失败 → cancelOwned()（取消全部 owned map/reduce agents）
→ reduce: rippleInsert 在线归并（ReduceFanIn=8）→ foldLevels → mergeDistillationsPrompt
→ 成功: 完整 account
→ 失败: partialWithTail → CondensationIncomplete/Unavailable + 最后 chunk 原始字节 raw_tail
```

关键常数：`ReduceFanIn = 8`、`AwaitAgentTimeoutMs = 600_000`（每 chunk join 预算）、
`Spool.ChunkSizeBytes = 204800`（= `SPOOL_CHUNK_BYTES`）。

### 失败降级（DISTILL-006 / Oracle 2 行为面）

- 全部成功 → 完整 account（不含 `Condensation incomplete`）。
- 任一 chunk 失败（NotFound 硬失败 / 真超时）→ 不 throw；`partialWithTail lang account rawTail`：
  - `account` 非空 → `CondensationIncomplete`（模板含 `Most recent raw output:` + `raw_tail`）；
  - `account` 空 → `CondensationUnavailable`（模板同样携带 `raw_tail`）。
- `raw_tail` = 最后一个 chunk 的原始字节（`rawTailText chunks`）——对未见过原文的读者保留可定位痕迹。
- 失败 chunk 的 work record **不会**出现在 summary（`summary-for-<failedId>` 缺失 = 不虚构成功）。

### 定向 await 合同（DISTILL-008，`DistillationRuntime.fs`）

- `IDistillationRuntime`：`Fork` / `AwaitAgentWithPermit` / `CurrentJournalRevision` /
  `AwaitJournalChangeFrom` / `CancelAgent`。
- permit 门：每次 await 前 `requirePermit()`；`RECOVERY_WAITING:` → `ForkError.TimedOut`（等 readiness
  信号后再一次 fresh permit check）；其它 permit 错误 → `ForkError.NotFound`（hard fail，不重试）。
- `awaitAgentWithPermit`：deadline 内 throttle；journal advance 才重试；超时 → `DISTILL_AWAIT_TIMEOUT`。
- `ofForkRuntime`：纯 ForkRuntime 无 journal → fail closed（不铸造 synthetic permit）。

### 输出预算合同（DISTILL-011/012）

- `Process/LargeGate.fs`：单持有者大进程门（FIFO cancelable waiters；first holder wins；release 泵队）——
  一次只允许一个大输出进程占用全局预算窗口（EXEC-013）。
- `Domain/ToolResultBound.fs`：Host 默认 head truncation（2000 行 / 51200 B）之前完成确定性留尾截断：
  `Marker = "...head truncated (tail kept)...\n\n"` + UTF-8 安全 tail（`ContentMaxLines = 1998` /
  `ContentMaxBytes = 51166`），保证 Host 不再二次截断（ARCH-012）。

### Distiller 私有 runtime（EXEC-014）

Distiller 映射子会话：`distillerAgent = ManagedAgent.nameOf AgentTier.Fast Role.Distiller`（固定名，
非 caller 选择）；`HandleOwnership.HostOwnedHidden`（对父 list/join/horizon/guard/恢复不可见，仍持久）；
`run` 工具同步掌控 fork → permit-gated await → 摘要 → 返回；调用方不 join、不承担生命周期。

## 物理落点（CURRENT EVIDENCE）

- Resource：`resources/provider/role/distiller/`（fragment humility 散文）。
- Semantic owner surface: `OpenCode/Tools/DistillationSurface.fs` translates the fixed Distiller role, private-target rule, empty permission catalog, and `run` execution verb to JS-native strings/booleans/arrays. Tests consume this surface; they do not reconstruct Role/ToolSpec values.
- Failure：`Process/LargeGate.fs`、`Domain/ToolResultBound.fs`。
- Tests：包内 `tests/executor-summarize.test.mjs`（MOVE）、`tests/distiller-fragment-humility.test.mjs`（NEW）。

## 边界与弃权（非 normative）

- **GARBAGE——chunk 统计 wire**：蒸馏不得返回 chunk 统计仪表盘、不得叙述 map-reduce 机械过程、
  不得报告 success ratio（Role Law「切割是你的私务」）；不进入未来 WHAT 的任何字段合同。
- **GARBAGE——Meditator/Executor 角色路径**：与 Distiller 无关的已删算法面，见
  历史 how/execution 已删除清单。
- **HOW——机制常数**：`ReduceFanIn=8`、`AwaitAgentTimeoutMs=600_000`、`MemoryBufferBudget=204800`、
  `Spool.ChunkSizeBytes=204800`、`HostMaxLines=2000`/`HostMaxBytes=51200`、
  `ContentMaxLines=1998`/`ContentMaxBytes=51166`、`MarkerBytes=34`：有界性/诚实性才是 WHAT。
- **HOW——当前实现形状**：Distiller 当前是 fast 固定 agent 的 LLM map/reduce；可整体替换为
  deterministic+LLM hybrid summarizer（独立变化测试），WHAT 不动。
- **归属他包**：spool 物理采集（`Process/ProcessOutput.fs`、`Spool.fs`）→ `process-execution`；
  Distiller child 的生命周期/隐藏 handle → `managed-session-lifecycle`；Assignment 机器字段的
  horizon 过滤 → `participant-horizon`；ARCH-012 的 wire 渲染 owner → `provider-projection`。
- **不复制** `process-execution`（exit/onExit/cancel）、`review-judgement`（PERFECT/REVISE）、
  `context-compression`（Blogger/prefix）的命题。

## 验证与测试落点

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|---------|
| DISTILL-001 大输出诚实压缩、不静默截断 | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-001] fragment_humility_compression_is_lossy_but_honest_not_a_silent_empty_success`（失败 → 承认不完整而非报成功）；`tests/executor-summarize.test.mjs` `WHAT[DISTILL-001] DISTILLATION_distill_fragment_prompt_is_plain_intent` | NEW+MOVE | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-002 保留改变 judgment 的事实 | anchor `distinguishing`（双语言命中）；`tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-002] fragment_humility_keeps_the_judgment_changing_distinguishing_marker`（marker 保留）；`requirements/output-distillation/tests/large-gate.test.mjs`（预算合同交叉） | REUSE+NEW | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-003 fragment 谦逊 ≠ 整体成功 | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-003] fragment_humility_admits_fragment_boundary_and_never_fabricates_failed_summary`（summary 命中 `/Condensation incomplete\|Most recent raw output/`、不含 `summary-for-<failedId>`）；anchor `fragment-humility` | NEW | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-004 合并不发明因果/成功率 | anchors `merge-conflicts` / `no-invented-causality`（双语言命中）；`tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-004] fragment_humility_failed_fragment_is_not_outvoted_by_quiet_chunks`（失败者被诚实保留而非投票消除）；`tests/executor-summarize.test.mjs` `WHAT[DISTILL-004] DISTILLATION_merge_distillations_prompt_is_plain_intent` | NEW+REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-005 对未见原文 reader 可用 | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-005] fragment_humility_raw_tail_keeps_the_locator_for_an_unseen_reader`（verbatim 含 marker）；anchor `locatable-to-unseen-reader` | NEW | `node --test requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-006 失败路径 = partial + raw tail | `tests/distiller-fragment-humility.test.mjs` `WHAT[DISTILL-006] fragment_humility_failed_second_chunk_keeps_raw_tail_and_admits_incompleteness`；`tests/executor-summarize.test.mjs`（`WHAT[DISTILL-006] EXEC_distill_spool_await_not_found_hard_fail_collects_failure`、`WHAT[DISTILL-006] EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`） | NEW+MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` |
| DISTILL-007 chunked map + online reduce + cancelOwned | `tests/executor-summarize.test.mjs`（`WHAT[DISTILL-007] EXEC_distill_spool_targeted_await_one_call_per_agent_no_stash`、`WHAT[DISTILL-007] EXEC_distill_spool_targeted_await_out_of_order_returns_own_agent`、`WHAT[DISTILL-007] EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await`）；`tests/reconcile-supervisor-distill.test.mjs` `WHAT[DISTILL-007] EXEC_distillation_cancel_owned_on_failure` | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/reconcile-supervisor-distill.test.mjs` |
| DISTILL-008 每 chunk 定向 await 一次；permit 分型 | `tests/executor-summarize.test.mjs`（`WHAT[DISTILL-008] EXEC_distill_spool_family_waiting_waits_for_readiness_before_one_fresh_permit_check`——call order `[permit, readiness, permit]`；`WHAT[DISTILL-006] EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success`） | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs` |
| DISTILL-009 私有 runtime | `tests/distiller-role-contract.test.mjs` `WHAT[DISTILL-009] distiller_is_private_internal_runtime_not_a_public_fork_or_horizon_target`（`DistillationSurface` 返回私有角色、固定 fast-distiller identity）；`requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` `EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible` | NEW+REUSE（surface→本包；hidden handle→managed-session-lifecycle） | `node --test requirements/output-distillation/tests/distiller-role-contract.test.mjs requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs` |
| DISTILL-010 Distiller 不执行/不裁决 | anchor `no-execution`（distiller 组双语命中）；`tests/distiller-role-contract.test.mjs` `WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface`（`DistillationSurface` 零权限、run 为唯一执行面）；`requirements/process-execution/tests/executor-tool.test.mjs`（RUN_* 与 distill 边界） | NEW+REUSE | `node scripts/checks/semantic-anchors.mjs` |
| DISTILL-011 Large Gate 预算合同 | `requirements/output-distillation/tests/large-gate.test.mjs`（`WHAT[DISTILL-011] VERIFY_009_large_gate_first_acquire_succeeds_immediately`、`WHAT[DISTILL-011] VERIFY_009_large_gate_second_acquire_waits_until_release`、cancelable waiters 组）；`requirements/output-distillation/tests/large-gate-runner.test.mjs` `WHAT[DISTILL-011] EXEC_011_large_estimate_acquires_and_releases_the_gate`；`requirements/process-execution/tests/process-runner.test.mjs` `EXEC_011_large_estimate_acquires_and_releases_the_gate` | REUSE | `node --test requirements/output-distillation/tests/large-gate.test.mjs requirements/output-distillation/tests/large-gate-runner.test.mjs requirements/process-execution/tests/process-runner.test.mjs` |
| DISTILL-012 确定性留尾截断 | `requirements/output-distillation/tests/tool-host-codec-full.test.mjs`（ToolResultBound 面）`WHAT[DISTILL-012] CODEC_register_applies_tool_with_uncurried_execute_and_bounds_result` | REUSE（SPLIT：wire 渲染→`provider-projection`） | `node --test requirements/output-distillation/tests/tool-host-codec-full.test.mjs` |
| DISTILL-013 不返回 chunk 统计仪表盘 | `tests/executor-summarize.test.mjs` `WHAT[DISTILL-013] DISTILLATION_prompts_carry_no_chunk_index_or_level`；`tests/executor-tool.test.mjs` `WHAT[DISTILL-013] RUN_spooled_output_runs_distillation_without_chunk_statistics`（wire 无 chunk_count/total_bytes/spool_path） | MOVE | `node --test requirements/output-distillation/tests/executor-summarize.test.mjs requirements/output-distillation/tests/executor-tool.test.mjs` |

### 移动/新写文件清单

| 源 | 目标 | 类型 | 结果 |
|----|------|------|------|
| — | `src/Wanxiangshu/OpenCode/Tools/DistillationSurface.fs` | NEW semantic owner surface | `DistillationSurface` exposes only JS-native role/privacy/permission/execution observations; role test imports it directly |
| — | `requirements/output-distillation/tests/distiller-fragment-humility.test.mjs` | NEW（Oracle 2，SPLIT：DISTILL-001..006 六 test） | `node --test` 绿（6 断言） |
| — | `requirements/output-distillation/tests/distiller-role-contract.test.mjs` | NEW（DISTILL-009/010 contract test） | `node --test` 绿（2 断言） |

### SPLIT@cutover 清单（REUSE 文件拆分计划）

- `requirements/output-distillation/tests/large-gate.test.mjs`：整文件归本包（DISTILL-011）；fable_modules import 是
  test-boundary 门 grandfathered 行，移动会红 → 留原处。
- `requirements/process-execution/tests/process-runner.test.mjs`：`EXEC_011_large_estimate_acquires_and_releases_the_gate`
  归本包；run/spawn/kill 断言 → `process-execution`；estimate 拒绝 → `time-capability`。
- `requirements/managed-session-lifecycle/tests/distiller-ownership.test.mjs`：Distiller 私有 runtime 语义归本包（DISTILL-009）；
  `HostOwnedHidden` handle → `managed-session-lifecycle`；Assignment 过滤 → `participant-horizon`。
- `requirements/output-distillation/tests/tool-host-codec-full.test.mjs`：ToolResultBound 截断断言归本包；Semantic/Wire
  codec 断言 → `provider-projection`。
- `requirements/process-execution/tests/executor-tool.test.mjs`：distill 面（`ExecutorTool` 中 distill 工具）归本包；
  RUN_* → `process-execution`；tool registry → `capability-enforcement`。

### 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.distiller`（全部 5 个）：`distinguishing`、`fragment-humility`、`merge-conflicts`、
`locatable-to-unseen-reader`、`no-invented-causality`。

# HOW — knowledge-reuse 的实现模型与约束

> 非 normative。描述当前实现如何满足 WHAT；实现可整体替换（`17-repository.md` INDEPENDENT CHANGE：Case maintenance 换 deterministic merge + optional LLM 而 reuse semantics 不变）。

## 模块地图（当前实现）

### Production semantic owner（纯决策 + JS-native boundary）

`src/Wanxiangshu/Repository/Knowledge/Casebook/Surface.fs` owns the Casebook semantic
translation. Its registered sibling owner surfaces keep adjacent contracts at their actual
owners: `IndexSurface.fs` owns the provider index, `BookkeeperSurface.fs` owns maintenance
transactions, `LifecycleSurface.fs` owns draft/finalize wiring, and `FetchSurface.fs` owns the
provider fetch tool. `SyncDelegateSurface.fs` is delegation-owned and exposes only the opaque
reusable runtime needed by the G6 host path. These modules translate plain JS values and keep
F# models, workflows, collection/result representations, Host schemas, and session runtime
opaque. Durable Casebook operations receive an opaque EventStoreSurface handle.


| 类型/模块 | 内容 |
|---|---|
| `Observation` | `FileRead(path, contentHash)` / `GlobResult(pattern, paths)` / `GrepResult(pattern, matches)` —— typed observation（KNOWLEDGE-REUSE-003） |
| `ObservationIdentity` | 同路径同内容去重的规范化身份（`read:` / `glob:` / `grep:` 前缀 + 排序后内容） |
| `Case` | `{ SessionId; Q; A; Observations; LastAccessOrder }`（LastAccessOrder 是 monotonic counter，不是 wall clock） |
| `CasebookEvent` | `CaseCaptured` / `CaseRefreshed` / `CaseAccessed` / `CaseEvicted` —— fold 输入（KNOWLEDGE-REUSE-007） |
| `ReplayResult` | `Fresh` / `Stale`（KNOWLEDGE-REUSE-004/005） |
| `Observations.normalize` | 按 identity 去重 + 稳定排序，同一证据折叠同一字节 |
| `Observations.classifyReplay` | 存储与重放集合精确相等 → Fresh，否则 Stale |
| `CasebookProjection.apply` | Captured 插入/替换、Refreshed 替换 Q/A/observations、Accessed 派生访问序、Evicted 移除；同 Case 多 head 由 EventStore 层表达 DomainConflict |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Surface.fs` | registered `CasebookSurface`: JS-native pure laws/capture plus durable operations over an opaque EventStore handle. |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/IndexSurface.fs` | registered `CasebookIndexSurface`: JS-native `tryGet`/`refresh`/`resolve` snapshots and stable shelfmarks; internal index records never cross. |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperSurface.fs` | registered `CasebookBookkeeperSurface`: JS-native refresh/staging envelopes and opaque session-port capability; Bookkeeper runtime/tool internals stay private. |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/LifecycleSurface.fs` | registered `CasebookLifecycleSurface`: marker, draft, collector, finalize, cleanup, and access operations with JS-native results. |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/FetchSurface.fs` | registered `CasebookFetchSurface`: fetch spec/execute over an opaque EventStore handle; replay and refresh authority stay private. |
| `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs` | registered delegation-owned `SyncDelegateSurface`: opaque reusable runtime/Host/Journal/Attachment harness with JS-native child/result observations. |



| 文件 | 内容 |
|---|---|
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Capture.fs` | `contentHash`；`ofReadExecution` / `ofGlobExecution` / `ofGrepExecution` / `ofExecCommand`（executor 命令 tokenize 识别：`cat`/`head`/`tail`/`sed` 单文件正例；`sh -c`/`bash -c`/命令替换安全跳过）；`capture(toolName, args, output)` |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Replay.fs` | `replayOne`（当前 worktree 只读重放单个 observation）；`replayAll`（List.choose，捕获缺失的 observation 跳过） |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Workflow.fs` | `CasebookFeature.isEnabled`（marker = `.wanxiang/casebook` 目录）；`archiveInspectorResult`（Append Captured）；`fetchCase`；`checkFreshness`；`refreshCase`（Append Refreshed）；`needsRefresh`；`finalizeCase`（exactly-once）；`touchCaseAccess`（Append Accessed） |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Index.fs` | `Snapshot`（shelfmark + canonical question only）；`shelfmarkFor`；`resolve`（shelfmark → 内部 Case）；`refresh` / `invalidate`（epoch 推进）；frozen snapshot 进程内缓存 |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Store.fs` | `CasebookStream = "casebook"`；事件类型 `InspectorCaseCaptured` / `InspectorCaseRefreshed` / `InspectorCaseAccessed` / `InspectorCaseEvicted`；`appendCaptured/Refreshed/Accessed/Evicted`；`tryDecodeEnvelope` 只解码单个 envelope，历史枚举与 fold 归 CanonicalIntegrator |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Lifecycle.fs` | `collector`；`setEnabled`；`notePrompt` / `noteAnswer`（draft 收集）；`tryFinalizeInspector`（ReuseScope close → exactly one finalize）；`cleanupInspector`（unexpected delete：零 EventStore 写）；`touchAccess` |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/SessionDraft.fs` | `CasebookDraftStore`（session → Q/A turns 的内存 draft） |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/Bookkeeper.fs` | `refreshStale`（CaseRefresh：freeze → transaction → stability verify → Refreshed） |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperStaging.fs` | `beginTransaction` / `snapshot` / `apply` / `take` / `abort`（js-bookkeeper 的 staged 变换） |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperRuntime.fs` | `BookkeeperRequest = CaseRefresh | CaseFinalize`；`bindSession` / `unbindSession` / `tryTxId` / `runTransaction`（CreateSiblingSession physical-root lane + `js-bookkeeper` only + staging） |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/OpenCode/Tools.fs` | `js-bookkeeper(program)` spec + execute：case SDK（`setQuestion`/`setAnswer` 各至多一次）+ runtime base class；无 filesystem capability |
| `src/Wanxiangshu/OpenCode/Tools/FetchTool.fs` | `fetch(shelfmark)` spec + execute：shelfmark 解析 → replay → Fresh/Refreshed/Stale consequence；`fetchGate`/`fetchInFlight`（same-worktree single-flight） |

### Session 交叉（不归本包 HOW 主体）

Bookkeeper child 生命周期（`fast-bookkeeper`/`deep-bookkeeper`、Clerk/Curator Persona、InternalLeaf + Attached）由 Session/Process 侧持有（历史 shape/casebook Bookkeeper 身份边界）；staged SyncInspector 的 Persona/ProviderLanguage 在物理 delete 后保留到 owner ReuseScope 的 `CaseFinalize` 结束；Bookkeeper 以无 physical parent 的 sibling lane 创建，避免等待已删除 Host parent，同时继承 commissioner identity，finalize 后由 Host deletion scope drop。本包只消费 `BookkeeperRequest` 契约（KNOWLEDGE-REUSE-006）。

## 主流程

```text
Inspector 调用（复用或非复用 scope）
→ typed observation capture（read/glob/grep 工具执行）
→ scope terminal（非复用）或 ReuseScope close（复用）
→ freeze draft（Q 逐字 + A 逐字 + observations）
→ exactly one finalize/archive provider transaction
→ Append InspectorCaseCaptured（大正文 PayloadRef → store payloads）
→ CasebookProjection fold 更新 index

后续 fetch(shelfmark)：
→ CasebookIndexSnapshot（当前 epoch 冻结；provider 只含 shelfmark + canonical Q）
→ shelfmark 解析到内部 Case
→ 对当前 worktree replay observations（只读，不写）
→ no-delta → Fresh consequence + exact canonical A（freshness hint，非正确性证明）
→ delta → Bookkeeper CaseRefresh（js-bookkeeper* 0..N → stability verify → Refreshed）
→ 失败 → Stale consequence + 保留旧 canonical A（older account）
```

## 依赖（DEPENDS ON，逐条理由）

| 依赖 | 理由 |
|---|---|
| `repository-investigation` | fetch 的 replay 是真实观察（`CasebookReplay.replayAll` 对当前 worktree 重放 typed observations）；freshness hint 依赖「当前事实由真实观察建立」的保证；hint 永远不是 fact。 |
| `durable-events` | Case 事实以 `InspectorCase*` events + PayloadRef 进入统一 EventStore；durable authority、CAS、fold 由它提供（KNOWLEDGE-REUSE-007）。 |
| `durable-convergence` | replica 收敛 = EventStore set union；同 Case 并发 fork 的 DomainConflict 表达由 convergence 物理层提供（KNOWLEDGE-REUSE-011）。 |

## 历史与弃权

### 被拒方案（详见历史 change（perm-inspector）、历史 why/casebook 条款）

独立 Git store / refs / hook；timestamp / revision 决定 freshness 与 merge winner；逐调用 finalize；从 transcript 文本推断 observation；full knowledge base；无 marker 也运行；`edit-qa` 双文档字符串替换；Bookkeeper 借用 Inspector self-model；`(revision, wall_clock)` LWW。均记录于 `WHY.md` §历史拒绝方案。

### 判定为 HOW（非 normative；不入 WHAT）

- marker 目录名 `.wanxiang/casebook`、LRU capacity / prune key 权重、`CompletionTimeoutMs = 600_000` 等常数。
- `fast-bookkeeper`/`deep-bookkeeper` 机器身份、Clerk/Curator Persona、`js-bookkeeper` 工具的具体 JS SDK 形态 → 当前实现词汇（`participant-identity`/`session-ontology` 交叉）。
- digest synthesizer：历史 change（perm-inspector）曾规划 LearningCompiler / CaseSynthesizer；G6 Product Exit 明确 **synthesizer gone**——「不新建 LearningCompiler/CaseSynthesizer/StudentReplacement」是当前 absence（KNOWLEDGE-REUSE-006 边界），无合成器可迁移。

### 判定为 GARBAGE（migration/clean-break 沉积）

- Student/Teacher/QA bootstrap（`PROMPT-012` absence）：Casebook 的 G6-G 验证「无 Student/Teacher/QA/SKILL」是 migration ratchet，不进入永久 WHAT。
- 旧名 `edit-qa` 的兼容 alias：`js-bookkeeper` clean-break 后 `edit-qa` 非法；absence 由工具面保证（KNOWLEDGE-REUSE-006 现状），不另立命题。

### 不归本包（COVERAGE 交叉确认）

- 并发 DomainConflict 的一般收敛律与 `tests/unit/persist/event-store-merge*` → `durable-convergence`。
- Semble 低信任 hint 与 warm-start 管线 → `repository-investigation`（AGENT-027/032）。
- Inspector 的取证权（read/glob/grep 能力、Inquiry→Inspector 分层）→ `repository-investigation`/`office-capability`/`capability-enforcement`。
- ReuseScope / SyncDelegate / Attached session 生命周期 → `managed-session-lifecycle`/`session-ontology`/`delegation`。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（物理移入本包）/ `REUSE`（留在原处，记录锚点与 cutover 计划）。
> 单跑：`WANXIANGSHU_PROVIDER_LANGUAGE=en node --test requirements/knowledge-reuse/tests/<file>`。全套：`node requirements/verification-system/tests/run.mjs`。

### Semantic surface evidence

`casebook-surface.test.mjs` is the registered `CasebookSurface` contract: observations, cases,
events, normalized output, replay classification, LRU results, and exactly-once result envelopes
cross as plain JavaScript data. `casebook-store.test.mjs` exercises the same surface with the real
unified EventStore capability; it does not construct F# unions or inspect Current internals.
`casebook-index.test.mjs`, `bookkeeper-{mechanical,session,synthesis}.test.mjs`,
`lifecycle-wiring.test.mjs`, `fetch-tool.test.mjs`, and the G6 integration tests use their
registered owner surfaces (`IndexSurface`, `BookkeeperSurface`, `LifecycleSurface`,
`FetchSurface`, and delegation `SyncDelegateSurface`) rather than importing Model/Workflow/Store,
Host codecs, tool implementations, or session runtime internals. Each surface keeps its
resource/session capability opaque and returns only JS-native observations/results.


| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| `KNOWLEDGE-REUSE-001` | `casebook-store.test.mjs` → `CASE004_005_workflow_archive_fetch_closed_loop_reads_Current_only`（archive→fetch 闭环、无 commit history 引入）；`fetch-tool.test.mjs` → `CASE009_fetch_never_writes_the_subject`（不改 subject worktree） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/fetch-tool.test.mjs` |
| `KNOWLEDGE-REUSE-002` | `lifecycle-wiring.test.mjs` → `lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once`（Q/A 进入 Case）；`fetch-tool.test.mjs` → `CASE004_fetch_returns_exact_canonical_a`（返回 exact canonical A）；`casebook-domain.test.mjs` → `CASE002_fold_captured_and_refreshed_keeps_qa_verbatim` | MOVE | `node --test requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs requirements/knowledge-reuse/tests/fetch-tool.test.mjs requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| `KNOWLEDGE-REUSE-003` | `casebook-capture.test.mjs` → 全部 6 个 test（`CASE003_read_capture_is_typed_and_hashed` / `CASE003_glob_capture_parses_rendered_paths` / `CASE003_grep_capture_keeps_match_lines` / `CASE003_unknown_tool_yields_nothing` / `S63_executor_reading_positives` / `S63_executor_reading_negatives_skip_safely`）；`casebook-domain.test.mjs` → `CASE003_normalize_dedupes_and_orders_observations` | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-capture.test.mjs requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| `KNOWLEDGE-REUSE-004` | `fetch-tool.test.mjs` → `CASE004_fetch_uses_shelfmark_and_replays_before_refreshing`；`casebook-store.test.mjs` → `CASE004_refresh_and_needsRefresh_replay_the_same_Current`；`casebook-domain.test.mjs` → `CASE004_classifyReplay_fresh_only_on_exact_normalized_equality` | MOVE | `node --test requirements/knowledge-reuse/tests/fetch-tool.test.mjs requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/casebook-domain.test.mjs` |
| `KNOWLEDGE-REUSE-005` | `casebook-store.test.mjs` → `CASE004_005_freshness_check_is_hint_not_proof_reads_Current_only`（no-delta 是 hint 非 proof）；`bookkeeper-session.test.mjs` → `CASE006_missing_session_port_keeps_old_case`（维护失败 ≠ fetch 失败，返回旧 A） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/bookkeeper-session.test.mjs` |
| `KNOWLEDGE-REUSE-006` | `js-bookkeeper-tool.test.mjs` → 全部 6 个 test（`js_bookkeeper_surface_is_program_only_and_has_case_sdk` / `js_bookkeeper_program_reshapes_question_and_answer_atomically` / `js_bookkeeper_zero_mutation_is_legal` / `js_bookkeeper_duplicate_set_rolls_back_the_whole_program` / `js_bookkeeper_program_failure_rolls_back_staged_mutation` / `js_bookkeeper_unbound_session_cannot_change_a_case`）；`edit-qa-tool.test.mjs` → `CASE006_bookkeeper_provider_contract_is_one_program`（edit-qa 非法）；`bookkeeper-{mechanical,session,synthesis}.test.mjs` → CaseRefresh 全链路 | MOVE | `node --test requirements/knowledge-reuse/tests/js-bookkeeper-tool.test.mjs requirements/knowledge-reuse/tests/edit-qa-tool.test.mjs requirements/knowledge-reuse/tests/bookkeeper-mechanical.test.mjs requirements/knowledge-reuse/tests/bookkeeper-session.test.mjs requirements/knowledge-reuse/tests/bookkeeper-synthesis.test.mjs` |
| `KNOWLEDGE-REUSE-007` | `casebook-store.test.mjs` → `CASE007_captured_refreshed_round_trip_through_integrator_Current` / `CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan` / `CASE007_store_has_no_loadEvents_project_or_history_reader`；交叉 REUSE `scripts/checks/unified-store-gate.mjs`（禁 feature store） | MOVE + REUSE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs`；`node scripts/checks/unified-store-gate.mjs` |
| `KNOWLEDGE-REUSE-008` | `casebook-domain.test.mjs` → `CASE008_lru_evict_keeps_most_recently_accessed` / `CASE008_fold_accessed_and_evicted_derives_access_order`；交叉 `casebook-store.test.mjs` → `CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan`（evict tombstone 事件往返）；`lifecycle-wiring.test.mjs` → `lifecycle_touchAccess_and_touchCaseAccess_advance_integrated_access_order`（last_access 派生） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-domain.test.mjs requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs` |
| `KNOWLEDGE-REUSE-009` | `casebook-store.test.mjs` → `CASE009_marker_gates_the_surface`（双门）；`lifecycle-wiring.test.mjs` → `lifecycle_disabled_marker_skips_publication` | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-store.test.mjs requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs` |
| `KNOWLEDGE-REUSE-010` | `lifecycle-wiring.test.mjs` → `lifecycle_cleanupInspector_never_publishes_case` / `lifecycle_missing_answer_is_noop_finalize`（unexpected delete 仅 cleanup）；`universal-loop.test.mjs` → `G6_G_universal_loop_archive_finalize_fetch` / `G6_G_lifecycle_note_finalize_fetch_and_cleanup` / `G6_G_cancel_session_cleanup_no_publication`；`g6-host-reuse-finalize.test.mjs` → `G6_G_host_reusable_inspector_one_finalize_then_cold_fetch`（exactly-one finalize）；`g6-inspector-tool-finalize-fetch.test.mjs` → `G6_inspector_tool_sync_delegate_lifecycle_bookkeeper_fetch`；`casebook-store.test.mjs` → `CASE010_finalize_is_exactly_once_per_scope` | MOVE | `node --test requirements/knowledge-reuse/tests/lifecycle-wiring.test.mjs requirements/knowledge-reuse/tests/universal-loop.test.mjs requirements/knowledge-reuse/tests/g6-host-reuse-finalize.test.mjs requirements/knowledge-reuse/tests/g6-inspector-tool-finalize-fetch.test.mjs requirements/knowledge-reuse/tests/casebook-store.test.mjs` |
| `KNOWLEDGE-REUSE-011` | `fetch-tool.test.mjs` → `CASE011_fetch_single_flight_serializes_same_shelfmark`（same-worktree 串行化）；交叉 REUSE `requirements/durable-convergence/tests/event-store-merge.test.mjs` + `event-store-converge.test.mjs`（set union / DomainConflict / 禁 LWW 的物理 substrate → `durable-convergence`） | MOVE + REUSE | `node --test requirements/knowledge-reuse/tests/fetch-tool.test.mjs`；`node --test requirements/durable-convergence/tests/event-store-merge.test.mjs requirements/durable-convergence/tests/event-store-converge.test.mjs` |
| `KNOWLEDGE-REUSE-012` | `casebook-index.test.mjs` → 全部 4 个 test（`CASEBOOK_index_exposes_shelfmark_and_canonical_question_only` / `CASEBOOK_shelfmark_is_stable_and_not_the_session_identity` / `CASEBOOK_invalidate_then_refresh_advances_epoch` / `CASEBOOK_visible_set_change_advances_epoch`） | MOVE | `node --test requirements/knowledge-reuse/tests/casebook-index.test.mjs` |

### 统计

```text
WHAT 命题：12（KNOWLEDGE-REUSE-001..012）
落点：   MOVE 12 个命题（10 个纯 MOVE + 007/011 带 REUSE 交叉）
        REUSE 2（scripts/checks/unified-store-gate.mjs、tests/unit/persist/event-store-{merge,converge}.test.mjs → durable-convergence）
        NEW  0（14 个现有文件覆盖全部命题，无 GAP）
GAP：    0
```

### 移动文件清单（源 → 目标，均单独跑绿）

| 源（tests/unit/casebook/） | 目标（requirements/knowledge-reuse/tests/） | 断言数 | 单跑结果 |
|---|---|---|---|
| `bookkeeper-mechanical.test.mjs` | 同名 | 6 pass | 绿 |
| `bookkeeper-session.test.mjs` | 同名 | 3 pass | 绿 |
| `bookkeeper-synthesis.test.mjs` | 同名 | 7 pass | 绿 |
| `casebook-capture.test.mjs` | 同名 | 6 pass | 绿 |
| `casebook-domain.test.mjs` | 同名 | 4 pass | 绿 |
| `casebook-index.test.mjs` | 同名 | 4 pass | 绿 |
| `casebook-store.test.mjs` | 同名 | 9 pass | 绿 |
| `edit-qa-tool.test.mjs` | 同名 | 1 pass | 绿 |
| `fetch-tool.test.mjs` | 同名 | 6 pass | 绿 |
| `g6-host-reuse-finalize.test.mjs` | 同名 | 4 pass | 绿 |
| `g6-inspector-tool-finalize-fetch.test.mjs` | 同名 | 1 pass | 绿 |
| `js-bookkeeper-tool.test.mjs` | 同名 | 6 pass | 绿 |
| `lifecycle-wiring.test.mjs` | 同名 | 8 pass | 绿 |
| `universal-loop.test.mjs` | 同名 | 6 pass | 绿 |

适配说明：`casebook-domain.test.mjs`、`casebook-surface.test.mjs`、`casebook-store.test.mjs` 不再
导入 `Model.js` / `Workflow.js` / `Store.js`、`support/domain.mjs` 或 Fable collection/result
helpers；它们经 `CasebookSurface.js` 读取 JS-native 结果。`casebook-index.test.mjs`、Bookkeeper
mechanical/session/synthesis、lifecycle wiring、fetch tool 与 G6 集成测试同样只消费注册的
Index/Bookkeeper/Lifecycle/Fetch/SyncDelegate owner surfaces；真实工具、runtime、journal 与
Host codec 由各 owner surface 内部持有，不在 semantic zone 中复制 adapter。

### semantic anchor 归属（semantic-anchors.mjs）

本包拥有 `ROLE_SEMANTIC_ANCHORS.bookkeeper` 的全部 5 个 anchor id：

```text
reusable-knowledge / one-case / question-may-change / zero-mutation / transcript-is-data
```

（`scripts/checks/semantic-anchors.mjs` 的 `bookkeeper` 组：Role Law 散文锚点，锁 Bookkeeper 的复用边界。inspector 组归 `repository-investigation`。）

### SPLIT@cutover 计划（现有测试的 owner 拆分）

| 现有文件 | 当前 owner 混合 | cutover 动作 |
|---|---|---|
| `requirements/durable-convergence/tests/event-store-merge.test.mjs` / `event-store-converge.test.mjs` | `durable-convergence`（general set union / DomainConflict 物理律）+ 本包（Case 对象冲突语义） | 留在 `durable-convergence`；本包 PROOF 只引用（REUSE）；不物理移动 |
| `tests/unit/casebook/`（已移入本包） | 全部断言归本包（PROOF-MAP：casebook KEEP knowledge-reuse） | 已 **MOVE** 完成；`tests/unit/casebook/` 目录随 cutover 删除 |
| `g6-host-reuse-finalize.test.mjs` / `g6-inspector-tool-finalize-fetch.test.mjs` / `universal-loop.test.mjs` | 本包（Casebook lifecycle/fetch）+ `managed-session-lifecycle`（ReuseScope）/`delegation`（SyncDelegate）交叉 | **SPLIT**（如需）：ReuseScope close / SyncDelegate 生命周期断言归对应包；Casebook finalize/fetch 断言留本包 |

# HOW — causal-wait（实现模型与约束，非 normative）

> 本文件解释实现，不另造 normative owner。WHAT 命题见 `WHAT.md`；测试落点见 `HOW.md`。

## 实现模型总览

```text
Kernel/CausalWait.fs            通用诊断词汇 + frontier 纯算法（无业务 case）
Session/CausalWaitRegistry.fs   process-local 注册表 + CausalWaitHub 单例（observer / reader）
Session/CausalAwait.fs          await 括弧（awaitTask / awaitUnit / race / untilSignalOrDeadline）
Session/CausalWaitBridge.fs     Scheme B 诊断文件桥（.wanxiangshu/diagnostics/causal-waits.json）
scripts/checks/causal-wait-boundary.mjs  静态边界 gate（六条扫描）
```

### 1. `Kernel/CausalWait.fs` — 通用诊断词汇

```fsharp
type CausalOwnerRef = { Kind: string; Identity: (string * string) list }
type CausalProducerRef = WorkflowProducer of CausalOwnerRef | ExternalProducer of kind * identity
type WaitEscape = DeadlineAt of DateTimeOffset | CancelledBy of CausalOwnerRef
                | ProcessLifetime | SessionLifetime | OpenEndedExternal
type DiagnosticWait = { WaitKind; Owner; Subject; Producer; Escapes; Source }
type DiagnosticWaitExit = WaitResolved | WaitFailed | WaitCancelled | WaitTimedOut | WaitDisposed
type WaitTransition = { Sequence; Kind: Entered|Left; Wait; Exit: DiagnosticWaitExit option }
type DiagnosticWaitSnapshot = { Active: DiagnosticWait list; History: WaitTransition list; Sequence }
```

- `CausalOwner.key` 用 `kind:id=v` 排序拼接——同一 owner 的稳定身份串。
- `WaitKind` / `Subject` **仅供 diagnostics render**，不是 Domain vocabulary（`CausalWait.fs` 头注释）。
- exit case 名带 `Wait*` 前缀，避免与 `TerminalOutcome.Failed/Cancelled` 在 Session 模块碰撞。
- `CausalFrontier.ofSnapshot`：从 living roots 沿 consumer→producer 边走，遇到已见 owner 报 `CausalWaitCycle`（不递归爆栈）、无 active wait 报 `BrokenCausalEdge` / `ProducerRunningWithoutWait`、外部 producer 报 `ExternalProducerFrontier`、空快照报 `Empty`。**纯诊断算法**，不驱动控制流。

### 2. `Session/CausalWaitRegistry.fs` — 注册表与单例

- `active: Dictionary<int64, DiagnosticWait>` + `history: Queue<WaitTransition>`（默认容量 256，越界 Dequeue）+ 单调 `nextId` + `snapshotSequence`，`lock gate` 串行化。
- `IWaitObserver.Enter` 注册 lease；lease `MarkExit` 记录 one-shot exit（可重复调用，最后一次生效）；`Dispose` 幂等，未标记时默认 `WaitDisposed`。
- `CausalWaitHub`：进程内单例。`observer` = Enter-only 包装（每 transition 后刷新 bridge 文件）；`reader` / `snapshot` / `frontiers` 供诊断面。`setWorkspace` 由 plugin boot 设置，之后 Enter/Leave 覆盖写 bridge 文件。
- `DSL-MUTABLE` 注释标注：registry 是合法物理 mutable 资源（与 TCS/Dictionary/subscription registry 同类），不承载业务控制流。

### 3. `Session/CausalAwait.fs` — await 括弧

- `awaitTask` / `awaitUnit`：enter(descriptor) → await Task → resolve → `MarkExit WaitResolved`；异常按类型分类（`OperationCanceledException` → Cancelled、`TimeoutException`/消息含 "timed out" → TimedOut、含 "cancel" → Cancelled、其余 → Failed）→ 重新抛出。**业务顺序完全不变**，只是注册了一个观测。
- `race`：primary vs 预构建 escape loser，`Promise.race`（Fable Task 无 WhenAny），作为**一个**复合 wait 展示；赢家 exit 记录（Resolved 或 escape 的 exit）。
- `untilSignalOrDeadline`（G4R-CE S1 / rabbit §5.3）：tryRead 命中 → cancel deadline → Ok；否则 race 一个真实 `awaitSignal` 对**同一个** `IDeadlineHandle`；信号到 → re-read（同一 deadline）；deadline 到 → `Error WaitTimedOut`。**无 slice timer、无轮询间隔、无 UtcNow 循环**。stale 信号循环 re-arm 必须每次是 fresh pending Promise（已 resolve 的 Promise 会让 CE busy-spin）。

### 4. `Session/CausalWaitBridge.fs` — Scheme B 诊断桥

- `<workspace>/.wanxiangshu/diagnostics/causal-waits.json`：含 pid / sequence / active / history / frontiers。
- `.git/info/exclude` 写入 `.wanxiangshu/` 标记，保持 `git status` / IsDirty 干净（best-effort）。
- 覆盖写、吞异常（never throws into business flow）。**业务代码不得读它**（静态门第 4 条）。

### 5. 静态边界 gate（enforcement，mechanism 由本包拥有语义）

`scripts/checks/causal-wait-boundary.mjs` 六条扫描：

1. `Domain/` 不得引用 `CausalWaitRegistry|CausalWaitHub|CausalAwait`。
2. `Application/` 不得访问 `IWaitSnapshotReader`。
3. `Journal/**` 与 `Composition/Durable/Fact.fs` 不得出现 `CausalWait|WaitKind|IWaitSnapshotReader|CausalAwait`。
4. `Session/PromptDispatcher.fs`、`Application/Reconciliation/TurnCompletionProgram.fs` 不得引用诊断 snapshot / `causal-waits.json`。
5. 关键迁移点（SyncDelegateRuntime、CohortWorkflow、FinalityTool、JoinTool、Host、ReviewBarrierWorkflow、ManagerJob）不得有裸 `return!/do! xxx.Task` 业务等待（`cancel.Task` 臂与 `CausalAwait.await` 窗内例外）。
6. `CausalWaitRegistry.fs` 的 `let mutable` 必须带 `DSL-MUTABLE` 注释。

gate 由 `node scripts/check.mjs` 接线；本轮不移动 `scripts/checks/` 文件。

## 事件驱动 vs 轮询的落地（与消费方的关系）

| 等待场景 | 机制 | owner |
|---|---|---|
| join 等 completion | `CompletionMailbox` 双通道：agent 路径 Pulse（Journal），PTY 路径 PublishPty；`CausalAwait.race`/`untilSignalOrDeadline` 括弧 | `delegation`（条件）+ 本包（观测/事件驱动纪律） |
| record-ready 等 review evidence | 同 snapshot 判据 + journal 事件唤醒，禁 timer/sleep/墙钟轮询（REVIEW-017） | `review-assurance`（条件）+ 本包（event-driven） |
| 业务状态探测 | 有界因果重读（≤3 次），用尽保持 Dirty 等下一粗粒度信号；禁墙钟退避 | `host-boundary` |
| deadline / watchdog | 允许墙钟，但集中、可取消、可注入（`ITimerPort`） | `time-capability` |
| 跨进程互斥 | `proper-lockfile` 重试保持 cross-process 合同 | `change-integration`（另行裁决） |

## 历史与弃权

| 源 | 裁决 | 理由 / 落点 |
|---|---|---|
| 历史 change（causal-ce-observability） | EVIDENCE（全部吸收） | CCE 五问、类型隔离、frontier、RED-1..10、canary 教训 → WHAT CAUSAL-001/002/005/007、WHY §失败模式 1 |
| 历史 change（waitfact-causal-renewal） | EVIDENCE（吸收非权威面） | `renewOn` 显式归因、背景写入不续期 → WHAT CAUSAL-001 边界、WHY §失败模式 2（E2E watchdog 本体归 verification-system） |
| 历史 change（reconciler-event-driven-de-polling，causal-wait 部分） | EVIDENCE（吸收） | 等待四分类表、B 类事件等待零轮询 → WHAT CAUSAL-005、HOW 事件驱动表 |
| 历史 change（ce-temporal-ownership） | 本包不消费（时间/Join 部分归 structured-workflow/delegation/time-capability） | 其「五个时序 owner」教训 → `time-capability` WHY；canary causal integrity（poll≠progress）→ verification-system |
| 历史 DSL-012/013/014 | EVIDENCE | DSL-012 → CAUSAL-001..004；DSL-013/014（Semantic Vocabulary）归 `structured-workflow` |
| 历史 HOST-004 | NEEDS-SPLIT（本包取非权威面） | `QuiescencePermit` 观测稳定≠静止资格、不写 Journal、不参与 crash recovery → CAUSAL-008；reconcile machinery → `host-boundary` |
| 历史 CTX-014 | EVIDENCE（诊断边界） | 可观测诊断不得成控制输入 → CAUSAL-001 实例 |
| 历史 REVIEW-017 | 消费（不拥有） | record-ready 等待事件驱动 → CAUSAL-005 消费；fresh witness 判据归 `review-assurance` |
| 历史 GLORY-072/073 | 消费（不拥有） | record-ready 等待与 recovery → `review-assurance` + `work-record` + 本包（事件驱动/非权威）交叉 |
| 历史 loop 条款 | GARBAGE（对本包） | 循环检测/强杀归 `degeneration-guard`；`LoopKillArmed` 进程内事实是 degeneration 的，不是等待诊断 |
| 历史 orchestrator 条款 | GARBAGE（对本包） | ORCH 条款归 `change-integration` / `delegation`；无等待诊断内容 |
| `requirements/verification-system/tests/causal-diagnostics.test.mjs` | REUSE（含多 owner） | bridge 文件断言 + E2E diagnostics 格式化（`formatDiagnostics`/`formatCausalSection`/watchdog onTimeout）与 verification-system MECHANISM 混合 → SPLIT@cutover |
| `scripts/checks/causal-wait-boundary.mjs` | 机制保留（本包拥有语义） | 不可移动（scripts/checks 禁止改）；REUSE 经 check.mjs |

## 阅读实现代码的入口

```text
src/Wanxiangshu/Kernel/CausalWait.fs        # 通用词汇 + frontier 算法（先读）
src/Wanxiangshu/Session/CausalWaitRegistry.fs  # 注册表 + 单例（第二读）
src/Wanxiangshu/Session/CausalAwait.fs      # await 括弧（第三读）
src/Wanxiangshu/Session/CausalWaitBridge.fs # 诊断桥（第四读）
scripts/checks/causal-wait-boundary.mjs     # 静态边界（第五读）
```

## 不归本包

- 时间 capability（clock/timer/deadline 注入）→ `time-capability`。
- 业务流程由语言结构表达 → `structured-workflow`。
- 某个具体 reviewer / process / session 的等待条件 → 各业务 owner（`review-assurance`、`delegation`、`process-execution` 等）。
- crash recovery；process-local 观测可在重启后安全消失 → `crash-reconciliation`。
- Host snapshot 的业务事实定义 → `host-boundary`。

## DEPENDS ON

无 hard 产品依赖（`requirements/INDEX.md` 依赖骨架 Phase E 结论）：wait 的 deadline 是**可选 escape**（需要时消费 `time-capability`），event-driven wake **不依赖** `structured-workflow`。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE`（已物理移入本包 `tests/`）/ `REUSE`（留在原处，记录 cutover 拆分）/ `NEW`（本包新写）。
> 运行命令：`node --test <file>` 单跑；`node requirements/verification-system/tests/run.mjs` 全单元（自动包含 `requirements/**/tests/*.test.mjs`）；`node scripts/check.mjs` 全部静态门。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点，均带 `WHAT[<ID>]` 前缀） | 类型 | 运行命令 |
|---|---|---|---|
| CAUSAL-001 观测非权威 | `tests/causal-wait.test.mjs` — `RED_8_application_observer_enter_only_snapshot_via_reader`（observer/reader 分面）+ `tests/reconcile-idle-observation-non-authoritative.test.mjs` — `EXEC_reconcile_idle_before_transcript_materializes_within_causal_rereads`（idle 观测不构成 turn，非权威消费面）+ REUSE：`scripts/checks/causal-wait-boundary.mjs`（六条静态扫描；经 check.mjs 运行，可红） | MOVE + NEW + REUSE | `node --test requirements/causal-wait/tests/causal-wait.test.mjs requirements/causal-wait/tests/reconcile-idle-observation-non-authoritative.test.mjs`；`node scripts/check.mjs` |
| CAUSAL-002 跨 owner 等待可诊断 | `tests/causal-wait.test.mjs` — `RED_1_active_wait_visible_after_enter`（owner/producer/kind 可见）+ `tests/wait-lifecycle.test.mjs` — `descriptor_carries_typed_owner_producer_subject` | MOVE + NEW | `node --test requirements/causal-wait/tests/causal-wait.test.mjs requirements/causal-wait/tests/wait-lifecycle.test.mjs` |
| CAUSAL-003 观测不进 Journal/决策 | `tests/boundary-observation.test.mjs` — `Journal codec surfaces stay free of the causal-wait vocabulary` / `Fact codec surface stays free of the causal-wait vocabulary` / `diagnostics snapshot stays out of decision and prompt paths`（本包 NEW，pin gate 第 3/4 条同一事实的最小可执行子集）+ REUSE：`scripts/checks/causal-wait-boundary.mjs` 第 3/4/5 条（Fact/Journal codec 干净、诊断不进 PromptDispatcher/TurnCompletionProgram、关键迁移点无裸 TCS.Task await） | NEW + REUSE（gate 不可移动） | `node --test requirements/causal-wait/tests/boundary-observation.test.mjs`；`node scripts/check.mjs` |
| CAUSAL-004 Reader/Writer 类型隔离 | `tests/wait-lifecycle.test.mjs` — `observer_surface_has_no_snapshot`（`IWaitObserver` 无 Snapshot 成员）+ `tests/causal-wait.test.mjs` — `RED_8_application_observer_enter_only_snapshot_via_reader`（reader 经 CausalWaitHub 读）+ REUSE：gate 第 1/2 条（Domain/Application 边界） | MOVE + NEW + REUSE | `node --test requirements/causal-wait/tests/wait-lifecycle.test.mjs requirements/causal-wait/tests/causal-wait.test.mjs`；`node scripts/check.mjs` |
| CAUSAL-005 event-driven 优先 polling | `tests/until-signal-or-deadline.test.mjs` — `THEOREM_untilSignalOrDeadline_returns_immediately_when_tryRead_ready` + `THEOREM_untilSignalOrDeadline_signal_then_ready_cancels_deadline` + `THEOREM_untilSignalOrDeadline_stale_signal_loops_until_deadline`（无 slice timer/轮询间隔，真实信号 re-arm；SPLIT@cutover：CausalAwait 词汇归本包，deadline 能力归 time-capability） | REUSE（SPLIT@cutover） | `node --test requirements/causal-wait/tests/until-signal-or-deadline.test.mjs` |
| CAUSAL-006 取消/完成后观测终止 | `tests/causal-wait.test.mjs` — `RED_2_resolve_clears_active_and_records_resolved` / `RED_3_fail_clears_active_and_records_failed` / `RED_4_cancel_clears_active_and_records_cancelled` / `RED_4_cancel_message_also_classifies_as_cancelled` / `history_capacity_bounds_ring_buffer` + `tests/wait-lifecycle.test.mjs` — `dispose_defaults_to_wait_disposed` / `mark_exit_then_dispose_preserves_exit` / `repeated_mark_exit_last_one_wins` / `dispose_is_idempotent_single_leave` / `reenter_is_fresh_observation_not_revival` / `history_default_capacity_is_256` + `tests/escape-taxonomy.test.mjs` — `wait_escape_has_five_typed_cases` / `escapes_render_distinctly_in_diagnostics` / `deadline_escape_carries_typed_instant` | MOVE + NEW | `node --test requirements/causal-wait/tests/causal-wait.test.mjs requirements/causal-wait/tests/wait-lifecycle.test.mjs requirements/causal-wait/tests/escape-taxonomy.test.mjs` |
| CAUSAL-007 frontier 纯诊断解释 | `tests/causal-frontier.test.mjs` — `RED_5_nested_graph_walks_to_external_frontier` / `RED_6_missing_producer_reports_broken_causal_edge` / `RED_7_cycle_reports_without_hanging` / `empty_snapshot_yields_empty_frontier` | MOVE | `node --test requirements/causal-wait/tests/causal-frontier.test.mjs` |
| CAUSAL-008 process-local、重启安全消失 | `tests/wait-lifecycle.test.mjs` — `fresh_registry_starts_empty_no_durable_state`（新 registry 无任何状态）+ `tests/causal-wait-bridge.test.mjs` — `CAUSAL_BRIDGE_writeSnapshot_overwrites_workspace_json`（诊断文件 git-excluded、非 Journal）/ `CAUSAL_BRIDGE_hub_refreshes_file_on_enter` + REUSE：`requirements/verification-system/tests/causal-diagnostics.test.mjs` — `gather reads causal waits file`（诊断文件 git-excluded、非 Journal） | MOVE + NEW + REUSE | `node --test requirements/causal-wait/tests/wait-lifecycle.test.mjs requirements/causal-wait/tests/causal-wait-bridge.test.mjs`；`node --test requirements/verification-system/tests/causal-diagnostics.test.mjs` |

### 关联 REUSE 落点（边界消费方，不重复拥有）

| 场景 | 落点 | owner |
|---|---|---|
| Escape 显式终止路径（CCE-005 渲染） | `tests/escape-taxonomy.test.mjs`（本包 NEW）— WaitEscape 五 case 全区分、bridge JSON tag 全区分 | 本包 |
| Scheme B 桥 + E2E 诊断首屏 | `requirements/verification-system/tests/causal-diagnostics.test.mjs`（`CAUSAL_DIAG_format_puts_frontier_before_e2e_events` 等） | 本包（bridge 面）+ `verification-system`（format/watchdog harness）SPLIT@cutover |
| E2E watchdog 因果续期（`renewOn`） | `requirements/verification-system/tests/integration/harness/timeout-cases.mjs`、`requirements/verification-system/tests/e2e/support/scenario-schema.js`（waitFactRenewOnProblems 校验） | `verification-system`（消费 CAUSAL-001） |
| QuiescencePermit 观测非权威 | `requirements/host-boundary/tests/reconcile-idle-early.test.mjs`、`tests/unit/host/**`（host-boundary 的 machinery） | `host-boundary` + 本包（非权威面） |

### 运行与红/绿判读

- 单跑：`node --test requirements/causal-wait/tests/<file>`。任一断言失败 → 该命题的当前世界 RED。
- 全单元：`node requirements/verification-system/tests/run.mjs`（自动包含 `requirements/causal-wait/tests/**`）。
- 静态门：`node scripts/check.mjs`（`causal-wait-boundary.mjs` 是本包语义的静态 enforcement）。

### SPLIT@cutover 清单（本轮 REUSE，cutover 时拆分）

1. `requirements/causal-wait/tests/until-signal-or-deadline.test.mjs` → CausalAwait 词汇断言迁本包；deadline 能力断言迁 `time-capability`。
2. `requirements/verification-system/tests/causal-diagnostics.test.mjs` → bridge 文件/registry 断言迁本包；`formatDiagnostics`/`formatCausalSection`/watchdog onTimeout 断言迁 `verification-system`。
3. `scripts/checks/causal-wait-boundary.mjs` → cutover 后作为本包静态 gate 保留（文件位置是否移动由 requirement-system 布局裁决）。

### Semantic anchor ids

本包在 `scripts/checks/semantic-anchors.mjs` 中**不拥有**任何 semantic ID（该 catalog 的 owner 为 cognitive-environment / office-capability / action-affordance / epistemic-reasoning / review-judgement）。本包的语义 proof 是行为测试 + `causal-wait-boundary.mjs` 静态门，不是 prompt 散文锚点。

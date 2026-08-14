# HOW — causal-wait（实现模型与约束，非 normative）

> 本文件解释实现，不另造 normative owner。WHAT 命题见 `WHAT.md`；测试落点见 `PROOF.md`。

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
3. `Journal/**` 与 `Kernel/Fact.fs` 不得出现 `CausalWait|WaitKind|IWaitSnapshotReader|CausalAwait`。
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
| `archive/changes/completed/causal-ce-observability.md` | EVIDENCE（全部吸收） | CCE 五问、类型隔离、frontier、RED-1..10、canary 教训 → WHAT CAUSAL-001/002/005/007、WHY §失败模式 1 |
| `archive/changes/completed/waitfact-causal-renewal.md` | EVIDENCE（吸收非权威面） | `renewOn` 显式归因、背景写入不续期 → WHAT CAUSAL-001 边界、WHY §失败模式 2（E2E watchdog 本体归 verification-system） |
| `archive/changes/completed/reconciler-event-driven-de-polling.md`（causal-wait 部分） | EVIDENCE（吸收） | 等待四分类表、B 类事件等待零轮询 → WHAT CAUSAL-005、HOW 事件驱动表 |
| `archive/changes/completed/ce-temporal-ownership.md` | 本包不消费（时间/Join 部分归 structured-workflow/delegation/time-capability） | 其「五个时序 owner」教训 → `time-capability` WHY；canary causal integrity（poll≠progress）→ verification-system |
| `archive/docs/what/dsl-structured-program.md` DSL-012/013/014 | EVIDENCE | DSL-012 → CAUSAL-001..004；DSL-013/014（Semantic Vocabulary）归 `structured-workflow` |
| `archive/docs/what/host.md` HOST-004 | NEEDS-SPLIT（本包取非权威面） | `QuiescencePermit` 观测稳定≠静止资格、不写 Journal、不参与 crash recovery → CAUSAL-008；reconcile machinery → `host-boundary` |
| `archive/docs/what/context.md` CTX-014 | EVIDENCE（诊断边界） | 可观测诊断不得成控制输入 → CAUSAL-001 实例 |
| `archive/docs/what/review.md` REVIEW-017 | 消费（不拥有） | record-ready 等待事件驱动 → CAUSAL-005 消费；fresh witness 判据归 `review-assurance` |
| `archive/docs/what/glory.md` GLORY-072/073 | 消费（不拥有） | record-ready 等待与 recovery → `review-assurance` + `work-record` + 本包（事件驱动/非权威）交叉 |
| `archive/docs/what/loop.md` | GARBAGE（对本包） | 循环检测/强杀归 `degeneration-guard`；`LoopKillArmed` 进程内事实是 degeneration 的，不是等待诊断 |
| `archive/docs/what/orchestrator.md` | GARBAGE（对本包） | ORCH 条款归 `change-integration` / `delegation`；无等待诊断内容 |
| `tests/unit/session/causal-wait-bridge.test.mjs` | REUSE（含多 owner） | bridge 文件断言 + E2E diagnostics 格式化（`formatDiagnostics`/`formatCausalSection`/watchdog onTimeout）与 verification-system MECHANISM 混合 → SPLIT@cutover |
| `scripts/checks/causal-wait-boundary.mjs` | 机制保留（本包拥有语义） | 不可移动（scripts/checks 禁止改）；REUSE 经 check.mjs |

## 阅读实现代码的入口

```text
src/Wanxiangshu/Kernel/CausalWait.fs        # 通用词汇 + frontier 算法（先读）
src/Wanxiangshu/Session/CausalWaitRegistry.fs  # 注册表 + 单例（第二读）
src/Wanxiangshu/Session/CausalAwait.fs      # await 括弧（第三读）
src/Wanxiangshu/Session/CausalWaitBridge.fs # 诊断桥（第四读）
scripts/checks/causal-wait-boundary.mjs     # 静态边界（第五读）
```

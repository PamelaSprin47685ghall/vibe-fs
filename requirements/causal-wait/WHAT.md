# WHAT — causal-wait（唯一 normative 合同）

> 本文件是 `causal-wait` 的唯一 normative 语义合同。每条命题必须同时为真；测试落点见 `PROOF.md`。
> 术语首次出现给定义；引用精确到 `src/...fs` 与测试文件。

---

## CAUSAL-001：等待观测是非权威的 process-local 诊断信息

**规范陈述**：业务等待的诊断观测（`DiagnosticWait`，`src/Wanxiangshu/Kernel/CausalWait.fs`）可以描述当前 wait、owner、producer、causal identity、cancellation/deadline；**不得**决定业务 branch、mint permit、写 Journal 作为世界事实、用于 recovery、用于 dedupe、推进 workflow、影响 PromptAuthority / Finality / Reviewer / Manager 决策。一句话：**观察可以看程序，程序绝不可以看观察**（DSL-012，`docs/what/dsl-structured-program.md`）。

**含义/动机**：观测是给诊断面（人 / E2E watchdog / 排障）看的，不是给程序决策看的。一旦观测能改变业务结果，它就变成第二套真相源，与 durable facts 竞争 authority。

**边界**：`WaitKind` / `Subject` 字符串仅供 diagnostics render，不是 Domain vocabulary、不进 decision。E2E watchdog 是证明 harness（归 `verification-system`），其因果续期语义（`renewOn` 显式归因，waitfact-causal-renewal）消费本条：背景写入只记录不续期。

**证据指针**：→ `PROOF.md` CAUSAL-001 行。

---

## CAUSAL-002：跨 owner / turn / attempt / capability 的等待必须生成诊断观测

**规范陈述**：任何跨业务 owner、跨 Host turn、跨 provider attempt 或跨 physical capability 的业务等待（`docs/what/dsl-structured-program.md` DSL-012），都必须能生成一个 process-local diagnostic wait observation，回答 CCE 五问：Owner（谁在等）、Wait（等什么真实条件）、Producer（谁有资格满足）、Last causal progress（最后相关事实）、Termination（谁负责结束）。

**含义/动机**：可诊断性是 suspended-flow 的硬要求（causal-ce-observability.md：三个 orchestrator canary 曾只能看到「测试脚本在等什么」）。观测与等待同生共死：`CausalAwait.awaitTask` 等括弧函数保证 enter → await → resolve/cancel/fail → leave 的完整生命周期。

**边界**：具体等待条件与 producer 语义由各业务 owner 提供 typed descriptor；本包提供通用诊断词汇与注册机制，不含 Manager/Reviewer/Student 等业务 case。

**证据指针**：→ `PROOF.md` CAUSAL-002 行。

---

## CAUSAL-003：观测不得进入 Journal / Fact codec / Prompt 决策路径

**规范陈述**：`CausalWait` / `WaitKind` / `IWaitSnapshotReader` / `CausalAwait` 不得出现在 Journal 与 Fact codec 表面（`src/Wanxiangshu/Journal/**`、`Kernel/Fact.fs`）；诊断 snapshot 不得进入 `Session/PromptDispatcher.fs` 与 `Application/Reconciliation/TurnCompletionProgram.fs` 的决策/提示词路径。

**含义/动机**：这是 CAUSAL-001 的可执行版本。静态边界由 `scripts/checks/causal-wait-boundary.mjs` 强制（六条扫描：Domain 不引用实现、Application 不持有 reader、Fact/Journal codec 干净、诊断不进决策路径、关键迁移点无裸 TCS.Task await、mutable 有 DSL-MUTABLE 标注）。

**边界**：gate 文件本体在 `scripts/checks/`（本轮不移动）；本包拥有「观测不进 Journal / 不进决策」这条产品事实，gate 是其 enforcement。E2E 的 `causal-waits.json` 桥文件（`CausalWaitBridge`）是诊断输出，不是 Journal，业务代码不得读它。

**证据指针**：→ `PROOF.md` CAUSAL-003 行。

---

## CAUSAL-004：Reader / Writer 权限从类型上隔离

**规范陈述**：业务 workflow 只持有 `IWaitObserver`（仅 `Enter: DiagnosticWait -> IWaitLease`）；诊断面才持有 `IWaitSnapshotReader`（`Snapshot: unit -> DiagnosticWaitSnapshot`）。Application 不得访问 `IWaitSnapshotReader`；Domain 不得引用 CausalWait 实现。`CausalWaitRegistry` 同时实现两个接口，但**引用方拿到的引用类型**决定它能不能读。

**含义/动机**：类型隔离比 code review 可靠——即使程序员想写 `if registry.Snapshot().Contains(...)`，Application 侧的引用没有 `Snapshot` 可调（causal-ce-observability.md §8）。

**边界**：`CausalWaitHub` 的 `observer`（Enter-only 包装）与 `reader` 是进程内单例的两面；观测 registry 本身（active dict + ring buffer + sequence）是合法物理 mutable 资源（DSL-MUTABLE），不承载业务控制流。

**证据指针**：→ `PROOF.md` CAUSAL-004 行。

---

## CAUSAL-005：event-driven wake 优先于 polling

**规范陈述**：等待应由实际依赖解除（真实信号 / journal waiter / process signal / typed deadline），而不是 wall-clock luck 或轮询间隔。`CausalAwait.untilSignalOrDeadline`（`src/Wanxiangshu/Session/CausalAwait.fs`）必须：tryRead 优先；否则 race **一个**真实信号对 **一个** `IDeadlineHandle`；信号到达后 re-read（同一 deadline）；无 slice timer、无轮询间隔、无 `UtcNow` 循环。race 必须作为**一个**复合 wait 展示（`CausalAwait.race`），不打印成多个互不相关 wait。

**含义/动机**：轮询把「等什么」退化成「睡多久」，既不可诊断又不可确定；事件驱动让等待由依赖解除，诊断能看到真实因果边（reconciler-event-driven-de-polling.md：业务状态探测须有界因果重读，事件等待零轮询）。

**边界**：deadline 能力本体归 `time-capability`；本包规定等待**优先事件驱动**并在需要时把 deadline 作为 escape 参与 race。业务状态探测（有界因果重读、`ReconcileDecision`）归 `host-boundary`。

**证据指针**：→ `PROOF.md` CAUSAL-005 行。

---

## CAUSAL-006：取消 / 完成后观测生命周期终止，不复活业务机会

**规范陈述**：等待 resolve / fail / cancel / timeout / dispose 后，其观测离开 active 集合并记录 exit（`WaitResolved` / `WaitFailed` / `WaitCancelled` / `WaitTimedOut` / `WaitDisposed` 之一，仅诊断用，不是业务 result）。lease `Dispose` 幂等：未 `MarkExit` 时默认记 `WaitDisposed`；已标记的 exit 不被覆盖。已终止的观测不得复活——重新进入同一 descriptor 是新观测（新 sequence），不是旧观测续命。

**含义/动机**：取消后旧观测若仍被视为「仍在等」，会复活已终止的业务机会（如已 cancel 的 join 重新唤醒）。exit 是诊断分类，业务顺序不变：CE suspend → 注册 lease → Task settle → dispose。

**边界**：取消/超时的**业务后果**（如 `JoinWaitOutcome.Interrupted`）归消费方（`delegation` 等）；本包只保证观测生命周期终止。历史是**有界** ring buffer（默认容量 256），不无界增长。

**证据指针**：→ `PROOF.md` CAUSAL-006 行。

---

## CAUSAL-007：最小未满足因果前沿是纯诊断解释

**规范陈述**：从当前活着的根 workflow 出发，沿「consumer 正等待 producer」的边向下追踪到第一个无法解释的 unresolved producer，得到 `CausalFrontier`（`CausalFrontier.ofSnapshot`，`Kernel/CausalWait.fs`）。五类：`ExternalProducerFrontier`（等在外部 producer）、`BrokenCausalEdge`（consumer 等一个不存在的 owner）、`ProducerRunningWithoutWait`（producer 活着但未声明 wait）、`CausalWaitCycle`（环，检测不爆栈）、`Empty`。frontier 只解释，不驱动控制流；E2E 超时诊断首屏 = CAUSAL FRONTIER，原始材料降为第二层。

**含义/动机**：一次 dump 必须自动给出「卡在哪里、为什么」，不能只给事件尾巴（causal-ce-observability.md §14–15）。

**边界**：frontier 的**渲染**（`CausalWaitBridge.toPlainObject` 的 JSON、E2E 的 `formatCausalSection`）是诊断机制（与 verification-system 交叉）；**算法**（`ofSnapshot` 纯函数）归本包。

**证据指针**：→ `PROOF.md` CAUSAL-007 行。

---

## CAUSAL-008：观测是 process-local 的，重启后安全消失

**规范陈述**：观测 registry 是进程内单例（`CausalWaitHub`），不写 durable 介质、不参与 crash recovery；重启后观测自然消失，且消失是安全侧（不会有假等待、不会有残留授权）。`QuiescencePermit` 类 idle-derived 观测稳定 ≠ 静止资格：观测只描述，不授予资格（HOST-004 rev.3 的非权威面）。

**含义/动机**：诊断信息可以丢；恢复只从 durable facts 重入普通程序（`crash-reconciliation`）。若观测需要持久化才能工作，它就变成了业务事实——违反 CAUSAL-001。

**边界**：`QuiescencePermit` 的 reconcile machinery（single-flight / dirty latch / 因果重读 / `TurnOutcome` 分类）归 `host-boundary`；「观测稳定≠静止资格、不写 Journal、不参与 crash recovery」的**非权威性**归本包。

**证据指针**：→ `PROOF.md` CAUSAL-008 行。

---

## 反向覆盖（源 Clause → 本包命题）

| 源 Clause / change（COVERAGE.md / CHANGES-AUDIT.md 归属） | 落点 |
|---|---|
| DSL-012 业务异步等待因果观测（`docs/what/dsl-structured-program.md`） | CAUSAL-001/002/003/004 |
| `causal-ce-observability.md` CCE-001..005 + 类型隔离 + frontier | CAUSAL-001/002/005/007 |
| `waitfact-causal-renewal.md`（显式 renewOn，不反推因果） | CAUSAL-001（E2E 消费面） |
| `reconciler-event-driven-de-polling.md`（causal-wait 部分：B 类事件等待零轮询） | CAUSAL-005 |
| HOST-004 `QuiescencePermit`（非权威 + 重启安全消失面） | CAUSAL-008 |
| REVIEW-017 禁 timer/sleep/wall-clock 轮询（event-driven 面） | CAUSAL-005（消费：record-ready 条件归 review-assurance） |
| CTX-014 诊断边界（可观测诊断不得成控制输入） | CAUSAL-001（诊断非权威的 context 实例） |
| `scripts/checks/causal-wait-boundary.mjs` | CAUSAL-003/004（enforcement） |
| `loop.md` / `orchestrator.md` | 无本包内容（loop 归 degeneration-guard；orchestrator 无等待条款）→ 弃权记录见 `HOW.md` |

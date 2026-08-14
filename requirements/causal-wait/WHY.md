# WHY — causal-wait

## 不可替代的存在理由

**业务等待需要知道「正在等什么 / 为什么还没发生」，但诊断这一等待不能反过来成为 durable business fact、prompt authority 或决策真相源。**

一个真实系统里，跨 owner 的业务等待几乎必然存在：Manager 等 Reviewer 的 verdict、Finality 等 reviewer terminal、Orchestrator 等 Manager job、join 等 child completion、process 等 PTY exit。这些等待若**不可诊断**，一旦卡住只能看到「Task pending」——无法回答五个问题：

1. 谁拥有当前 workflow？（Owner）
2. 它在等哪一个真实条件？（Wait）
3. 谁有资格满足它？（Producer）
4. 与这个等待直接相关的最后一个已发生事实是什么？（Last causal progress）
5. 如果永远不发生，谁负责结束它？（Termination）

但若为了可诊断而把这些观测**升级为业务事实**（写 Journal、进 prompt、驱动分支），会摧毁整个语义层：诊断状态变成第二套真相，与 durable facts 竞争 authority——「观察可以看程序，程序绝不可以看观察」。这就是本包不可替代的 WHY：**等待既要可观测可诊断，观测又必须永久是非权威的 process-local 信息**。两个方向是同一枚硬币的两面，不能拆成两个包（`requirements-design/01-meta-programming.md` causal-wait 卡注）。

## 独立变化测试（Independent Change Test）

把 process-local waiter 从当前实现换成 subscription / future / actor mailbox，所有业务 package 的 WHAT 都不变。反过来，具体业务（Reviewer/process/session）重写等待条件也不要求本包改变。「等待如何被诊断」是一个独立的语义轴。

## 失败模式考古（历史上为什么发生过）

### 1. 三个 orchestrator canary 只能看到「测试脚本在等什么」（causal-ce-observability.md §0）

`orchestrator-publish` / `orchestrator-restart-publish` 等 canary 在 clean master 上 watchdog timeout，诊断只能输出 `blocked: orch.2 / blocked: manager.3 / blocked: manager.4`——这回答了「测试脚本还在等什么」，却无法回答「生产 CE 当前到底在等什么、那个等待是谁创建的、谁有资格满足它、为什么没发生」。教训：**控制流拥有权不等于 suspended-flow 可观测性**；业务 CE 调用栈持有 control flow，却没有可解释的等待观测。本包把「任何跨 owner/turn/attempt/capability 的等待必须生成 process-local diagnostic observation」写成 DSL-012 正式条款，并给出最小未满足因果前沿作为诊断首屏。

### 2. watchdog 把「任意 journal 增长」当成因果进展（waitfact-causal-renewal.md）

E2E watchdog 的 `awaitFactBarrier` 曾把两类观察都当阻塞进展：被等待事实计数增长 **和** 任意 journal 事实增长（后者经未指定 `blocking` 的 `advance` 默认继承 `true`）。这与 VERIFY-004 的背景车道定义冲突：背景写入应「记录但不续期」。修复让续期依据由剧本显式声明（`renewOn`），不从「journal 有任何写入」反推因果。教训：**因果归因必须显式，禁止从噪声反推**——诊断观察（包括 E2E watchdog 的 advance 分类）不能因为「有点进展」就升级成阻塞事实。

### 3. 轮询与退避睡眠充当业务等待（reconciler-event-driven-de-polling.md）

Reconciler 曾按退避数组 `setTimeout` 重读 snapshot 探测业务状态；Executor join 曾 `while not done` 忙等；SSE 曾 `setInterval(15_000)` 周期扫描。该 change 把等待分为四类并确立规则：业务状态探测必须有界因果重读（不得以墙钟退避推进）、事件等待必须事件驱动零轮询、deadline/watchdog 允许墙钟但须集中可取消可注入、跨进程互斥另立合同。教训：**等待应由实际依赖解除（事件），不是 wall-clock luck**——event-driven 优先于 polling 是本包核心命题。

### 4. 诊断状态混入决策路径的静态泄漏

`causal-wait-boundary.mjs` 的四条静态边界来自真实泄漏风险：Domain 曾可能引用 CausalWait 实现、Application 曾可能持有 `IWaitSnapshotReader`、CausalWait 曾可能进入 Fact/Journal codec、诊断 snapshot 曾可能进入 PromptDispatcher/决策路径。类型隔离（`IWaitObserver` vs `IWaitSnapshotReader`）+ 静态门把「程序不可以看观察」从纪律变成编译/门禁可查。

## 与相邻包的边界（为什么不是它们的子集）

| 相邻语义 | 归属 | 理由 |
|---|---|---|
| 时间能力（clock/timer/deadline） | `time-capability` | 等待的**能力输入**与等待的**诊断**是两条轴；deadline 是本包的可选 escape |
| 业务流程用语言结构表达 | `structured-workflow` | 「无第二程序计数器」独立于「等待可诊断」；Phase E 已删 `structured-workflow → causal-wait` hard edge |
| 具体等待条件（record-ready、join、verdict） | `review-assurance` / `delegation` / `process-execution` 等 | 本包只保证等待**可诊断且非权威**，不规定谁在什么条件下等 |
| crash recovery | `crash-reconciliation` | process-local 观测重启后安全消失是特性；恢复只从 durable facts 重入 |
| Host snapshot 业务事实 | `host-boundary` | `TurnUnknown` / `QuiescencePermit` 的观测 machinery 归 Host；「观测稳定≠静止资格」的**非权威性**归本包 |
| E2E watchdog / proof ladder | `verification-system` | watchdog 是证明 harness；其因果续期语义消费本包 |

## RED 的样子

- 业务等待无法回答 CCE-001..005 五问（只能看到裸 Task pending 或盲 sleep）。
- 某处把 wait snapshot 写进 Journal、喂给 PromptDispatcher、或驱动 Fallback/probe/squash。
- 取消后旧观测仍 active（复活业务机会）。
- Application 持有 `IWaitSnapshotReader` 或 Domain 出现 CausalWait 引用（静态门红）。

# causal-wait — WHAT

本文件是 `causal-wait` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## CAUSAL-001: 等待观测是非权威的 process-local 诊断信息

业务等待的诊断观测（`DiagnosticWait`）仅用于描述当前的等待主体、关联目标、生产者身份与终止逃逸路径，**严禁**作为业务分支决策的依据、严禁用于签发业务许可、严禁写入 Journal 持久化事实、严禁用于崩溃恢复或影响调度决策。观察可以看程序，程序绝不可以看观察。

## CAUSAL-002: 跨 owner / turn / attempt / capability 的等待必须生成诊断观测

任何跨业务 owner、跨 Host turn、跨 provider attempt 或跨底层物理能力的业务异步等待，必须生成进程内的因果诊断观测，完整回答 CCE 五问：Owner（等待主体）、Wait（等待的具体真实条件）、Producer（有权满足条件的生产者）、Last causal progress（关联的最后已发生事实）与 Termination（终止逃逸责任）。

## CAUSAL-003: 观测不得进入 Journal / Fact codec / Prompt 决策路径

因果等待词汇（`CausalWait`、`WaitKind`、`IWaitSnapshotReader`、`CausalAwait`）严禁出现在 Journal 与 Fact 的序列化与编解码接口中，诊断快照严禁传入 Prompt 构建器或业务决策路径中。

## CAUSAL-004: Reader / Writer 权限从类型上隔离

业务工作流仅能持有写入权限的 `IWaitObserver` 接口（仅提供 `Enter` 方法返回租约），快照读取权限 `IWaitSnapshotReader`（提供 `Snapshot` 读取方法）仅向外部诊断基础设施暴露。Application 层严禁获取读取器，Domain 层严禁引用因果等待的实现。

## CAUSAL-005: event-driven wake 优先于 polling

业务等待必须由真实的依赖解除事件（真实信号、Journal 写入事件、进程退出信号或强类型截止时间）驱动唤醒，严禁使用盲目轮询间隔、墙钟退避睡眠或带有全局时钟判断的循环推进业务等待。组合竞争等待必须作为单一复合观测对外呈现。

## CAUSAL-006: 取消 / 完成后观测生命周期终止，不复活业务机会

当异步等待达成、失败、超时或被取消时，其对应的诊断观测必须立即移出活跃集合并记录退出状态（如 `WaitResolved`、`WaitCancelled`、`WaitTimedOut`）。观测租约的释放操作必须幂等，已终止的观测严禁复活，防止终止后的业务任务被意外唤醒。

## CAUSAL-007: 最小未满足因果前沿是纯诊断解释

系统必须提供纯算法（`CausalFrontier.ofSnapshot`），从活跃的根工作流出发沿等待因果链自动分析出最小未满足因果前沿（如等在外部生产者、等待边断裂、生产者无等待运行或循环等待），直接作为排障与测试超时的首屏诊断输出。因果前沿仅作排障解释，严禁反向驱动业务控制流。

## CAUSAL-008: 观测是 process-local 的，重启后安全消失

因果等待注册表是严格的进程内单例，不记录持久化介质，不参与崩溃恢复。进程重启后旧的等待观测自然清空并安全消失，系统恢复仅从持久化事实重新进入普通业务流程。

## CAUSAL-009: wait vocabulary、runtime、diagnostic与mailbox必须分层

wait identity、frontier、reader/writer capability type形成pure contract；registry/await runtime只拥有process-local waiter；Node diagnostic adapter只实现窄observation port；CompletionMailbox runtime独立拥有其physical wake resource；proof Surface不得成为production provider。production consumer只能取得composition注入的最窄capability，禁止通过global observer/service locator取能力；process-local diagnostic target只能first-bind，后续plugin instance不得重定向。禁止contract closure包含registry、TaskCompletionSource、Node import、diagnostic implementation或mailbox implementation。

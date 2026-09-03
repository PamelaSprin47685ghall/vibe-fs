# causal-wait — WHY

## 不可替代的存在理由

**跨组件的业务异步等待必须能够被清晰诊断（回答「正在等什么、为什么还没发生」），但诊断观测结果绝不能反过来成为持久化业务事实、决策输入或提示词权威。**

在复杂的分布式或并发工作流中，跨 Owner 的等待是必然存在的（如 Manager 等待 Reviewer 判决、Finality 等待审查终态、Orchestrator 等待子任务交付）。如果这些等待**不可诊断**，当系统阻塞挂死时只能观察到模糊的「Task pending」，无法回答五个关键的因果诊断问题（CCE 五问）：
1. **Owner**：哪个业务工作流在等待？
2. **Wait**：它正在等待哪一个具体真实的业务条件？
3. **Producer**：谁有资格产生该条件以解除等待？
4. **Last causal progress**：与该等待直接相关的最后一条已发生事实是什么？
5. **Termination**：如果条件永远无法满足，由谁负责有界终止？

然而，如果为了诊断便利而将这些中间等待观测**提升为业务事实**（写入 Journal、传入 Prompt 或驱动业务分支），会导致诊断状态与持久化事实竞争权威，摧毁领域状态机的单一真理源。

因此，`causal-wait` 的核心存在理由是：**确立「观察可以看程序，程序绝不可以看观察」的不可变原则，既让所有跨边界等待完全可因果诊断，又确保该观测严格作为进程内的非权威诊断信息存在。**

## 核心张力与设计原则

- **事件驱动优先于轮询（Event-driven over Polling）**：等待必须由真实的依赖解除信号（如 Journal 事件、进程信号、强类型截止时间）唤醒，严禁使用盲目的墙钟轮询或退避休眠推进业务逻辑。
- **读写权限类型隔离（Reader/Writer Capability Separation）**：业务代码仅能获取登记等待的写入接口（`IWaitObserver`），只有外部诊断工具才能持有快照读取接口（`IWaitSnapshotReader`），从类型系统层面杜绝业务逻辑读取诊断数据。
- **进程内生命周期与安全消失（Process-local & Fail-safe Ephemerality）**：等待观测仅存于内存注册表中，不进入持久化存储，重启后安全消失，系统恢复仅从持久化事实重新重入。

## 核心不变量与违约状态（RED）

仓库处于 RED 状态，当且仅当出现以下任一破坏等待因果性与隔离性的违约：
1. 诊断观测结构（`DiagnosticWait` 等）被写入 Journal、进入 Fact 编解码器或传入 Prompt/决策逻辑。
2. 业务层（Application / Domain）获取或调用了快照读取器（`IWaitSnapshotReader`）。
3. 跨 Owner、跨 Turn 或跨能力的异步等待未进行因果描述登记，退化为无因果上下文的裸 Task 等待。
4. 使用轮询休眠（sleep/polling）代替事件驱动机制来等待业务条件达成。
5. 等待任务在取消或超时后，其观测状态未被正确注销，复活已终止的业务机会。
6. frontier vocabulary、registry/await runtime、Node diagnostic adapter、CompletionMailbox与proof Surface处于同一project，使只需typed wait capability的consumer获得mutable waiter与physical diagnostic authority。

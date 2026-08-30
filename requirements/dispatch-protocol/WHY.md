# dispatch-protocol — WHY

## 1. 领域动机与核心矛盾

当已获授权的逻辑交互（Prompt）穿过不可靠的宿主（Host）与网络时，必须解决传输不确定性带来的副作用复制问题：
1. **传输回执冒充物理落地**：将宿主调用返回的 `accepted-*` 异步收据误认为物理消息已被 provider 消费，导致过早假设状态。
2. **崩溃重试引发重复执行**：宿主可能已接收消息并触发 provider 运行，系统在崩溃恢复后如果盲目重发，会造成双重逻辑副作用（如两次重复的工具调用或双倍扣费）。
3. **缺少稳定幂等标识**：依靠时间窗口或随机 ID 无法在进程重启后正确定位同一逻辑动作的物理执行痕迹。

`dispatch-protocol` 建立严格的 **at-most-one** 物理调度边界：
- 唯一入口与确定性 `PromptKey` 绑定；
- 采用四态 Claim 生命周期（`Claimed → Submitted → PhysicalAccepted / Abandoned`）；
- 面对不确定物理结果（Uncertain Outcome）严格 fail-closed 挂起，禁止盲目重发。

## 2. 核心不变量与破坏后果

- **At-Most-One 逻辑效应**：宁可挂起待决状态，绝不虚构 exactly-once 或在未获物理证据前盲目重试；若破坏，并发重试将导致业务世界状态分叉。
- **Claim 先于物理发送持久化**：必须先完成 durable claim 记录，方可调用底层传输通道；若破坏，崩溃窗口内发生的调用将彻底失联。
- **Receipt ≠ Physical Message**：传输回执仅代表已入队（Submitted），唯有宿主真实的物理消息证据才能解决 Claim（PhysicalAccepted）。
- **Dispatch ≠ Execution**：dispatch 只证明合成 prompt 的传输与物理落地。物理消息落地后的 durable acceptance、provider start、terminal 与 exact settlement 由 `managed-chat-execution` 独占；混合两者会使传输重放意外触发 provider 执行。
- **Activation 先于 Recovery**：构造期间读 journal 或启动调和会在 durable substrate 尚未可用时制造第二启动顺序；恢复只能在 durability activation 后由事实事件驱动。

## DEPENDS ON

- `interaction-authority`
- `effect-accounting`
- `host-boundary`
- `durable-events`
- `managed-chat-execution`

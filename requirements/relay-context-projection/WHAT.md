# relay-context-projection — WHAT

## [001] 完整物理历史

物理 transcript、durable audit 与后继 provider projection 均保留前任全部物理消息，包括工具调用/结果、suicide、nudge、迟到 parts 和内部 loop wake。不得按退休 cut、条数、时间或文本删除、截断、折叠或重排前任历史。

## [002] Cut 只判定请求身份

`ProjectionCut = { ProviderRunId; ToolCallId }` 随退休原子持久化，只用于精确识别退休 attempt。其迟到 parts 和后续请求在 transform 边界按 stale 拦截，只可记入审计或诊断，不得作为新任活跃请求。真实权威输入与新任 owner-admitted `manager-assess` 不受拦截；身份按位置、role 与 typed message identity 判定，不猜文本、run 或到达顺序。

## [003] 历史不代替工作区

Provider 上下文由完整物理历史与显式资源构成；工作区由 Manager 自行检查，projection 不转述其内容。AuthorityRevision、SnapshotId、phase ID 与 secret 不进入 provider 消息；当前动作由事实约束的资源与能力表达，不由前任摘要代替。

## [004] 统一评审起点

首任与后继均先独立评审接手的工作。首任以用户任务和接手状态为依据，不伪造前任成果；后继结合前任完整 provider 历史、共享工作区与显式工作记录判断，不只看静态快照。

## [005] 线程连续

不得为缩短上下文新建用户线程或删除历史。物理 SessionId 可复用，逻辑 IncumbencyId 与 provider context 必须重开；重开不清除既有物理历史。

## [006] 确定性映设

相同事实产生相同可见集合。Projection 不生成合成消息、列表或历史摘要，不改写、概括或拼接前任消息，也不另行注入 hidden reasoning、token 或 credential。

## [007] 退休事实不回退

Crash 不得丢失 committed cut。每个退休后的新任均以 LatestRetirement cut 识别 stale 请求，包括 Accepted 证书失效后的普通重开；Continue 才自动激活后继。恢复既不复活旧 attempt，也不删除已保留历史。

## [008] 保留不等于授权

根请求及每次 accepted AuthorityRevision 的物理消息按 typed identity 保留，前任其它消息和内部 wake 也作为历史保留。形似 user 的 wake 不构成新权威；不得以保留历史为由放宽 authority 判定或引入文本例外。

## [009] 工作事实可核查

后继看见前任完整物理交互，projection 不概括成结论替其评审。固定 DevOps 的最新产物、修改与执行结果通过共享工作区和显式记录/检查接口可见，并与历史相互印证。同一物理会话的交互各归确定任期，不混成并行 Manager 对话。

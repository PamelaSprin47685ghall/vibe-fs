# relay-context-projection — WHAT

后继投影与 work-008 同一义务。编号留在本包，是因为 surface 仍引用这些命题。

## [001] 后继剥除

后继 provider 投影剥除前任 assistant 与 tool。非首任注入一次 LWR_prev。首任不注入。不得按条数、时间或文本重排后继自己的消息。

## [002] Cut 只判定请求身份

ProjectionCut 只识别退休尝试。迟到 parts 不得作为新任活跃请求。真实新任请求不受这道拦截。身份按位置、role 与 typed message identity 判定，不猜文本。

## [003] 历史不代替工作区

provider 上下文由剥除后留下的本任消息、用户原文与显式资源构成。工作区由 Manager 自行检查，projection 不转述其内容。当前动作由事实约束的资源与能力表达，不由前任摘要代替。

## [004] 统一评审起点

首任与后继均先独立 review 接手的工作。首任以用户任务和接手状态为依据，不伪造前任成果。后继结合 LWR_prev、共享工作区与显式工作记录判断。

## [005] 线程连续

不得为缩短上下文新建用户线程。物理 SessionId 可复用。逻辑 incumbencyId 与 provider context 必须重开。重开剥除前任 assistant 与 tool，不把它们留在后继消息集里。

## [006] 确定性映设

相同事实产生相同可见集合。projection 不生成合成消息，不改写、概括或拼接前任消息，不得包含 hidden reasoning、token 或 credential。

## [007] 退休事实不回退

crash 不得丢失已提交的 cut。每个退休后的新任以最新 cut 识别 stale 请求。Relayed 才自动派发后继。恢复不复活旧 attempt。

## [008] 保留不等于授权

形似 user 的 wake 不构成新权威。不得以审计里仍能读到旧消息为由放宽 authority 判定，也不得引入文本例外。

## [009] 工作事实可核查

projection 不把前任工作概括成结论替后继评审。固定 DevOps 的最新产物、修改与执行结果通过共享工作区和显式记录可见。同一物理会话的交互各归确定 tenure，不混成并行 Manager 对话。

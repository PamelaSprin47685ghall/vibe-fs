# Strength — 设计理由

本页只解释选择；可观察语义见 `what/strength.md`。

## 为什么只投机只读调查

Strength 要替代的是昂贵 primary 在局部窗口里重复做出的机械调查决策，而不是替代写入、执行、权限或最终判断。`read/glob/grep` 的错误方向只增加有界成本与上下文字节；写文件、执行命令、网络访问或权限交互会把错误预测变成真实世界副作用，无法靠 primary 忽略结果恢复。

## 为什么 Candidate 不能直接成为历史

Replica 行为是 intervention。primary 尚未消费时，它既不是用户行为，也不是 primary 已发生的因果历史。提前写入 XTrace/Companion 会让未发生的世界污染未来请求；反过来，primary 已消费后若重启时丢失，又会删除真实因果历史。因此必须以 durable Candidate → consumption proof → Promotion 分开“准备好”与“已经影响 primary”。

## 为什么复用现有 owner

Session 身份、Prompt authority、Projection、XTrace、Fallback 与 durable storage 已各有唯一 owner。Strength 只需要在这些代数中增加一个合法 case；重新建立 Replica role、Satellite kind、私有 journal/blob、fallback 或 projection DSL 会制造同一事实的第二表示，并让恢复与权限产生分叉。

## 为什么 same-role fast leaf

Strength 需要的是与 primary 相同的 CanonicalRole 语境、不同的较低成本模型、以及更窄的 request-specific 工具集合。现有 `fast-ROLE/deep-ROLE` 与 `AttemptExecutionProfile` 已能表达三者。新增 Replica role 或 Agent 只会破坏 Agent→Role 的函数关系。

## 为什么训练只用 shadow/control primary 数据

Replica 自己产生的 readonly 请求是策略干预，不是“没有 Strength 时 primary 本来会做什么”的观测。把 intervention 当 label 会形成自我强化闭环。deterministic control holdout 保留无干预 primary 轨迹，使 readonly 命中率、成本与质量差异可识别、可重启重现。

## 为什么默认 K0

Strength 是优化，不是正确性前提。成本关系、目标 ProviderRun、Host canary、durability 或 eligibility 任一无法证明时，最安全的选择是 K0；但已经 Promoted 的历史属于真实因果事实，即使新 speculation 熔断也必须继续恢复与 replay。

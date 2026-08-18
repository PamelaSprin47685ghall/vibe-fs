# WHY：为什么需要 intra-participant-parallelism

当一项工作内部存在真正可分离的并发切片时，层级 delegation 会把“增加执行容量”误表达成“创造另一个 participant”。这会改变 parent/child topology、handle、join obligation 与 responsibility owner；而单 session 多 provider stream 又会破坏现有单线 attempt/transcript/prefix 假设。

本包唯一存在理由是：**允许同一个 logical participant 临时拥有多个 coequal execution presents，同时保持 identity、authority、children、外部 responsibility 与最终 completion cell 不分裂。**

RED failure meaning：user-facing/root participant 的 provider-visible tool list 出现 Fission、或 root 强行调用时先收到 prompts/容量等与 origin 无关的业务错误；user-facing/root participant 被 Fission 替换成多个 physical roots，导致用户所见会话主体突然消失/分叉；Fission 后出现新的 logical participant/handle；lane 抢走别的 lane completion；fission 前已有外部 work 只被一个 lane 消费；部分 lane admission 后旧 caller 已被中断；任意 completion order 导致 work 丢失/重复；restart 后猜 lane；或 parent 收到 0/2+ 次 terminal completion。

独立变化测试：可以整体替换 lane transport、handoff topology 或 convergence algorithm，而 participant identity、delegation、LWR 格式、PTY execution contract 均无需重写。因此它不是 `delegation`、`managed-session-lifecycle`、`work-record` 或 `process-execution` 的子条款。

## DEPENDS ON

`participant-identity`, `session-ontology`, `managed-session-lifecycle`, `office-capability`, `capability-enforcement`, `participant-horizon`, `work-record`, `process-execution`, `durable-events`, `crash-reconciliation`.

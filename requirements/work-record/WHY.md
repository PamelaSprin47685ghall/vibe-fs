# work-record — WHY

一段 work 会在多个边界之间传递（如父子 delegation、process review、Finality、SyncDelegate caller 等）。每个 receiver 需要同一个事实：这段 work 做了什么、边界在哪。但不同 receiver 的投影需求不同（例如是否包含 Opening）。如果不同 receiver 各自生成摘要，同一个 work 就会存在多份互相矛盾的表述，使得 review 与 finality 无法互证。

**work-record 保证：一段 work 只有一个 canonical bounded statement（LifecycleWorkRecord / LWR），receiver 只能选择投影视图，不能改变事实本身。**

## 核心不变量与张力

- **事实统一 vs 视图投影**：同一份 canonical record 必须完整保留 Opening、Chronicle 与 Recent work；`includeOpening` 仅控制渲染，不改变事实内容。
- **因果边界 vs 会话时间**：record 的范围严格由因果游标界定，不受会话物理顺序或读取者主观新近性影响。
- **诚实陈述 vs 格式束缚**：正式陈述采用散文 claim 表达，严禁以固定 DTO schema 绑架真实工作语义。

## 违反边界的失败意义

- receiver 收到的工作记录混入其它 invocation 或 session 的历史数据。
- 记录丢失 constitutive Opening，或尝试从次要文本拼接重建。
- 记录因 receiver 不同而改变事实本身而非仅改变投影。
- 要求 participant 填写固定字段 DTO 才算完成工作。

## DEPENDS ON

- `semantic-trace`
- `context-compression`
- `participant-horizon`

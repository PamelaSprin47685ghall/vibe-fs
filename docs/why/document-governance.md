# 为什么采用正式规范与单文件 Change 生命周期

正式规范回答“当前系统是什么”，Change 回答“一项已批准工作从等待到完成发生了什么”。把两者混在
Proposal/Status 副本里，会产生重复状态、丢失原始批准范围，并诱使实现者从半成品记录解释产品行为。

## 关键取舍

**目录状态而非正文状态。** 文件所在的 proposed/active/completed 已足够表达生命周期；再写 YAML status、
manifest 或数据库只会产生第二真相源。

**一个工作一个文件。** Proposal、Status、Outcome 分开时，范围、进度和结果容易漂移。移动同一文件并追加
有限章节，既保留原始批准内容，也避免平行账本。

**用户裁决，Agent 执行。** Proposed 的进入条件由用户负责。Agent 重做 Admission 或索要 Accepted 证明
没有增加安全性，只会把用户已经完成的裁决变成阻塞。真正的安全边界是：Agent 不自选工作、不改批准范围，
遇到矛盾报告 blocker。

**Original proposal 冻结。** 最终结果反写初始 Proposal 会销毁“当时批准了什么”的证据。错误预测、未采用
方案和后来修订都应通过追加内容解释，而不是让历史看起来从未犯错。

**Active 不是状态日志。** Git 已保存提交过程；Active 只需要剩余关闭条件、客观 blocker 和完成判据。
不要求每次提交更新，可以减少维护噪音和过期快照。

**Completed 永久保留但不具规范权威。** Git 能保存字节历史，Completed 提供以工作项为单位的可读上下文；
当前产品语义仍只从正式 docs 读取，避免历史设计重新成为影子规范。

**普通小修改不强制建立 Change。** 工作流服务于需要显式批准范围和跨层闭环的工作，不应变成每次格式修复
或测试补充的仪式成本。

## 被拒方向

- Agent 自动扫描 Proposed 并挑选“应该开始”的工作。
- Agent 根据正文语气、代码痕迹或日期推断 Active/Completed。
- 独立 Proposal、Status、Decision、Outcome 或 Accepted 档案。
- Rejected、Withdrawn、Paused 等额外目录；取消或替代只接受用户明确指令。
- Change manifest、中央 ID 注册表、YAML 生命周期字段或冻结状态机。
- 用 Active/Completed 覆盖正式 what/shape/how/proof。
- 完成后删除 Proposal，或把最终结果改写成最初设想。

## 权衡

| 选择 | 代价 | 收益 |
|---|---|---|
| 用户管理 Proposed | 自动化不能替用户排期 | 启动授权清楚，Agent 不越权 |
| Active 原文冻结 | 文件可能保留后来证明错误的判断 | 批准范围可审计 |
| Completed 永久保存 | 历史文件持续增长 | 变更上下文不依赖 Git 考古 |
| 不设 manifest | 不能从单表查询全部元数据 | 目录即状态，无同步债务 |
| 正式 docs 独立 | 实施中需同时维护 Active 与 docs | 当前语义与工作历史不混淆 |

核心边界：Changes 保存批准范围与历史，正式 docs 保存当前真理，代码只按正式执行链实现。

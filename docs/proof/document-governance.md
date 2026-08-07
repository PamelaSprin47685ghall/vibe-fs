# 文档治理 — 证明

规则见 `what/document-governance.md`；程序见 `how/document-governance.md`。

## 机器检查

| 检查 | 命令 | 守住的 GOV |
|---|---|---|
| 正式 Clause 唯一、引用可解析、前缀归属、正式导航完整 | `scripts/checks/spec.mjs` | GOV-001、GOV-005 |
| Proposed/Active/Completed 不定义正式 Clause；`CHG-NNN` 合法 | 同上 | GOV-005、GOV-006 |
| Changes 三目录存在；旧 docs Proposal/Status 目录不存在 | 同上 | GOV-001、GOV-010 |
| 同一路径工作项不并存于多个生命周期目录 | 同上 | GOV-006 |
| 当前仓库文件不引用废止工作流路径 | 同上 | GOV-010 |
| 生产代码、资源和测试不把 Proposed 当规范 | 同上 | GOV-003、GOV-007 |
| 正式 docs、代码和测试不从具体 Completed 文件解释当前语义 | 同上 | GOV-003、GOV-004 |
| 本地 Markdown 链接存在 | 同上 | 导航完整性 |

检查器不得读取正文推断生命周期状态、批准证据或完成状态，不检查 Decision Owner、Accepted 字段，
不自动移动文件，也不建立 manifest。

## 人工评审

| 检查 | 失败含义 |
|---|---|
| Agent 未经用户指定启动 Proposed | GOV-007 |
| Active 原文被反向改写 | GOV-006、GOV-008 |
| Active 成为目标产品语义的唯一来源 | GOV-003、GOV-011 |
| Completed 被用作当前实现依据 | GOV-004 |
| Active 保存进度流水或未经批准的新设计 | GOV-008 |

新增静态门必须有纯规则回归，并用受控反例证明仓库入口会判红；反例不得进入最终提交。

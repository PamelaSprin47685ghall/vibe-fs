# 文档治理 — 证明

规则：`what/document-governance.md`。程序：`how/document-governance.md`。理由：`why/document-governance.md`。

## 机器检查

| 检查 | 命令 | 守住的 GOV |
|------|------|------------|
| 条款唯一、引用可解析、前缀归属、伪 ID、全部正式文件与活跃流动面导航 | `scripts/checks/spec.mjs` | GOV-005、GOV-006、GOV-008 |
| Proposal/Status 不定义任何 Clause 形标题 | 同上 | GOV-006、GOV-008 |
| AGENTS/README 不定义正式 Clause 标题 | 同上 | GOV-002、GOV-005 |
| 生产代码、资源和测试不依赖 Proposal ID 或路径 | 同上 | GOV-003 |
| 旧权威路径不进入生产源码 | `scripts/checks/architecture.mjs` | GOV-010 |

## 人工 / 评审

| 检查 | 失败含义 |
|------|----------|
| 代码直接实现 proposal | GOV-003 |
| how 被改成「当前半成品快照」且无 status | GOV-002、GOV-008 |
| 同一 ID 双定义 | GOV-005 |
| 未迁入 clean break 仍被当合同 | GOV-010 |

新增静态门必须有纯规则回归，并用一次受控反例证明仓库入口会判红；恢复反例后再执行 `npm run lint`。

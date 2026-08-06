# 文档治理 — 证明

规则：`what/document-governance.md`。程序：`how/document-governance.md`。理由：`why/document-governance.md`。

## 机器检查

| 检查 | 命令 | 守住的 GOV |
|------|------|------------|
| 条款唯一、引用可解析、前缀归属、伪 ID、正式文件与全部活跃 status/proposal 导航 | `scripts/checks/spec.mjs` | GOV-005、GOV-006、GOV-008、导航 |
| 流动面无 `## PREFIX-NNN` 定义 | 同上（status/proposal 扫描） | GOV-008、GOV-006 |
| 旧权威路径不进入生产源码 | `scripts/checks/architecture.mjs` + 评审 proposal 历史文本 | GOV-010 |

## 人工 / 评审

| 检查 | 失败含义 |
|------|----------|
| 代码直接实现 proposal | GOV-003 |
| how 被改成「当前半成品快照」且无 status | GOV-002、GOV-008 |
| 同一 ID 双定义 | GOV-005 |
| 未迁入 clean break 仍被当合同 | GOV-010 |

## 本仓库实证

- 正式文件在 `docs/{why,what,shape,how,proof}`  
- 活跃 status 仅保存实现相对正式规范的差距；未裁决设计只进 proposal
- `npm run lint` 含 spec-check  

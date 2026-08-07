# 文档治理 — 执行程序

## Implements

- GOV-003
- GOV-005
- GOV-006
- GOV-007
- GOV-008
- GOV-009
- GOV-010
- GOV-012

## Ownership

写入口与层边界见 `shape/document-governance.md`。本文件只规定执行顺序；不定义产品行为。

## 阅读与修改顺序

```text
what → shape → how → status → code/resources → proof
```

先从 `docs/README.md` 找到主题，再读取相关正式层与活跃差距。`why` 用于理解理由；
只有评审候选变化时才读取 `proposal`。禁止从 Proposal 或单独从 what 直接修改实现面。

## 行为变更程序

1. 在 `proposal/` 写候选 Delta，并填写基线、影响图、兼容性、证明计划、Decision Owner 和准入阻塞。
2. 按 GOV-007 检查当前正式层是否可接纳；未解决的正式语义冲突由 Decision Owner 裁决。
3. 接受时，在同一变更内把知识分发到适当正式层：长期理由进 why，行为进 what，所有权进 shape，算法进 how，证明义务进 proof。
4. 若实现尚未对齐，把剩余物理差距转换为最小 status 条目；不得复制 Proposal 正文或已完成历史。
5. 读取相关实现，按 `what → shape → how` 修改 code/resources。
6. 执行 proof；完全对齐后删除对应 status。

拒绝时，仅把有长期价值的理由写入 why。未经用户同意，不删除仍未实现的 Proposal。

## Proposal 最小结构

- Current baseline
- Proposed delta
- Impact map
- Alternatives
- Migration / cutover
- Compatibility disposition
- Proof plan
- Decision owner
- Admission blockers

Proposal 只描述相对当前正式规范的 Delta。正式条款只用 Clause ID 和链接引用，不复制正文；
研究笔记、聊天导出、实现进度和完整基线不进入 Proposal。

## Status 最小结构

- Target clauses
- Active physical gap
- Evidence / blocker

Status 不定义 Clause、不提出新设计、不保留完成项、日期快照、提交列表或完成百分比。

## Clause 搬移

1. 保留原 ID 和语义。
2. 在最适合回答核心问题的正式层建立唯一标题。
3. 删除旧定义，将引用改到新位置。
4. 同步 `scripts/checks/spec.mjs` 的前缀归属和 `docs/README.md` 导航。
5. 运行规范门禁，确认无重复、悬空或越权定义。

## 失败处理

发现正式语义冲突时停止产品语义修改。记录冲突位置、影响范围、可选裁决和 Decision Owner；
不得为了让门禁变绿而选边。可独立确定的导航、重复副本和快照污染仍可继续修复。

## Verification

先运行 `node scripts/checks/spec.mjs`，再运行 `npm run lint`。新增门禁时必须：

1. 提交永久的纯规则回归；
2. 临时引入目标反例并确认仓库门禁判红；
3. 恢复反例；
4. 重新运行正式检查。

影响更大时按 `proof/verify.md` 追加相应验证。

# 文档治理 — 执行程序

本文件是治理执行的直接依据。规则定义见 `what/document-governance.md`；理由见 `why/document-governance.md`。

## 阅读顺序

```text
读 what（行为）→ 读 shape（边界）→ 读 how（目标实现）→ 对照 status → 改实现面 → 用 proof 证明
```

禁止从 `proposal/` 或单独从 `what/` 直接改生产代码。

## 新增或修改产品行为

1. 若变更未裁决：在 `proposal/` 撰写候选，填齐最小模板（见下）。
2. 检查 `BaselineAdmissible`（GOV-007）：规范面无内部冲突；已知实现差距已在 `status/`；proposal 影响图完整。
3. 裁决接受后，同一变更内更新所有受影响的：
   - `why/`（理由与被拒方向，仅当有长期解释价值）
   - `what/`（行为）
   - `shape/`（边界与所有权）
   - `how/`（目标实现）
   - `proof/`（如何证明）
4. 实现尚未对齐：在 `status/` 建或更新精简条目。
5. 删除该 proposal 文件。
6. 实现追上后删除对应 status。

裁决拒绝：有价值的拒绝理由写入 `why/`，删除 proposal。

## Proposal 最小模板

```markdown
# <标题>

## Problem
## Current baseline
## Goal
## Non-goals
## Impact map
- what:
- shape:
- how:
- proof:
- code/resources:
## Alternatives
## Migration / cutover
## Compatibility disposition
Compatible | ExplicitMigration | ExplicitReset | CleanBreak
## Proof plan
## Decision owner
```

`Baseline Gate evidence` 如需引用，只指向 commit 范围的 CI/检查结果，不粘贴大段日志进仓库。

## status 最小内容

```markdown
# <主题>

目标：
- 对齐的 what/shape/how/proof 位置或条款 ID

当前：
- 未实现 / 部分实现 / 阻塞

缺口：
- 少量关键差距

阻塞：
- 仅在存在时填写
```

## 条款迁移与拆分

1. 确定条款核心规范命题属于哪一层；定义只保留在那一层。
2. 理由性段落迁 `why/`；所有权与写入口迁 `shape/`；算法与流程迁 `how/`；测试与门禁迁 `proof/`。
3. 非定义处用 ID 引用，禁止复制条款正文。
4. 主题文件跨层使用相同 slug。
5. 废止条款：删除定义，编号空缺；在导航中去掉；测试不得再依赖废止 ID。

## 矛盾处理程序

1. 停止用“选一边”继续实现。
2. 在 `status/` 或变更说明中记录矛盾与影响闭包。
3. 修复规范面至内部一致，或形成 proposal 后裁决。
4. 原子更新规范面；必要时更新实现面与 `status/`。

## 目录职责检查（人工 + 门禁）

| 检查 | 失败含义 |
|------|----------|
| `status/` 出现 Clause ID 定义（`## PREFIX-NNN`） | status 越权定义规范 |
| `proposal/` 被测试或代码当作唯一权威路径硬编码为实现依据 | proposal 污染实现 |
| 同一 Clause ID 在多个文件以定义标题出现 | 重复定义 |
| 引用 ID 无定义 | 悬空引用 |
| 正式规范文件未进入导航 | 导航不完整 |
| 旧权威路径 `spec/`、`docs/rfcs/`、`docs/decisions/` 仍被当作 SSOT | clean break 未完成 |

## 导航维护

`docs/README.md` 索引正式规范文件与前缀归属。增删正式文件后同步导航。`proof` 侧检查器验证：定义唯一、引用可解析、前缀归属、导航覆盖。

## 与实现门禁的分工

| 门禁 | 职责 |
|------|------|
| 文档检查（`scripts/checks/spec.mjs` 等） | 规范面文本一致性 |
| 架构/DSL 检查 | 实现面是否符合 shape/how 中的硬约束 |
| unit / integration / e2e | proof 声明的行为与契约 |
| `check:release` | 发布是否允许 |

Proposal 裁决不要求 `ProofGreen` 或实现已对齐；发布与合入生产行为仍要求相应 proof 与实现门禁。

## 本次体系切换程序

1. 建立七目录与治理三文（本文件及 why/what 对应文）。
2. 将保留的产品知识按职责写入正式层；条款 ID 不改号。
3. 未裁决未来设计进入 `proposal/`。
4. 已接受且仍有效的理由进入 `why/`。
5. 对照 `how/` 建立初始 `status/`。
6. 更新检查器与仓库内引用。
7. 删除旧权威目录与已消费 proposal。
8. 运行文档检查至通过。

切换后未迁入的旧规则废止（GOV-010）。
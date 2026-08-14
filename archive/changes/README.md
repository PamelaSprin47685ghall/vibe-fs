# Changes

本目录保存已批准变更的启动、实施和完成历史，不定义当前产品规范。当前产品语义仅以
`docs/{why,what,shape,how,proof}` 为准。

## Ownership

`proposed/` 由用户管理。所有进入 `proposed/` 的 Proposal 均已完成人工裁决并批准；
Agent 不重新判断 Proposal 是否批准。

Agent 默认不得在 `proposed/` 中创建、修改、重命名、移动或删除文件，不得主动选择工作。
只有当前用户明确要求启动指定 Proposal 时，Agent 才可以将该文件移动到 `active/` 并开始实施。

Proposal 的提出、讨论和裁决发生在 Agent 执行工作流之外，由用户或负责人管理。

## Lifecycle

```text
用户管理 proposed
    ↓ 用户明确启动指定 Proposal
active
    ↓ 正式 docs、实现与 proof 闭环
completed
```

- `proposed/`：已经批准，等待用户启动；正文仍由用户管理。
- `active/`：用户已经启动，实施或验证尚未闭环；Original proposal 已冻结。
- `completed/`：已经实现并验证完成，永久保存 Original proposal 与 Final outcome。

Active 文件顶部必须说明它是变更工作记录而非当前规范。Completed 文件顶部必须包含：

```markdown
> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。
```

目录位置是生命周期状态的唯一来源。文件正文不得维护 `status: proposed|active|completed`
等重复状态字段，也不建立 manifest、中央注册表或状态数据库。

## Core rules

1. 每项工作恰好对应一个文件；文件在三个目录之间移动。
2. 不创建平行的 Proposal、Status、Decision 或 Outcome 文件。
3. Change 文件不是当前产品规范，不得定义正式产品 Clause ID。
4. Change 可以使用独立的 `CHG-NNN` 编号；它不是产品 Clause ID，也不需要中央登记。
5. Proposal 从 `proposed/` 移入 `active/` 后，原始内容冻结。
6. 后续事实只追加到 `Active work`、`Amendments`、`Blockers` 或 `Final outcome`。
7. Amendment 只有在用户明确修改批准范围时才能追加；Agent 不自行批准范围变化。
8. Active 只维护有限 Remaining work、客观 blocker 和 Completion criteria，不维护每日或逐提交日志。
9. Completed 永久保存 Proposal 原文和用户批准的范围，不事后美化最初设想。
10. 普通小修复不必创建 Change；Agent 不为所有任务自动建立 Proposed 文件。

## Starting approved work

用户明确要求实施 `proposed/<file>.md` 时，Agent：

1. 读取指定文件，不重新裁决；
2. 将同一文件移动到 `active/`；
3. 保持 Original proposal 原文不变；
4. 追加有限的 `Active work`；
5. 按批准范围更新正式 docs、实现和 proof；
6. 不创建独立 Status。

发现正式规范矛盾或客观不可实施条件时，记录 blocker 并报告用户；不得缩减、扩大或重写批准范围。

## Closing work

批准范围、正式规范、实现和 proof 全部闭环后，在同一文件追加 `Final outcome`，至少记录：

- Outcome
- Final specification
- Implementation result
- Verification
- References

然后把同一文件移动到 `completed/`。Completed 是历史记录，不能用来解释当前产品行为。

## Migration note

从旧目录迁入 `proposed/` 的文件保留原文。原文中的旧生命周期措辞和旧路径只属于历史
Proposal 内容；当前状态只由所在目录决定。

从旧 Status 迁入 `active/` 且没有独立 Proposal 的文件必须明确标注 `Work origin`，不得伪造
Original proposal。

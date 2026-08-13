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

目录与 writer 边界见 `shape/document-governance.md`。本文件只规定 Agent 的执行顺序。

## 普通实现任务

```text
确认用户是否指定 Active Change
→ what
→ shape
→ how
→ 相关 Active 的工作来源、Remaining work、Completion criteria
→ code/resources
→ proof
```

没有相关 Active 时按正式 docs 直接完成普通小修改；不得为每项任务自动创建 Change。
Active 只限制批准范围和关闭条件，不替代正式规范。

## 启动 Proposed Change

用户明确要求实施 `changes/proposed/<file>.md` 时：

1. 读取用户指定的文件，不扫描其它 Proposed，不重新裁决。
2. 将同一文件移动到 `changes/active/`。
3. 将移动时已有正文整体视为 Original proposal 并冻结，不修改其字节或实质内容。
4. 只在文件末尾追加 `Active work`，记录 Specification impact、有限 Remaining work、
   Completion criteria 和当前客观 Blockers。
5. 按批准范围更新正式 why/what/shape/how/proof，再修改实现。
6. 不创建独立 Status、Decision 或 Outcome 文件。

Proposal 中缺少 Accepted、Decision Owner 或 Admission 信息不构成 blocker；进入 Proposed 已证明批准。

## Active 实施

实施期间只在以下时点更新 Active：启动、用户明确修改批准范围、出现 blocker、关闭。
不记录每日进展、每个 commit、完成百分比或大段代码快照。

用户明确修改批准范围时，只追加 Amendment：日期、Requested by、Change、Reason。Agent 不自行批准修订，
也不回写 Original proposal。

发现不可实施条件时，按 GOV-009 更新 Blockers 并报告用户；其它不受影响的工作可以继续。

## 关闭 Change

关闭前确认：

1. 批准范围已实现；
2. 正式 docs 内部一致；
3. 实现与 how 对齐，旧路径/旧行为按批准范围清除；
4. proof、测试和门禁通过；
5. 没有未解决 blocker。

然后在同一文件追加 Final outcome：Outcome、Final specification、Implementation result、Verification、
References。清理无长期价值的临时实施笔记后，把同一文件移动到 `changes/completed/`。
Original proposal 和用户批准的 Amendments 不得删除、压缩或美化。

## 旧 Status 迁移

- 有真实未完成工作：转为 Active，保留有效问题背景和关闭条件。
- 没有独立 Proposal：写明 Work origin，不伪造 Original proposal。
- 已完成且证据充分：保留背景，追加 Final outcome 后移入 Completed。
- 完成状态无法确定：移入 Active，在报告中列出确认需求，不猜测 Completed。
- 与同一 Proposal 属于同一工作：合并为一个文件；Original proposal 保持原文，Status 只转成 Active work。

## Clause 与路径维护

正式 Clause 搬移时保留 ID，删除旧定义并更新引用、前缀 owner 和 `docs/README.md`。
Change 文件只引用正式 Clause；`CHG-NNN` 不进入产品前缀表。

原 docs 下的 Proposal 与 Status 路径不得出现在当前导航、实现或检查配置中。
迁入 Changes 的冻结历史原文可以保留旧措辞；其生命周期含义以目录和 `changes/README.md` 为准。

## Verification

运行 `npm run format-build-test`。新增门禁必须有永久回归，并用受控反例
证明仓库入口会判红；恢复反例后重新执行正式检查。

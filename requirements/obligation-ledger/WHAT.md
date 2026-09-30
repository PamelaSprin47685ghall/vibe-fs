# obligation-ledger — WHAT

本包只规定 OpenCode 原生 todowrite 的宿主待办边界。插件不拥有第二套待办账本，不通过 assume、画板或 Magic Todo 重写 Host 的 todos 语义；插件额外拥有的只有 provider-facing retainCheckpoints，并在原生执行前把它从业务参数中隐藏。

## [001] 原生 todo 行不改写

Host 原生 todos 数组及其 content、status、priority 由 Host schema 与 executor 定义。插件不得给行补默认值、改状态、去重、改序、改文本或加入私有规划字段。retainCheckpoints 不是 TodoItem 字段，只是插件协议参数。

## [002] 完整列表原样交给 Host

每次 todowrite 的 todos 都按模型提交的完整数组原样交给原生 executor；插件只隐藏 retainCheckpoints。空数组、重复行、中文、多行文本及显式 priority 均不得被插件改写。Host 如何用该数组替换 UI 属原生 executor 行为，不在本仓复制实现。

## [003] 不维护第二份 desired/applied 待办投影

插件不得保存 canonical todo 内容、desired/applied snapshot、owner 待办副本或重放队列。待办 UI 的事实由 Host 原生 executor 拥有；本仓 durable projection 只记录压缩 checkpoint 的 ToolCallId 与 retainCheckpoints，不记录 todos 内容。

## [004] 压缩事实不能反推待办

TodoCheckpointCommitted 只证明某次成功 todowrite 建立了上下文压缩 checkpoint；它不能用于恢复、重建或判断 Host TodoTable 内容，也不能从 Host TodoTable 反推 compression checkpoint。

## [005] 失败执行不形成 checkpoint

tool.execute.before 只校验并隐藏 retainCheckpoints，不产生 TodoCheckpointCommitted；tool.execute.after 只恢复原始 provider 参数。Host 在 after 时 ToolPart 仍可能是 running，因此 checkpoint 的确认边界是随后 exact `message.part.updated` 的 `completed` 状态。若终态为 error、没有 terminal evidence 或 durable append 失败，不得伪称压缩 checkpoint 已提交。

## [006] 清单无裁决权

清单不授予权限，不判断测试、完成或退休。空清单、全部 completed 或自然语言自称完成都不是质量证书或可退休证明。

## [007] 历史 Magic Todo 只读

旧 TodoWritePrepared、TodoWriteAccepted、LegacyTodoSeedAdopted 等 Magic Todo 协议不得重新成为当前写入口、任务 authority、prefix 切换或 Host 回写依据。现有 legacy 接口可以明确拒绝；若未来提供审计读取，也必须是只读且不激活旧语义。

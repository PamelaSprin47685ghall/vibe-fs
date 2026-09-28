# obligation-ledger — WHAT

本包只规定宿主待办清单的兼容投影。认知画板、阶段与持久化归 cognitive-workspace，质量判断归 relay-assessment，退休条件归 relay-retirement。

## [001] 有界输入

`assume.todos` 每行必填 `content`、`status`；status 仅为 `pending | in_progress | completed | cancelled`，可选 priority 缺省 medium。向固定 Host 版本输出完整行，不追加私有规划字段。输入兼容不等于与 Host schema 字节同构。

## [002] 完整替换

按 owner 的物理 session 整体替换清单，保留行内容与顺序。空列表明确清空该 session 的 UI，不表示质量通过或允许退休。

## [003] 投影幂等

最新 committed snapshot 的 todos 是 desired，已确认写入 Host 的 snapshot identity 是 applied；不一致时只重投最新值。写入前确认 owner 仍占用该 session；上一任的迟到写入或 ack 不得覆盖下一任清单或回退 applied。

## [004] 单向真相

Journal facts 与认知快照是唯一语义真相，Host TodoTable 只是单向投影。不得从 Host 表恢复画板、todos 或阶段，也不得刷回旧账；漂移修复只重新投影既定事实，不产生新的语义副作用。

## [005] 同步失败

Host 写入失败不回滚已提交快照、不重跑 jq，也不宣称全部交付。返回完整画板并如实报告 `todo_sync=pending | applied`，系统重试未交付投影；此状态不属于 TodoItem。已冻结的首次返回值不得被未来请求改写。

## [006] 无裁决权

清单不授予权限，不判断测试、完成或退休。空清单、全部 completed 或画板自称完成，都不是质量证书。

## [007] 历史只读

旧 `TodoWritePrepared`、`TodoWriteAccepted`、`LegacyTodoSeedAdopted` 保留受控读取供审计与迁移，不得在新 Life 激活旧承诺门禁、lag-1 前缀切换或 Host 回写。

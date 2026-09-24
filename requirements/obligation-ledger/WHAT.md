# obligation-ledger — WHAT

本包只规定宿主待办清单（TodoTable/UI）的兼容投影。认知画板、阶段提交与持久化语义归 `cognitive-workspace`；质量判断归 `relay-assessment`；退休条件归 `relay-retirement`。

## [001] 输入规范化是有界兼容面，不是官方 schema 字节同构

`assume` 的 `todos` 输入是有界兼容面：`content` 与 `status` 必填，`status` 取 `pending|in_progress|completed|cancelled`；`priority` 可选、缺省 `medium`。向固定 Host 版本输出时投影为该版本要求的完整行。禁止追加任何私有规划字段（`planComplete`、`workingOn`、`horizon`、`obligations`、`revision` 等）。有界输入不宣称与宿主 schema 字节同构。

## [002] 按 owner 的完整列表替换与空清单清除

投影按 owner 的物理 session 执行完整列表替换：提交的 `todos` 整体覆盖该 session 的现有清单，行内容与顺序不被工具改写。`todos=[]` 合法，表示明确清空该 session 的 UI 清单，不表示质量通过，也不表示允许退休。

## [003] desired/applied 幂等与上一任迟到不覆盖

最新 committed snapshot 的 todos 是 desired；已证实写入宿主的 snapshot identity 是 applied。两者不等时重新投影最新值。宿主 UI 写入前必须验证该 owner 仍是该 session 的当前逻辑占用者；上一任的迟到写入与迟到 ack 不得覆盖下一任的清单，也不得回退 applied cursor。

## [004] 宿主 UI 不是 canonical 真相源

Journal facts 与认知画板快照是唯一语义真相源，宿主 TodoTable 仅是单向兼容投影。禁止用 Host 表反推或恢复画板、todos 或阶段；禁止把 Host sink 刷回旧账；状态漂移只允许执行无副作用的纯投影修复。

## [005] 同步失败诚实分型且不重跑 jq

宿主写入失败属于外部副作用未交付，不是认知提交失败：已持久提交的快照不回滚、不重新执行 jq、不伪造全局成功。工具可以返回完整画板并明确 `todo_sync=pending`，由系统重试投影；确认写入时为 `applied`。这两个值是宿主交付状态，不是 TodoItem 的业务字段。已冻结的首次返回值不得在未来的请求里被就地改写。

## [006] 禁止反向成为语义权威

待办清单不判断测试是否通过，不授予角色权限，不决定任务是否完成，不构成可退休证明。清单为空、清单全部 completed、或画板里写了「完成」，都只是模型自己的声明，不是系统结论。

## [007] 历史解码只读

历史 Journal 中的旧 `todowrite` 事实（`TodoWritePrepared` / `TodoWriteAccepted` / `LegacyTodoSeedAdopted`）保留受控的 legacy 读取能力，仅供审计与迁移。旧事件不得在新 Life 激活任何承诺门禁、lag-1 前缀切换或 Host 回写。

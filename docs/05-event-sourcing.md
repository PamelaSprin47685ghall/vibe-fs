# 05 — 事件溯源与持久化

## SSOT

工作区根目录：

```text
[workspace]/.wanxiangshu.ndjson
[workspace]/.wanxiangshu.ndjson.lock
```

**唯一 durable 真相**（review task、backlog、nudge 去重等）。宿主 `session.messages`、compaction 后注入的 anchor **不是** SSOT。

## 六条纪律

1. **意图不落盘**：未校验的自然语言、草稿参数不写入。
2. **事实不可改**：仅追加行；修正靠补偿事件，非覆盖旧行。
3. **内存 = fold**：`ReviewStore`、backlog 投影、nudge 表 = 对 NDJSON 的纯 fold + 可选进程缓存。
4. **先盘后内存**：append 成功后才更新缓存投影；失败 = 命令未发生。
5. **一行一事件**：每行自包含 JSON（Shell 解析为 `WanEvent`）。
6. **按 session 分区**：每行含 `session` 字段；fold 过滤 `sessionId`。

## 事件种类（Kernel SSOT）

定义于 `src/Kernel/EventLog/Types.fs`：

| `Kind` 常量 | 含义 |
| :--- | :--- |
| `loop_activated` | With-Review 激活，payload 含 `task` |
| `loop_cancelled` | 取消 loop |
| `review_verdict` | 审查结论（accepted / needs_revision / terminated / cancelled） |
| `work_backlog_committed` | todowrite/task 校验通过后全量 todos + 五报告 + `select_methodology` |
| `nudge_dispatched` | nudge 已派发（含 action、anchor） |
| `submit_review_wip_recorded` | WIP 提交记录 |
| `nudge_dedup_cleared` | 去重表清空（如新用户消息） |
| `assistant_completed` | 助手轮次完成（辅助 nudge 快照） |

另有 **万象阵** 相关 kind（`squad_*`、`task_*`）在同文件定义，由万象阵运行时写入**同一或独立**日志策略见万象阵 PRD；万象术核心 fold 主要消费 review/backlog/nudge 族。

## 信封字段（概念）

与 PRD-02 一致：`v`、`session`、`kind`、`at`、`payload`、`id`、`host` 等由 Shell codec 序列化；Kernel `WanEvent` 使用 `Map<string,string>` payload 参与 fold。

## Fold 函数

见 `src/Kernel/EventLog/Fold.fs`：

- **`foldReviewTask`**：`loop_activated` 设 task；`review_verdict` 终局 verdict 清空；`loop_cancelled` 清空。
- **`foldWorkBacklogSnapshot`**：取最后一次 `work_backlog_committed`。
- **`foldNudgeDedup`**：记录已派发 anchor；WIP / dedup_cleared 重置策略。
- **`foldNudgeSnapshot`**：供 nudge 决策的聚合视图（open todos、loop 是否活跃等）。

`SessionState`（Shell 缓存）聚合上述投影，供 `EventLogRuntimeNudge` / `Sync` 读取。

## 写入 API（Shell）

`EventLogRuntimeAppend.fs` 暴露业务级 `appendLoopActivated`、`appendWorkBacklogCommitted`、`appendReviewVerdict` 等；内部统一 `appendAndCache`。

**`work_backlog_committed`**：在 `todowrite`/`task` 工具校验通过后调用，payload 由 `TodoWriteArgs` 构造。

## 锁与并发

- 跨进程：`O_CREAT|O_EXCL` 锁文件（架构测试 `eventLogUsesAdvisoryFlock` / proper-lockfile 策略以代码为准）。
- 进程内：`PromiseQueue.SerialQueue` 串行化 append。

## 损坏行处理

读盘时遇到截断/非法行：**在该行截断**，丢弃该行及之后字节，不跳过坏行继续 fold（避免建在错误基线上的状态）。

## 启动与恢复

1. 打开 workspace → `EventLogStore` 读 NDJSON → 构建 `SessionState` 缓存  
2. `syncAllSessionsFromEventLog` / 单 session `syncReviewFromEventLog`、`syncBacklogFromEventLog`  
3. 再注册宿主 hook  

**文本回放备选**：`ReviewReplaySync.syncReviewFromTexts` 从对话文本推断 task，仅作 fallback；首选 **事件重放**（`EventLogRuntimeSync`）。

## 与 compaction 的关系

宿主压缩上下文时，万象术**不**依赖「compaction 前消息」或 multi-frontmatter 锚点恢复 backlog/review。展示层仍可输出 front-matter 供 LLM 阅读，但程序判断以 fold 为准。

## 验证

- 单元：`tests/` 中 EventLog fold / codec 测试
- 架构：`nudgeDedupMustUseEventLogFold`、`nudgeLoopStateMustReplayHistory`
- 关键套件：`EventLogFoldTests`、`EventLogCodecTests`、`EventLogRuntimeTests`、`NudgeEventSourcingTests`

## 相关文档

- [06-review-and-nudge.md](./06-review-and-nudge.md)
- [07-work-backlog.md](./07-work-backlog.md)
- [18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)
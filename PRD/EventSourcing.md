# 万象术：工作区事件溯源（`.wanxiangshu.ndjson`）

> **规格 SSOT**。实现与测试须与本文件一致；与旧「宿主消息历史 + compaction 补锚点」叙事冲突时，以本文件为准。

## 0. 动机

OpenCode / Mux 等宿主会对 **context 做 compaction**，`session.messages` 与 `experimental.chat.messages.transform` 切片 **均不可靠** 作为 review / todo / nudge 的长期真相。

旧方案：从对话文本 fold `inferReviewTaskFromTexts`、从 tool 结果 fold backlog、compaction 后 `prompt()` 注入 `See above for some messages before compaction.` + 多块 front-matter 补锚点。复杂、易漏、与宿主折叠策略耦合。

新方案：**工作区根目录** `[workspace]/.wanxiangshu.ndjson` 为万象术 durable 语义的唯一真相；宿主历史仅作 LLM 上下文，不作状态机 SSOT。

## 1. 公理

| 公理 | 含义 |
|------|------|
| 意图不落盘 | 用户/模型自然语言、未提交的 tool 参数、内存中的「打算做」**不**写入 NDJSON |
| 事件才落盘 | 命令经校验并产生 **不可抵赖事实** 后，追加一行事件（例：`/loop` 激活、`submit_review` 受理、`todowrite` 成功提交、reviewer `return_reviewer`） |
| 当前状态 = 积分 | `ReviewStore`、`WorkBacklog` 投影、`Nudge` 去重表等内存结构 = 对 NDJSON（按 `session` 过滤后）**纯 fold** 的结果 |
| 先写盘后改内存 | append 成功 → 再更新内存；append 失败 → 等同该命令未发生 |
| 一行一事件 | NDJSON：每行一个自包含 JSON 对象；**禁止**用 JSON 数组文件做追加 |
| 按 session 分区 | 每行 **必须** 含 `session` 字段（宿主 session id）；fold 时只消费匹配 session 的行（全文件顺序仍全局单调） |

## 2. 物理文件

```
join(workspaceRoot, ".wanxiangshu.ndjson")
```

- **追加**：只写文件末尾；损坏行在恢复时截断（该行及之后丢弃），不跳过坏行继续 fold。
- **锁**：侧边文件 `[workspace]/.wanxiangshu.ndjson.lock` 以 `wx` 独占创建；释放时 `unlink`。读写与 `nudge_dispatched` claim 均在锁内；同进程另经 `SerialQueue` 排队。
- **启动**：插件激活或 session 首次需要投影时，对目标 `session`（或全 workspace）**重放** NDJSON → 填充 `ReviewRuntime` / backlog 投影 / nudge 相关快照；**禁止**用「仅读宿主历史」替代重放作为 loop/backlog 真相。

## 3. 行格式（契约）

每行 JSON 对象最低字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `v` | int | 事件 schema 版本，从 `1` 起；演化时 Kernel 提供 `upgradeEvent` 纯升级链 |
| `session` | string | 宿主 session id |
| `kind` | string | 事件类型（见 §4） |
| `at` | string | ISO-8601 时间（审计用；**逻辑 fold 不依赖**时间排序，依赖文件行序） |
| `payload` | object | 类型由 `kind` 决定 |

可选：`id`（UUID，幂等去重）、`host`（`opencode` \| `mimocode` \| `mux` \| `omp`）。

**禁止**在 payload 中存整段宿主 `obj`；只存 Kernel 已定义的 record/DU 编码。

## 4. 事件类型（初版清单）

实现阶段以 `src/Kernel/EventLog/`（待建）为枚举 SSOT；文档层约定语义：

| `kind` | 何时 append | payload 要点 |
|--------|-------------|--------------|
| `loop_activated` | worker With-Review 激活（含 `/loop`、等效命令） | `task: string` |
| `loop_cancelled` | 用户取消 With-Review | — |
| `review_verdict` | reviewer 结论写回父会话语义成立时 | `verdict`: `accepted` \| `rejected` \| `terminated` \| `cancelled`；`feedback?` |
| `work_backlog_committed` | `todowrite` / `task`（Mimocode）校验通过并采纳 | `todos`；五份报告字段 + `select_methodology`（与 `WorkBacklog` 一致） |
| `nudge_dispatched` | nudge 在锁内 claim 成功后落盘（先于或伴随宿主投递） | `action`: `nudge-todo` \| `nudge-loop` \| `nudge-runner`；`anchor`: 当前 assistant 文本 |
| `submit_review_wip_recorded` | `submit_review` WIP 受理 | — |
| `nudge_dedup_cleared` | nudge 去重状态清除（新用户消息或 submit_review_wip 后） | — |

后续可增 `fallback_*`、`methodology_note` 等；增 kind 须先改 Kernel 类型 + 本表 + 测试。

**与 front-matter 关系**：宿主消息 **仍可** 带 YAML front-matter 供 LLM 阅读；**不得**再作为 `inferReviewTaskFromTexts` / backlog replay 的 SSOT。front-matter 由 append 成功后的 **投影编码** 生成（单向：事件 → 展示），而非展示 → 状态。

## 5. 分层职责

```
Host hook / tool execute
    → Shell: decode obj → Kernel Command
    → Kernel: validate → Event list（0..n 行待追加）
    → Shell: withFileLock → append NDJSON → on OK fold into memory
    → Shell: optional encode 宿主消息（非 SSOT）
```

| 层 | 职责 |
|----|------|
| `Kernel/EventLog/*` | 事件 DU（`Types.fs`）、按 session fold（`Fold.fs`：`foldReviewTask`/`foldWorkBacklogSnapshot`/`foldNudgeDedup`）、版本升级纯函数 |
| `Shell/EventLogFiles.fs` | 路径、锁、append、读全文件/按 session 流式读、损坏截断 |
| `Shell/EventLogRuntime.fs` | 与 `ReviewRuntime`、`ReviewReplaySync` 对接：启动重放、append 与投影同步 |
| 宿主 `MessageTransform*` | 移除 compaction-anchor 注入；caps/backlog **展示** 可读投影，不从历史 fold SSOT |

## 6. 删除或废弃的行为（迁移目标）

| 旧行为 | 新行为 |
|--------|--------|
| `inferReviewTaskFromTexts(session texts)` 作 loop SSOT | `foldReviewTaskFromEvents(session, lines)` |
| `replayBacklogWith` 从消息 fold 五份报告 | `foldBacklogFromEvents` |
| compaction 后 `buildCompactionAnchorPrompt` + `prompt()` 锚点 | **删除**；compaction 只影响 LLM 窗口，万象术状态已在 NDJSON |
| `syncReviewProjection` 从 transform 读 texts | `syncReviewProjection` 从事件 fold（或内存已由重放初始化） |
| `ReviewReplayPolicy` 依赖「全量 messages」 | 改为事件重放 + 可选宿主文本仅作 UI |
| `experimental.session.compacting` 保留 durable backlog 语义 | 缩为「compaction 前无需补锚」；若仍 hook，仅日志/无操作 |
| PRD/README「历史优先于内存」指 **对话历史** | 改为 **`.wanxiangshu.ndjson` 优先于内存**；对话历史非真相 |

**保留**：`PromptFrontMatter` 解析（工具输出、用户可读锚点）；`LoopMessages` 字段名常量（编码事件 payload / 展示文案）；`inferReviewTaskFromTexts` + `ReviewReplaySync.syncReviewFromTexts` 作为宿主文本 fallback 保留，事件重放（`EventLogRuntime.syncReviewFromEventLog`）为首选路径。

**当前进度**：`Kernel/EventLog/`（`Types.fs` + `Fold.fs`）+ `Shell/EventLogCodec.fs` + `Shell/EventLogFiles.fs` + `Shell/EventLogRuntime.fs` 已建成；`foldReviewTask`/`foldWorkBacklogSnapshot`/`foldNudgeDedup` 纯函数已实现；NDJSON append + 文件锁 + 损坏行截断已实现；`EventLogRuntime.syncReviewFromEventLog` + `isLoopActiveFromEventLog` + `tryClaimNudgeDispatch` 已接入宿主。compaction-anchor 完全删除尚未完成。

## 7. 实施顺序（任务强制）

1. **文档**：`README.md`、`PRD/PRD.md`、`PRD/EventSourcing.md`（本文件）、`AGENTS.md` 指针。✅
2. **测试**：`tests/EventLogFoldTests.fs`、`tests/EventLogCodecTests.fs`、`tests/EventLogRuntimeTests.fs` — fold 纯函数、损坏行截断、append 顺序、锁下串行。✅
3. **开发**：`Kernel/EventLog/` + `Shell/EventLogCodec.fs` + `Shell/EventLogFiles.fs` + `Shell/EventLogRuntime.fs` + 各宿主 append 接线。✅ `inferReviewTaskFromTexts` 仍存在作为宿主文本 fallback（`ReviewReplaySync.syncReviewFromTexts`），事件重放（`EventLogRuntime.syncReviewFromEventLog`）为首选路径。

## 8. 验收标准

- ✅ 重启进程后,仅依赖 `.wanxiangshu.ndjson` 可恢复:当前 `task`(With-Review)、最新 backlog 全量 `todos` + 最近 `work_backlog_committed` 五份报告、nudge 所需快照。
- ⬜ OpenCode compaction **后** 不注入 anchor prompt,上述状态仍正确。(迁移中:`inferReviewTaskFromTexts` 文本 fallback 仍存在)
- ✅ 架构测试:`Kernel` 无 `Dyn`;事件 fold 在 `Kernel`;append 仅在 `Shell`。
- ✅ `npm run build-and-test` 全绿。

## 9. 与附录对照

- 替代 PRD §2.2「历史是事实」中 **对话历史** 段落 → 指向本文件 §1。
- 替代 PRD §3.9「Compaction 补锚点」→ §6 删除表。
- 替代 PRD 附录 M「inferReviewTaskFromTexts 与 ReviewStore」→ 事件重放序列（实现后补伪代码于 `EventLog/Fold.fs` 文档注释）。

---

*版本：与 with-review 任务「事件溯源」同步；实现见 `src/Kernel/EventLog/`、`src/Shell/EventLog*`。*
# 架构 — 控制结构与分层边界

行为不变量见 `what/architecture.md`。FLOW 展开见 `what/flow.md`。

## ARCH-001：结构化程序替代状态机

业务控制流只用语言提供的 continuation：`let!` / `do!` / `match` / 有界递归 / computation expression。

禁止把「程序下一步去哪」固化为领域字段：CurrentStage、NextAction、JoinOwner、ReviewPhase、FallbackPhase、NudgeLease、CompactionGeneration、SquadWaveState 等。

判断：字段是物理世界事物（进程、Session、Git tree、文件、模型输出），还是程序计数器？后者删除。

### 分层所有权

```text
Kernel / Domain     纯规则、事实、投影 — 无 Host I/O
Application         直接执行的 CE workflow
Infrastructure      Host hooks、Git、codecs、资源加载
Session / Process   运行时 cell、fallback、review、PTY 所有权
```

依赖方向：上层可依赖下层；Domain/Kernel 不得依赖 Session/OpenCode。  
资源读取仅在 `Infrastructure/Resources/`。

### Magic Todo 分层落点（交叉引用，不重定义 TODO-*）

| 关注点 | 层 | 唯一 owner / 条款 |
|--------|----|-------------------|
| tagged V2 `todowrite` schema / codec / examples / result 形态 | Infrastructure（`tool.definition` 同源模块） | TODO-002 |
| canonical todo 真值 `MagicTodoProjection` | Kernel / Domain 投影 | TODO-007；PROJ-009 |
| Host TodoTable | Infrastructure compatibility sink | TODO-007；非 canonical |
| process / Finality 工作记录 | 既有 canonical LWR machinery | TODO-008；TODO-012；COMPANION-003 |
| Manager lag-1 prefix | 既有 `ActivePrefixEpoch` + `EvidenceKind=TodoCheckpoint` | TODO-009；ARCH-004 |
| Opening floor（非 Activation） | Domain 结构性 cursor | TODO-001 |
| Manager-only continuous guidance 片段 | Application / Prompt surface | TODO-001；TODO-013；PROMPT-013；HOST-013 加法 |
| V2 runner 无等价 hook | Session / Process gate | TODO-004 fail-closed |

禁止：

```text
第二套 LWR / TodoProcessReviewEvidenceProjection / Y-complete reviewer projection（TODO-008/012）
平行 PrefixEpoch / todo-only rebase SSOT（TODO-009/012）
把 Host TodoTable 或 bridge 提升为 Domain 真值（TODO-007）
在 Domain 固化 TodoStage / ReviewStage / AwaitingReview 等程序计数器（TODO-012）
经 Manager 固定 surface 泄漏 reviewer / session / barrier / witness / 2N（TODO-013；GLORY-030 窄例外）
```

`PrefixRebaseCommitted.EvidenceKind = TodoCheckpoint` 是 ARCH-004 既有冷边界的合法新 evidence，不是新的 epoch 状态机；desired cutoff 由 Accepted 链推导，committed epoch 仅在下一 provider attempt seal 前原子提交（TODO-009）。

## ARCH-009：有界并发

业务层扇出**唯一**原语：

```text
mapBounded : maxConcurrency → CancellationToken → ('t → ct → Task<'u>) → 't seq → Task<'u list>
```

| 规则 | 要求 |
|------|------|
| 上限 | `maxConcurrency` 为正有限；禁止 0=无界或 0=1 |
| 结果序 | 按输入下标，不按完成序 |
| 空输入 | 空结果，action 零次 |
| 取消 | 取许可前观察 token；token 传给每个 action |
| 拒绝 | 任一 action 抛出 → 立即拒绝；已获许可的 action 跑完；许可必须归还 |

禁止业务层无界 `Promise.all` / `Task.WhenAll` 盖全集。适配器内部实现有界原语除外。

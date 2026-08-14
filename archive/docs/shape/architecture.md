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

## 命名与工具名所有权（ARCH-006 / ARCH-007）

| 不变量 | Owner | 禁止 |
|--------|-------|------|
| 人=名词（Role/Persona/office）；工具=动词 | 全库 provider surface | Role 与 Tool 同名承载不同语义（已删 Executor/`executor`） |
| 不同硬语义 → 不同名 | 各 tool schema owner | `commission` 冒充 `fork`；`judge` 冒充 `verdict` 工具名 |
| same tool name → 唯一 schema + 唯一 semantic contract | Gate A（ARCH-016） | 仅 schema 相同却生命周期/失败语义分叉 |

`join` / `horizon` 可跨 Role 共享，当且仅当合同完全同一。

## Provider Horizon 所有权（ARCH-014）

> Horizon 无状态机、无 UUID。

| 层 | 拥有 | 不拥有 / 禁止泄漏 |
|----|------|-------------------|
| 墙内机械 | Journal、CAS、cursor、SessionId、AgentId、JobId、PtyId、spool、fallback offset、`fast-`/`deep-` binding | 不得投影进 provider tool result / schema / fixed prose |
| Provider Horizon | 「发生了什么 + 下一步可做什么」；measurement / consequence | `status`/`code`/`error` DTO；回声已证事实；逼模型解码 DU |
| 各域 tool renderer | 本域后果叙述（服从 decision filter） | 私造第二套机器态暴露面 |

Horizon 法则正文见 `what/architecture.md` ARCH-014；本条只钉所有权：机器可尽知，参与者只见 horizon。

## WorkRecord 陈述边界（ARCH-015）

WorkRecord 陈述 = prose，不是 schema（what ARCH-015）。正式陈述在 Recent work 最后一条助手文本；无 Closing report 段。  
machine-semantic 结构只留协议真需处（如 `exit_code`、`verdict` 参数、`root_requirement`）。  
禁止 per-role fixed report DTO；WorkRecord 三标题所有权见 companion/glory。

## Gates A–F 所有权（ARCH-016）

| Gate | 守什么 | 失败面 |
|------|--------|--------|
| A Tool Referential Integrity | 同名工具唯一 schema+semantic owner（ARCH-007） | proof / 静态扫描 |
| B Provider Leak | SessionId/AgentId/JobId/PtyId/Fission/lane/worktree/offset/`fast-`·`deep-`/spool 不得出 horizon | provider 输出契约测试 |
| C Language Parity | 每个 provider semantic resource：EN + zh-CN（HOST-026）；叶对 + `{{placeholder}}` 集合一致 + Role Law 与高风险 tool description semantic-anchor 同 ID 双语命中（PROMPT-019/020） | 资源装载 / 缺语言 fail / 占位符集合不一致 / 缺锚点 |
| D Prompt Stability | 同 session：fallback / T1 / review / reanchor / Strength → system prompt 字节相同（AGENT-029、FALLBACK-014） | Persona/prompt 回归 |
| E Provider Prose Ownership | 已知 provider-surface owner 禁新增 NL literal；baseline 只减不增（PROMPT-019） | `provider-prose-ownership` 扫描红 / per-file 计数回归 |
| F Office Capability Integrity | 五 Office entitled consequence 在 Manager Role Law 与 `fork` description 等同 ID 命中（ARCH-017） | 投影缺锚点 / 把所有 Office 写成 witness |

Gate 是可失败门禁锚点，不是业务状态机字段。实现与 proof 拥有可红证据；各域不得以「局部方便」绕过。

## Office Capability 所有权（ARCH-017）

| 关注点 | 唯一 owner | 投影（不得另造真源） |
|------|------|------|
| 五 Office entitled consequence | ARCH-017 | Manager Role Law；`fork` description；各 Role Law 自我模型；caller-facing tool |
| 权限矩阵 | AGENT-006 | 不替代 capability model |
| forkable 集合 | AGENT-009 | 不替代后果描述 |
| 调用瞬间 affordance | PROMPT-020 | tool description + argument semantics |

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

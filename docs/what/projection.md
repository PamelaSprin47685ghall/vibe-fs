# Projection Algebra — 行为

条款前缀：`PROJ-`。承接 COMPANION-007 与 VERIFY-007。合成 TOML 记法在 ARCH-010，不承载 PROJ-。

## PROJ-001：投影是代数

Provider-visible 消息投影必须用 typed 组合子/直接执行的 computation expression 表达。禁止各功能直接接收并任意修改 `Message list`。

投影的唯一生产路径是同构的纯函数/直接执行 CE 管线（FLOW-001），不存在 `ProjectionProgram` AST + Interpreter 中间层。

## PROJ-002：输入是事实快照

投影 DSL 核心输入为不可变的 `ProjectionSnapshot`。字段集为**消费者驱动子集**（DSL-003）：
只承载当前已接线 intent 实际读取的事实，不得假装与完整目标形态同构。

**当前实现字段**（与 `ProjectionAlgebra.ProjectionSnapshot` 对齐）：

```fsharp
type ProjectionSnapshot =
    { CurrentProjection: ProviderSemanticProjection
      CommittedPrefix: PrefixSnapshot option
      BlogFrames: ResolvedBlogFrame list
      /// host message id 字符串集合；生产路径当前恒空
      TransportMessages: Set<string>
      HostReanchor: HostReanchorFact option }
```

**目标 / 完整形态**（尚未作为当前权威同构实现；待后续变更按消费者落地）：

```fsharp
// Attempt / PhysicalTimeline / SemanticEvents / ActivePrefixEpoch /
// CandidatePrefixProbe / LocalPendingParts 等——规范方向，非当前权威字段集
```

## PROJ-003：输出管线

核心输出依次为：

```text
SemanticEventTree
  → ProviderSemanticProjection
  → ProviderWireProjection
  → ProviderInputSeal
```

`ProviderWireProjection` 与 `ProviderSemanticProjection` 是不同类型（VERIFY-007），不得隐式互转。

## PROJ-007：DSL 不负责生命周期

DSL 只负责不可变快照 → 确定性 provider-visible projection。

DSL 不负责：启动或等待任何 Agent/provider、执行工具、写 Journal、恢复 Prompt、管理 ProviderRunIdentity、推进生命周期状态、控制器在线更新。

## PROJ-009：MagicTodoProjection 与单一工作记录投影

### Canonical todo

```text
MagicTodoProjection = Manager Life 的 canonical todo list SSOT
Host TodoTable       = compatibility sink only
```

语义与 settlement / reconciliation owner 见 TODO-007（settlement 规则 TODO-005）；本条只约束投影侧：

- provider-visible todo 真值只读 `MagicTodoProjection`（及由其推导的 tool result 字段）；
- 禁止把 Host TodoTable、session head 文本或 tool args 反推为 canonical；
- REVISE settlement 后的 sink 对齐不产生 checkpoint / review / 新 ProjectionIntent。

Manager-only `MagicTodoManagerGuideline` 片段的装配条件见 PROMPT-013 / TODO-013；HOST-013 pair marker 仍不占 ProjectionIntent。

### 禁止第二套 LWR / 工作记录 renderer

Process-review input、process-review report、Finality reviewer work record **一律**复用既有 canonical `LifecycleWorkRecord` machinery（COMPANION-003；bounded LWR / coverage 分型见 TODO-008；禁止平行 owner 见 TODO-012）。

禁止新增平行投影：

```text
TodoProcessReviewEvidenceProjection
Y-complete reviewer projection
独立 ReportRef summarizer / session-head LWR 冒充 frontier-bounded LWR
```

coverage 分型（不得在投影层互转）：

```text
RecordCoverage + bounded LWR（允许 canonical RawGap）→ process / Finality review 证据
PrefixCoverage + proven Y only（禁止 RawGap）        → Manager lag-1 prefix rebase
```

TodoCheckpoint 冷边界与 desired≠committed epoch 见 TODO-009 / ARCH-004；投影只消费已提交 `ActivePrefixEpoch`，不在 DSL 内提交 epoch。

### V2 schema 不在投影层

provider-visible `todowrite` V2 parameters / jsonSchema / description 的唯一 owner 是 `tool.definition` 编解码模块（TODO-002），不经 ProjectionIntent 复制第二份 schema；V2 hook 门禁见 TODO-004。

## 相关条款定义位置

以下条款按 GOV-011 定义于 shape/how，本表仅为导航，不重复定义。

| 条款 | 定义位置 |
|---|---|
| PROJ-001 | 本文件 |
| PROJ-002 | 本文件 |
| PROJ-003 | 本文件 |
| PROJ-004 | [`shape/projection.md`](../shape/projection.md)（PROJ-004：三层结构） |
| PROJ-005 | [`shape/projection.md`](../shape/projection.md)（PROJ-005：ProjectionIntent） |
| PROJ-006 | [`shape/projection.md`](../shape/projection.md)（PROJ-006：合并与冲突） |
| PROJ-007 | 本文件 |
| PROJ-008 | [`how/projection.md`](../how/projection.md#proj-008迁移顺序) |
| PROJ-009 | 本文件 |
| Magic Todo 语义 | [`what/todo.md`](todo.md)（TODO-002/004/005/007/008/009/012/013） |

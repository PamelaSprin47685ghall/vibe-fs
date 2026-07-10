# 03 — Kernel 纯规则层

## 职责

Kernel 承载**宿主无关**的稳定语义：状态机、事件 fold、工具元数据、权限规则、提示词片段、纯解析与纯算法。模块约 **92** 个 `.fs` 文件，按子目录与顶层文件组织。

## 子系统地图

| 簇 | 路径 | 核心内容 |
| :--- | :--- | :--- |
| Review | `ReviewSession/` | `Types`、`StateMachine`、`Registry`、`Query`、`Effects`、`Facade` |
| Nudge | `Nudge/` | 推导、registry、retry、submit_review hooks、todo 状态 |
| EventLog | `EventLog/` | `Types`（kind 常量）、`Fold`（纯 fold） |
| Fallback | `FallbackKernel/` | 降级决策、恢复、状态机 |
| 工具 SSOT | `ToolCatalog/` | `ToolSpec`、`Registry`、`Classification`、分族 FileIO/Web/Search/… |
| Backlog | `BacklogProjectionCore`、`BacklogProjection`、`WorkBacklog` | 从事件/参数投影待办展示 |
| 消息语义 | `Messaging`、`Message`、`MessageDedup`、`MessageTransformPolicy`、`ReviewReplayPolicy` | 角色、部件、去重、replay 策略 |
| 子代理元数据 | `Subagent`、`SubagentIntents`、`SubagentToolPolicy` | 意图、策略，非 spawn |
| 方法论元数据 | `Methodology/`、`MethodologyCatalog` | `select_methodology` 枚举与 todowrite 文案 |
| 提示词 | `CapsPrelude`、`CapsFormat`、`PromptFragments`、`LoopMessages`、`ReviewPrompts/`、`SearchPrompts`、`SubagentPrompts`、`OmpPrompts` | 宝典/铁律与片段 SSOT |
| 纯算法 | `FuzzyQuery`、`FuzzyPath`、`FuzzyFormat`、`Executor`、`TreeSitterKernel`、`Domain`、`Yaml`、`PatchParser` | 无 IO |
| 宿主命名 | `HostTools` | 四宿主工具名映射 |
| 权限 | `ToolPermission` | 角色 → 工具语义 |
| 其他 | `Config`、`ToolCopy`、`ToolArgs`、`ToolResult`、`WebFetchGuard`、`ReviewVerdict`、`WarnTdd` | 横切 |

## ReviewSession 状态机（概念）

状态 DU 消除非法组合；转移在 `StateMachine.fs`。典型状态：

- `Inactive` / `Active(task)` / `Locked(task, reviewerId)` / `Accepted` / `NeedsRevision(feedback)`

命令侧：`Activate`、`Submit`、`Lock`、`Unlock`、`Accept`、`RequestRevision` 等。  
**发出的事件种类**与 PRD 表一致，见 [06-review-and-nudge.md](./06-review-and-nudge.md)。

## EventLog Fold（纯函数）

`Kernel/EventLog/Fold.fs`：

| 函数 | 输出 |
| :--- | :--- |
| `foldReviewTask` | 当前 loop task `string option` |
| `foldWorkBacklogSnapshot` | 最新 backlog 快照 |
| `foldNudgeDedup` | 已派发锚点集合等 |
| `foldNudgeSnapshot` | nudge 决策用聚合快照 |
| `foldEventStream` | 通用骨架 |

Payload 在 Kernel 层多为 `Map<string,string>`（与 Shell codec 解耦）。

## ToolCatalog

`ToolCatalog.Registry.all` 列出核心工具 spec（coder、investigator、read、write、fuzzy_*、web*、submit_review、return_reviewer、executor*、continue 等）。  
**description、paramDocs、requiredFields** 为各宿主生成 schema 的 SSOT；宿主层禁止复制一份描述文案（架构测试 guard）。

## WorkBacklog 与 Kernel

`WorkBacklog` / `BacklogProjectionCore` 定义：

- 待办项形状、五份 `completedWorkReport` 字段约束（与 Shell codec 校验衔接）
- 从 committed 事件或参数构造**展示用**结构

真相仍在 NDJSON `work_backlog_committed`，见 [07-work-backlog.md](./07-work-backlog.md)。

## FallbackKernel

与 Shell `FallbackRuntime*` 配合：内核侧为**完美平方数启发式**与状态转移纯逻辑；实际消息改写与事件桥在 Shell。见 [12-fallback.md](./12-fallback.md)。

## 提示词与宝典

用户可见「Kolmolgorov 宝典 / 铁律」类长文本的 SSOT 在 **`CapsPrelude`**（及 `CapsFormat` 组装）。MessageTransform 只**引用** Shell 缓存组装结果，不在宿主目录复制 caps 正文（`ArchitectureTests.muxMessageTransformNoLocalCapsBuilder`）。

## 修改 Kernel 的检查清单

1. 是否引入 `Dyn` / `open Shell` / `DateTime.Now`？→ 禁止
2. 新状态是否用 DU + 穷举匹配？
3. 可预见业务失败是否 `Result` 分支而非异常？
4. 单文件是否逼近 300 行？→ 拆模块
5. 对应 `tests/*Tests.fs` 或架构探针是否更新？

## 源码入口（推荐阅读顺序）

1. `ReviewSession/StateMachine.fs` + `Types.fs`
2. `EventLog/Fold.fs` + `Types.fs`
3. `Nudge/`（与 `Nudge.fs` 顶层）
4. `BacklogProjectionCore.fs`
5. `ToolCatalog/Registry.fs` + `ToolPermission.fs`
6. `HostTools.fs`
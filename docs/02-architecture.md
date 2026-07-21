# 02 — 系统架构

## 三层模型

```text
┌──────────────────────────────────────────────────────────────────┐
│  Hosts (volatile adapters)                                         │
│  src/Hosts/OpenCode/  src/Hosts/Mux/  src/Hosts/Omp/               │
│  — hook 注册、schema 生成、宿主对象原地写字段纪律                   │
│  — SubsessionHostAdapter（ISubsessionHost 实现）                    │
└──────────────────────────────┬───────────────────────────────────┘
                               │ obj ↔ codec
┌──────────────────────────────▼───────────────────────────────────┐
│  Runtime (side effects)                                           │
│  FS / 网络 / 子进程 / MCP / EventStore / MessageTransform          │
│  Subsession / Fallback / Nudge / ReviewPrompts / Wanxiangzhen      │
└──────────────────────────────┬───────────────────────────────────┘
                               │ 强类型命令/事件
┌──────────────────────────────▼───────────────────────────────────┐
│  Kernel (pure rules)                                              │
│  ReviewSession / Nudge / EventSourcing / Subsession               │
│  FallbackKernel / ToolCatalog / ToolPermission / Methodology       │
└──────────────────────────────────────────────────────────────────┘
```

**判定法则**：去掉 Node 与宿主 `obj` 后仍成立的逻辑 → Kernel；否则 → Runtime 或 Host。

## 模块依赖纪律（架构测试强制执行）

- Kernel 规则可直接执行 → Kernel 单元测试
- 宿主边界稳定 → 宿主行为与 codec 契约测试
- Nudge loop 态必须事件 fold → 架构测试禁止直读 store
- Hook output 经 codec → Hook 输入输出契约测试
- Subsession 状态机纯函数 → Kernel/Subsession 单元测试

## 公开 JavaScript 入口

| 入口文件 | npm 路径 | 宿主 |
| :--- | :--- | :--- |
| `src/Hosts/Mux/Plugin.fs` | `wanxiangshu` → `.` | Mux（默认 main） |
| `src/Hosts/Omp/Plugin.fs` | `wanxiangshu/omp` | oh-my-pi |
| `src/Hosts/OpenCode/Plugin.fs` | 构建产物中的 OpenCode 插件 | OpenCode |
| `src/Hosts/OpenCode/PluginMimo.fs` | 无独立 npm export | Mimocode |
| `src/Hosts/OpenCode/PluginWanxiangzhen.fs` | `wanxiangshu/wanxiangzhen` | 万象阵 |

共享装配逻辑：OpenCode 系 → `PluginComposition.fs`；OMP → `src/Hosts/Omp/PluginComposition.fs`。

## Host 枚举与工具命名

`Kernel.HostTools.Host` = `Opencode | Mimocode | Mux | Omp`。

工具名可能不同：待办写入 OpenCode/Mux/OMP → `todowrite`；Mimocode → `task`。

`HostTools.normalizeToolNameForMux` 在权限分类前统一 canonical 名。

## 可变状态安放

- **允许**：`Runtime.Workspace.RuntimeScope` 派生实例（iterator store、scope 级队列等）
- **允许**：`Runtime.Subsession.SubsessionActorRegistry` 模块级 actor 注册表
- **允许**：`Runtime.Fallback.RuntimeStore` 每个 session 的可变状态
- **禁止**：Kernel 模块级可变；跨 session 裸全局（架构测试 `noDuplicateStateHolder`）
- **事件日志**：`Runtime.EventStore.EventLogStore` 进程内缓存 = fold 投影，**非**第二 SSOT；磁盘 NDJSON 为先

## 子系统概要

### Subsession Actor（子会话隔离）

子代理（Coder、Inspector、Browser、Meditator）通过 `SubsessionActor`（`src/Runtime/Subsession/SubsessionActor.fs`）运行，提供错误隔离、降级恢复和超时保护。由 `CommandProcessor`（10 步串行提交管线）+ `EffectSupervisor`（宿主 effect 分发）+ `ResourceScope`（RAII 定时器管理）三部件复合。子会话事件由 `SubsessionEventStore` 写入共享 NDJSON；`SubsessionActorRegistry` 管理 workspace × session 键注册表，支持安全投影（Safety Projection）重启后中毒恢复。

详见 [11-subagents.md](./11-subagents.md) § SubsessionActor。

### Fallback 运行时（模型降级）

Kernel 纯规则在 `src/Kernel/FallbackKernel/`（`StateMachine.fs`、`Decision.fs`、`Recovery.fs`、`Types.fs`）。Runtime 编排在 `src/Runtime/Fallback/`（`Coordinator.fs`、`FallbackCoordination.fs`、`RuntimeStore.fs`、`SessionRuntime.fs`、`Continuation*` 等 33 文件）。续命唯一物理路径：`IActionExecutor.SendContinue` → `SessionDispatcher` → 宿主 `session.prompt`，`recordHostAcceptedContinuation` 为唯一 Dispatched 写入入口。

详见 [12-fallback.md](./12-fallback.md)。

### Nudge 运行时

`src/Runtime/Nudge/NudgeFlow.fs` 与 `NudgeDispatchClaim.fs` 负责 nudge claim、去重和发送；`src/Kernel/Nudge/` 提供纯决策（`NudgeDerivation.fs`：`deriveAction` 从 `SessionSnapshot` 推导 action）。

详见 [06-review-and-nudge.md](./06-review-and-nudge.md) § Nudge。

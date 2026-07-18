# 02 — 系统架构
## 三层模型
```text
┌──────────────────────────────────────────────────────────────────┐
│  Host Adapters (volatile)                                         │
│  Opencode/  Mimocode/  Mux/  Omp/                                  │
│  — hook 注册、schema 生成、宿主对象原地写字段纪律                   │
│  — SubsessionHostAdapter（ISubsessionHost 实现）                    │
└──────────────────────────────┬───────────────────────────────────┘
                               │ obj ↔ codec
┌──────────────────────────────▼───────────────────────────────────┐
│  Shell (side effects)                                             │
│  FS / 网络 / 子进程 / MCP / EventLog / MessageTransform           │
│  ToolExecute / SubagentSpawn / RuntimeScope                       │
│  SubsessionActor / SubsessionService / SubsessionEventRouter       │
│  FallbackRuntimeState / FallbackEventBridge / NudgeRuntime         │
└──────────────────────────────┬───────────────────────────────────┘
                               │ 强类型命令/事件
┌──────────────────────────────▼───────────────────────────────────┐
│  Kernel (pure rules)                                              │
│  ReviewSession / Nudge / EventLog.Fold / WorkBacklog              │
│  Subsession/（Decision、Types、Policy、Fold、TranscriptDecision）  │
│  FallbackKernel/（StateMachine、Decision、Recovery）               │
│  ToolCatalog / ToolPermission / Methodology 元数据                │
└──────────────────────────────────────────────────────────────────┘
```

**判定法则**：去掉 Node 与宿主 `obj` 后仍成立的逻辑 → Kernel；否则 → Shell 或 Host。

## 模块依赖纪律（架构测试强制执行）

| 规则 | 探针示例 |
| :--- | :--- |
| Kernel 规则可直接执行 | Kernel 单元测试与编译器边界 |
| 宿主边界稳定 | 宿主行为与 codec 契约测试 |
| Nudge loop 态必须事件 fold | Nudge 事件溯源行为测试 |
| Hook output 经 codec | Hook 输入输出契约测试 |
| Subsession 状态机纯函数 | Kernel/Subsession 单元测试 |

完整列表见 [17-build-test-verify.md](./17-build-test-verify.md)。

## 公开 JavaScript 入口

| 入口文件 | npm 路径 | 宿主 |
| :--- | :--- | :--- |
| `src/Hosts/Mux/Plugin.fs` | `wanxiangshu` → `.` | Mux（默认 main） |
| `src/Hosts/Omp/Plugin.fs` | `wanxiangshu/omp` | oh-my-pi |
| `src/Hosts/OpenCode/Plugin.fs` | （包内构建产物） | OpenCode |
| `src/Hosts/OpenCode/PluginMimo.fs` | （包内构建产物） | Mimocode |
| `src/Hosts/OpenCode/PluginMimoTui.fs` | TUI 辅助 | Mimocode sidebar todo |
| `src/Hosts/OpenCode/PluginWanxiangzhen.fs` | `wanxiangshu/wanxiangzhen` | 万象阵 |

共享装配逻辑：**OpenCode 系** → `Hosts/OpenCode/PluginCore.fs`；**OMP** → `Hosts/Omp/PluginCore.fs`。

## Host 枚举与工具命名

`Kernel.HostTools.Host` = `Opencode | Mimocode | Mux | Omp`。

同一概念在不同宿主上的**工具名**可能不同，例如：

- 待办写入：OpenCode/Mux/OMP → `todowrite`；Mimocode → `task`
- 子代理任务工具：Mimocode 侧 `actor` 映射为 canonical `task`

`normalizeToolName` / `normalizeToolNameForMux` 在权限分类前统一 canonical 名。

## 可变状态安放

- **允许**：`Shell.RuntimeScope` 派生实例（iterator store、scope 级队列等）
- **允许**：`Shell.SubsessionActorRegistry` 模块级 actor 注册表
- **允许**：`Shell.FallbackRuntimeState` 每个 session 的可变状态
- **禁止**：Kernel 模块级可变；跨 session 裸全局（架构测试 `noDuplicateStateHolder`）
- **事件日志**：`EventLogStore` 进程内缓存 = fold 投影，**非**第二 SSOT；磁盘 NDJSON 为先

## 数据平面 vs 控制平面

| 平面 | 内容 |
| :--- | :--- |
| 控制平面 | 命令校验、FSM 转移、是否 append 事件 |
| 数据平面 | NDJSON 行、git（万象阵）、宿主 message 数组 |
| 展示平面 | YAML front-matter、caps prelude、Magic todo UI |

展示平面**不得**作为 review/todo/Subsession 的 SSOT（见 `05-event-sourcing`）。

## 子系统概要

### Subsession Actor（子会话隔离）

子代理（Coder、Inspector、Browser、Meditator）通过 `SubsessionActor` 轻量 Actor 消息泵运行，提供完整的错误隔离、降级恢复和超时保护。每个子会话拥有独立的 `SerialQueue`、Fallback 状态机、事件溯源和 NDJSON 持久化。

详见 [11-subagents.md](./11-subagents.md) § SubsessionActor。

### Fallback 运行时（模型降级）

`FallbackEventBridge` 编排模型降级全流程：宿主事件翻译 → FSM 转移 → 续命六阶段生命周期 → 门闩防并发。`FallbackRuntimeState` 维护每个 session 的降级状态、续命租约、门闩标志。

详见 [12-fallback.md](./12-fallback.md)。

### Nudge 运行时

`NudgeRuntime` 通过 `tryClaimNudgeDispatch` 在 `EventLogStore` 锁内执行原子 Claim，防止多路并发重复派发 nudge。`NudgeSnapshotState` 从事件流 fold 出决策所需快照。

详见 [06-review-and-nudge.md](./06-review-and-nudge.md) § Nudge。

## 架构演进（REF 架构愿景）

### 三位一体哲学

- **DDD（领域驱动设计）压缩空间复杂度**：划定限界上下文与一致性边界，避免全局状态污染。
- **Flow Kernel（自定义流代数）压缩时间复杂度**：以 `IAsyncEnumerable` 为最小协议，定义领域级流程算子，决定跨边界事物如何随时间协作与重试。
- **RAII（资源获取即初始化）压缩生命周期复杂度**：通过状态投影与 Scope 树，彻底消灭手动管理 Timer/Waiter 的偶然复杂度。

### 目标架构五部件

1. **纯函数内核** (`State × Command → Decision { NextState, Events, Effects }`)：绝对纯粹，无 IO、无网络、无时钟依赖。
2. **串行事务运行时**：承担唯一提交点，保证事务语义，包含持久化 Outbox 模式。
3. **RAII 资源作用域**：`CommittedState → ResourceSpec` 投影，根据状态 Diff 自动 Acquire/Dispose 层级 Scope。
4. **效应监督器 (Effect Supervisors)**：订阅 Outbox（非内存 CommittedDecision），执行宿主调用，处理超时/重试，将结果映射为领域 Command 回流至 Inbox。
5. **响应式边缘 (Reactive Edges)**：暴露两轨流 —— `IAsyncEnumerable<CommittedProgress>`（可重放、可用于业务决策）和 `IAsyncEnumerable<EphemeralTelemetry>`（best-effort、latest-wins）。

### DDD 限界上下文划分

- **Subsession Execution Context**：管理 Physical Session、Run、Turn 与 Poison 状态。Fallback 暂时作为此 Context 内的 Domain Policy。
- **Review Context**：管理 ReviewSession、Round、Verdict。
- **Squad Coordination Context**：Aggregate (Dag/Task) + Process Manager (Scheduler/Merge)。
- **Session Automation Processes**：多个独立的 Process Manager（Nudge、Continuation、Compaction）。

### 九条语义法律

1. **顺序法律**：Aggregate 内严格串行，Aggregate 间可并行。
2. **持久化法律**：区分 Proposed 与 Committed，未提交事件严禁被下游消费。
3. **取消法律**：Enumerator 的 Dispose 仅代表本地停止等待，绝不自动撤销已启动的 JS Promise 或远端 Prompt。
4. **背压法律**：Command 与 Domain Event 不可丢弃；Progress 与 Evidence 可 Latest-wins。
5. **单次枚举法律**：有副作用的 Process 流必须明确禁止二次枚举或重新执行。
6. **无重入法律**：Effect 完成后只能 enqueue Command → Inbox，绝不能同步或递归调用 processor.Handle Command。
7. **重放确定性法律**：耐久 Process Manager 的状态必须完全来自自己的持久事件和已提交的 Integration Events。
8. **关闭法律**：每个运行时组件必须明确区分 StopAccepting → Drain → AbortLocalWaiters → DisposeResources → Closed。
9. **幂等法律**：所有可能重放或重试的输入必须定义重复处理规则：CommandId 去重、EventId 去重、EffectId 幂等。

### 9 阶段演进路线图

| Phase | 内容 | 目标 |
| :--- | :--- | :--- |
| 0 | 协议轨迹、故障注入、Replay 基线 | 建立不可回归特征测试 |
| 1 | 战略 DDD 与 Context Map | 统一通用语言，划定边界 |
| 2 | RAII Scope/Lease 与 ResourcePlan | 消灭手动 Timer/Waiter 管理 |
| 3 | 最小 Flow Kernel 与九条语义法律 | 建立基于 IAsyncEnumerable 的流程代数 |
| 4 | Subsession Actor 三部件拆分 | CommandProcessor + EffectSupervisor + ResourceScope |
| 5 | Reactive Edges 与遥测分层 | 暴露 CommittedProgress / EphemeralTelemetry 两轨 |
| 6 | 拆分上下文 Projection | 消除 SessionState 上帝状态依赖 |
| 7 | 抽取跨上下文 Process Manager | Nudge、Continuation、Review Loop 独立化 |
| 8 | 清理全局 Fold 与旧 RuntimeScope/Actor 基础设施 | 彻底消除上帝状态 |

迁移策略：分阶段、每步 `npm run build-and-test` 全绿；禁止大爆炸重写。当前真相以**四套宿主目录 + 行为/契约测试**为准。不同时重写领域状态机和运行时机制。

## 相关文档

- Kernel 模块族：[03-kernel.md](./03-kernel.md)
- Shell 边界：[04-runtime.md](./04-runtime.md)
- SSOT 总表：[18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)
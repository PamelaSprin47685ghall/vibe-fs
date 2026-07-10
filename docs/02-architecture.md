# 02 — 系统架构

## 三层模型

```text
┌─────────────────────────────────────────────────────────┐
│  Host Adapters (volatile)                                │
│  Opencode/  Mimocode/  Mux/  Omp/                         │
│  — hook 注册、schema 生成、宿主对象原地写字段纪律          │
└───────────────────────────┬─────────────────────────────┘
                            │ obj ↔ codec
┌───────────────────────────▼─────────────────────────────┐
│  Shell (side effects)                                      │
│  FS / 网络 / 子进程 / MCP / EventLog / MessageTransform   │
│  ToolExecute / SubagentSpawn / RuntimeScope               │
└───────────────────────────┬─────────────────────────────┘
                            │ 强类型命令/事件
┌───────────────────────────▼─────────────────────────────┐
│  Kernel (pure rules)                                       │
│  ReviewSession / Nudge / EventLog.Fold / WorkBacklog     │
│  ToolCatalog / ToolPermission / Methodology 元数据       │
└─────────────────────────────────────────────────────────┘
```

**判定法则**：去掉 Node 与宿主 `obj` 后仍成立的逻辑 → Kernel；否则 → Shell 或 Host。

## 模块依赖纪律（架构测试强制执行）

| 规则 | 探针示例 |
| :--- | :--- |
| Kernel 禁止 `Dyn.*`、禁止直接 Shell | `ArchitectureTests.kernelBoundary` |
| Kernel 单文件有效行 ≤300（黄牌 200） | `ArchitectureTests.fileBodyUnder300` |
| Opencode ↔ Mux 禁止互引 | `opencodeNoMuxRef` / `muxNoOpencodeRef` |
| Omp 禁止 Opencode/Mux/engine | `ArchitectureTests.ompBoundary` |
| Nudge loop 态必须事件 fold | `nudgeLoopStateMustReplayHistory` |
| Hook output 经 codec，禁裸 `Dyn.set` | `ArchitectureTestsWireHook` 系列 |

完整列表见 [17-build-test-verify.md](./17-build-test-verify.md)。

## 公开 JavaScript 入口

| 入口文件 | npm 路径 | 宿主 |
| :--- | :--- | :--- |
| `src/Mux/Plugin.fs` | `wanxiangshu` → `.` | Mux（默认 main） |
| `src/Omp/Plugin.fs` | `wanxiangshu/omp` | oh-my-pi |
| `src/Opencode/Plugin.fs` | （包内构建产物） | OpenCode |
| `src/Opencode/PluginMimo.fs` | （包内构建产物） | Mimocode |
| `src/Opencode/PluginMimoTui.fs` | TUI 辅助 | Mimocode sidebar todo |
| `src/Opencode/PluginWanxiangzhen.fs` | `wanxiangshu/wanxiangzhen` | 万象阵 |

共享装配逻辑：**OpenCode 系** → `Opencode/PluginCore.fs`；**OMP** → `Omp/PluginCore.fs`。

## Host 枚举与工具命名

`Kernel.HostTools.Host` = `Opencode | Mimocode | Mux | Omp`。

同一概念在不同宿主上的**工具名**可能不同，例如：

- 待办写入：OpenCode/Mux/OMP → `todowrite`；Mimocode → `task`
- 子代理任务工具：Mimocode 侧 `actor` 映射为 canonical `task`

`normalizeToolName` / `normalizeToolNameForMux` 在权限分类前统一 canonical 名。

## 可变状态安放

- **允许**：`Shell.RuntimeScope` 派生实例（iterator store、scope 级队列等）
- **禁止**：Kernel 模块级可变；跨 session 裸全局（架构测试 `noDuplicateStateHolder`）
- **事件日志**：`EventLogStore` 进程内缓存 = fold 投影，**非**第二 SSOT；磁盘 NDJSON 为先

## 数据平面 vs 控制平面

| 平面 | 内容 |
| :--- | :--- |
| 控制平面 | 命令校验、FSM 转移、是否 append 事件 |
| 数据平面 | NDJSON 行、git（万象阵）、宿主 message 数组 |
| 展示平面 | YAML front-matter、caps prelude、Magic todo UI |

展示平面**不得**作为 review/todo 的 SSOT（见 `05-event-sourcing`）。

## 演进路线（类型安全与去重）

已识别问题与分阶段目标（非全部已落地）：

| 问题 | 目标 |
| :--- | :--- |
| 宿主 `obj` 渗入内核 | Shell DTO（`HostMessageDto` 等）+ 边界 decode |
| 内存与盘双写风险 | 突变仅经 EventLog append，投影只读 fold |
| 三宿主重复 spawn/fuzzy | `IHostAdapter` / `SubagentDispatcher` 统一 |
| 魔法字符串错误 | `DomainError` DU |
| 巨型 SessionLifecycleObserver | 拆为 Progress / Fallback / Nudge 观察片 |

迁移策略：分阶段、每步 `npm run build-and-test` 全绿；禁止大爆炸重写。当前真相仍以 **四套宿主目录 + ArchitectureTests*** 为准。

## 相关文档

- Kernel 模块族：[03-kernel.md](./03-kernel.md)
- Shell 边界：[04-shell.md](./04-shell.md)
- SSOT 总表：[18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)
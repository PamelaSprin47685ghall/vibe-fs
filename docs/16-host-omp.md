# 16 — oh-my-pi (OMP) 宿主

## 入口

- `src/Hosts/Omp/Plugin.fs` → npm **`wanxiangshu/omp`**
- `src/Hosts/Omp/PluginComposition.fs`：扩展装配
- 导出符号：`wanxiangshuExtension`（见 `README.md`）

## 隔离纪律

- **仅**依赖 `Kernel` + `Shell`
- 禁止 `open` `Wanxiangshu.Hosts.Opencode`、`Wanxiangshu.Mux`、`engine/`（架构测试 `ompBoundary`、`ompNoEngineRef`）

## 工具

- 分模块：`src/Hosts/Omp/FuzzyTools.fs`、`ExecutorTools.fs`、`WebTools.fs`、`ReviewToolsRegister.fs`、`TodoTool.fs`、`SubagentTools.fs` 等
- **Executor**：同步语义；**不**注册 `executor_wait` / `executor_abort`（与 OpenCode 异步执行器模型对齐方式不同）
- Schema：**TypeBox** via `OmpToolSchema.fs`（`executor` 含 `max_bytes`）
- `pi?registerTool` 动态注册

## 子 workspace

- `src/Hosts/Omp/ChildSession.fs`、`ChildSessionRegistry.fs`：子代理隔离目录

## Caps 与消息

- `src/Hosts/Omp/CapsCodec.fs`、`src/Hosts/Omp/MessageTransform.fs`：经 Runtime caps + `MessagingCodec`

## Review / Nudge / Magic

- `src/Hosts/Omp/NudgeHooks.fs`、`src/Hosts/Omp/NudgeRuntime.fs`
- `src/Hosts/Omp/MagicTodo.fs`
- Review：`ReviewToolsRegister` + `ReviewRuntime`

## Fallback

- `src/Hosts/Omp/Fallback/`：OMP Fallback adapter、事件翻译与执行

## 上游

**禁止**修改 `../oh-my-pi`；可参考其行为在本仓库实现。

## 相关文档

- [02-architecture.md](./02-architecture.md)
- [09-methodology.md](./09-methodology.md)
- [06-review-and-nudge.md](./06-review-and-nudge.md)
- [12-fallback.md](./12-fallback.md)

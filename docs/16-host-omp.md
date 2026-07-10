# 16 — oh-my-pi (OMP) 宿主

## 入口

- `Omp/Plugin.fs` → npm **`wanxiangshu/omp`**
- `Omp/PluginCore.fs`：扩展装配
- 导出符号：`wanxiangshuExtension`（见 `README.md`）

## 隔离纪律

- **仅**依赖 `Kernel` + `Shell`
- 禁止 `open` `Wanxiangshu.Opencode`、`Wanxiangshu.Mux`、`engine/`（架构测试 `ompBoundary`、`ompNoEngineRef`）

## 工具

- 分模块：`FuzzyTools`、`ExecutorTools`、`WebTools`、`ReviewToolsRegister`、`TodoTool`、`SubagentTools` 等
- Schema：**TypeBox** via `OmpToolSchema.fs`
- `pi?registerTool` 动态注册

## 子 workspace

- `Omp/ChildSession.fs`、`ChildSessionRegistry.fs`：子代理隔离目录

## Caps 与消息

- `Omp/CapsCodec.fs`、`Shell/OmpCaps`
- `Omp/MessageTransform.fs`：经 Shell caps + `MessagingCodec`

## Review / Nudge / Magic

- `Omp/NudgeHooks.fs`、`Shell/NudgeRuntime`
- `Omp/MagicTodo.fs`
- Review：`ReviewToolsRegister` + `ReviewRuntime`

## 上游

**禁止**修改 `../oh-my-pi`；可参考其行为在本仓库实现。

## 相关文档

- [02-architecture.md](./02-architecture.md)
- [09-methodology.md](./09-methodology.md)
- [06-review-and-nudge.md](./06-review-and-nudge.md)
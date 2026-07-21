# 16 — oh-my-pi (OMP) 宿主

## 入口

- `src/Hosts/Omp/Plugin.fs` → npm **`wanxiangshu/omp`**
- `src/Hosts/Omp/PluginComposition.fs`：扩展装配

## 隔离纪律

- **仅**依赖 `Kernel` + `Shell`
- 禁止 `open` `Wanxiangshu.Hosts.Opencode`、`Wanxiangshu.Mux`、`engine/`（架构测试）

## 工具

- 分模块：`FuzzyTools.fs`、`ExecutorTools.fs`、`WebTools.fs`、`ReviewToolsRegister.fs`、`TodoTool.fs`、`SubagentTools.fs`
- **Executor**：同步语义；**不**注册 `executor_wait` / `executor_abort`
- Schema：**TypeBox**（`OmpToolSchema.fs`，`executor` 含 `max_bytes`）
- `pi?registerTool` 动态注册

## Caps 与消息

- `src/Hosts/Omp/CapsCodec.fs`、`src/Hosts/Omp/MessageTransform.fs`：经 Runtime caps + `MessagingCodec`

## Review / Nudge / Magic

- `src/Hosts/Omp/NudgeHooks.fs`、`NudgeRuntime.fs`
- `src/Hosts/Omp/MagicTodo.fs`
- Review：`ReviewToolsRegister` + `ReviewRuntime`

## Fallback

`src/Hosts/Omp/Fallback/Hook.fs`、`EventTranslator.fs`、`ActionExecutor.fs`。

## 上游

**禁止**修改 `../oh-my-pi`；可参考。

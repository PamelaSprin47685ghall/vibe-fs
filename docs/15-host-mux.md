# 15 — Mux 宿主

## 入口

- `src/Hosts/Mux/Plugin.fs`：npm 包默认 **`wanxiangshu`** main → `build/src/Hosts/Mux/Plugin.js`
- `src/Hosts/Mux/PluginRegistration.fs`、`PluginCatalog.fs`：注册表与 catalog

## 工具与 Wrapper

- `src/Hosts/Mux/BuiltinTools.fs`、`BuiltinToolsFuzzy.fs`：内建工具 + fuzzy
- `src/Hosts/Mux/Wrappers.fs`：Mux 原生工具名 → 万象术执行链
- `src/Hosts/Mux/SubagentTools.fs`：子代理与 delegate

## 消息

`src/Hosts/Mux/MessageTransform.fs`：须用 Runtime caps cache + projection policy（架构测试）。

## Slash 与配置

- Slash command 与 workspace codec：`MuxWorkspaceCodec`、`MuxHookInputCodec`
- AI 设置：`MuxAiSettingsCodec`、`DelegatedAiSettings`

## Fallback

`src/Hosts/Mux/Fallback/Hook.fs`、`Executor.fs`。

## 与 ../mux 仓库

允许改 **binding**；mux 核心最小改动（`AGENTS.md`）。

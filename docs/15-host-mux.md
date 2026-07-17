# 15 — Mux 宿主

## 入口

- `Hosts/Mux/Plugin.fs`：npm 包默认 **`wanxiangshu`** main → `build/src/Hosts/Mux/Plugin.js`
- `Mux/PluginRegistration.fs`、`PluginCatalog.fs`：注册表与 catalog

## 工具与 Wrapper

- `Mux/HostTools.fs`、`HostToolsFuzzy.fs`：内建工具 + fuzzy
- `Mux/Wrappers.fs`：Mux 原生工具名 → 万象术执行链
- `Mux/SubagentTools.fs`：子代理与 delegate

## 消息

- `Mux/MessageTransform.fs`：须用 Shell caps cache + projection policy（架构测试）

## Slash 与配置

- Slash command 与 workspace codec：`MuxWorkspaceCodec`、`MuxHookInputCodec`
- AI 设置：`MuxAiSettingsCodec`、`DelegatedAiSettings`

## 与 ../mux 仓库

允许改 **binding**；mux 核心最小改动（`AGENTS.md`）。真正实现优先本仓库。

## Fallback

- `Mux/FallbackHooks.fs` → `createMuxFallbackHandler`
- `Mux/Fallback/EventTranslator.fs`：`muxEventTranslator`
- `Mux/Fallback/ActionExecutor.fs`：`IActionExecutor` 实现

## 相关文档

- [02-architecture.md](./02-architecture.md)
- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
- [11-subagents.md](./11-subagents.md)
- [12-fallback.md](./12-fallback.md)

# 待完成的重构项

# 一、仍未收口的系统级问题

### 1. `NudgeHook.fs:dispatchEventState` 仍分散使用 Dyn.* 读 props

* `dispatchEventState` 每个事件分支各自用 `Dyn.get`/`Dyn.str` 从 `props` 提取字段（`part`、`error`、`status`、`finish` 等），再传入 `OpencodeNudgeState` 的 handler。
* ~15 处 Dyn.* 调用，可收敛到统一的 event props decoder。
* 注意：Opencode host 的 event props 形状随事件类型变化，decoder 需按 `eventType` 分派。

### 2. `Registration.fs` 的 `deps` 仍为裸 `obj`

* `createRegistration (_deps: obj)` 将 `deps` 作为不透明依赖对象透传给工具创建，未解码为 record。
* 实质上 `deps` 是纯透传——其内部形状由 JS host 定义，F# 侧不读字段，因此优先级低。

---

# 二、已完成项（不再需要处理）

### NudgePolicy.fs 死代码（已删除 143 行）
* `NudgeShellState` / `decideNudge` / `recordSend` / `dispatchEvent` 等与 `Kernel/OpencodeNudgeState.fs` 重复的状态机逻辑已删除。
* 文件从 195 行缩减为 52 行，仅保留 4 个仍被引用的工具函数。

### Snapshot 解码提取（SessionSnapshotDecoder.fs）
* `collectSnapshot` 中的 `Dyn.*` 访问已提取到 `SessionSnapshotDecoder.fs`，含 `decodeTodos` 和 `decodeLastAssistant` 两个纯函数。

### TreeSitterKernel 字段别名收敛（MessageDecoder.firstPresent）
* `extractFilePaths` 中的 `path` / `file_path` / `filePath` 别名查找已收敛到 `MessageDecoder.firstPresent`。

### JS 入口收敛为强类型记录
* `MuxPlugin/EventDecoder.fs` — `DecodedHookEvent` record + `decodeHookEvent`。`MuxEventHook.fs` 的 `parseHookEvent` 和 `handleEvent` StreamEnd 分支已改用 decoder。
* `ResolveAiSettings.fs` — `AiConfigRecord` record + `decodeAiConfig`。`resolveDelegatedAgentAiSettings` 已改用 decoded record。

### 不可消除 / 合理保留的 Dyn.* 热点
* `JsBoundary.fs:translateJsError` — JS Error 对象链遍历，边界层合理保留。
* `Mux/Dedup.fs` ~15 处 `Dyn.withKey` — 构造不可变更新的唯一路径，不可消除。

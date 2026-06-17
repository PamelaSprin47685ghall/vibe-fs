# MAGIC 移植方案：把 todo 列表与 append-only 工作日志折叠能力内置到 vibe-fs

本方案把 MAGIC（GSD/pi 生态的 JS opencode 扩展，提供 `manage_todo_list` 工具 + append-only backlog + 上下文折叠）移植为 vibe-fs 的内置必选功能。本方案与同目录 `future/PLAN.md`（backtrack 可见历史重写）共享 `experimental.chat.messages.transform` 钩子，第五章专门分析两者相互影响。版本 0.1.0。

---

### 一、现状调研

#### 1. MAGIC 原有能力
1. `manage_todo_list` 工具（`operation` 为 `write` 或 `read`，`write` 必须带 `completedWorkReport`）。
2. per-session todo state + append-only backlog（只增不减的工作报告）。
3. hooks：`session_start` / `session_switch` / `session_fork` / `session_tree`（通过 `appendEntry` custom entry `magic-todo-backlog-entry` 与 `restoreFromBranch` 恢复状态）、`context`（消息投影折叠）、`session_before_compact`（compaction 预处理）。
4. 核心折叠逻辑：`findFoldRange` 定位第 1 个与倒数第 2 个 `manage_todo_list` 结果，折叠其间内容；`projectRange` 把第 1 个结果位置换成 backlog 投影，prefix user 消息折叠进合成 user 消息。
5. MAGIC 消息格式（GSD/pi）：`{ role, content: [{type:'text'|'toolCall'}], toolCallId, toolName, isError, details }`。

#### 2. vibe-fs 现状
1. 两套宿主适配层：Opencode 层（`src/Opencode/Plugin.fs`，注册 `chat.message` / `tool.definition` / `tool.execute.before` / `tool.execute.after` / `experimental.chat.messages.transform` / `command.execute.before` / `event` / `experimental.session.compacting`）；MuxPlugin 层（`src/MuxPlugin/Registration.fs`，导出 `createRegistration` 给 mux 宿主）。
2. vibe-fs 消息格式（opencode）：`Entry { type:'message'|'custom', message?, customType?, data? }`；`Message { info:{ id, role, agent, sessionID, toolName, toolCallId, isError, details, content }, parts:[{ type:'text'|'tool', text?, state:{input,output} }] }`。
3. 现有 todo 机制：依赖 opencode 内置 `todowrite` 工具（带 `phases`）；`SessionText.fs` 的 `getLatestTodoPhasesFromEntries` 从 `user_todo_edit` custom event 或 `todowrite` 成功结果读 `details.phases`；nudge 系统（`NudgeHook.fs`）通过 `client.session.todo` 读 todos 判断是否提醒 agent。

#### 3. opencode 宿主能力（已查证官方文档）
1. Hooks 全集：`chat.message`, `tool.definition`, `tool.execute.before`, `tool.execute.after`, `experimental.chat.messages.transform`, `command.execute.before`, `event`, `experimental.session.compacting`。
2. `experimental.session.compacting`：注入 compaction context 或替换 compaction prompt（语义是注入，非替换 `messagesToSummarize`）。
3. Events：`session.created`, `session.idle`, `session.compacted`, `session.deleted`, `todo.updated`, `message.updated` 等。
4. 没有 `session_start` / `session_switch` / `session_fork` / `session_tree` hooks；没有 pi 的 `appendEntry` custom entry API。

---

### 二、核心决策

1. `manage_todo_list` 替代 opencode 内置 `todowrite` 作为唯一 todo 真实来源；backlog 是 MAGIC 独有价值的 append-only 工作报告，opencode 无对应物。在 `ToolPolicy` / `resolveChatTools` / `getToolPolicy` 中禁用 `todowrite`，允许 `manage_todo_list`。
2. 折叠逻辑在 Kernel 层用 opencode `{info,parts}` 格式重写，不保留 GSD/pi `{role,content}` 格式。
3. 状态权威来源 = 原始历史重放：每次成功的 `manage_todo_list` write 结果携带 `details.todos` / `details.appendedReport`，从完整 entry 流扫描重放出当前 `MagicState`。运行时缓存只是可重建的 derivative（性能优化），不是真相源。
4. compaction 降级：注册 `experimental.session.compacting` hook 注入提示「保留最新 `manage_todo_list` 结果与全部 backlog」，因为 backtrack Projector 已在每次 transform 折叠可见历史。
5. 遵循内核-外壳分离：纯逻辑落 Kernel，宿主适配落 Opencode / MuxPlugin。

---

### 三、详细设计

#### 1. 消息归一化与折叠

给出纯类型定义：

```
type MagicPart =
    | Text of string
    | ToolCall of toolName:string * toolCallId:string
    | Other

type MagicMessage = {
    id: string
    role: string
    toolName: string option
    toolCallId: string option
    isError: bool
    isSynthetic: bool
    parts: MagicPart list
    details: obj option
}
```

说明：Kernel 的 `findFoldRange` / `projectRange` / `buildBacklogText` 全部基于 `MagicMessage[]` 纯运算；Opencode 层负责 `obj -> MagicMessage -> obj` 转换。

在 opencode 格式下定位 `manage_todo_list`：工具结果 = `info.role="toolResult" && info.toolName="manage_todo_list" && not info.isError`；失败结果 `isError=true` 不计入 fold range，只含失败调用的 assistant message 应隐藏。

合成消息：backlog 投影消息 `role="toolResult"`, `toolName="manage_todo_list"`, `toolCallId=原调用id`, `info.id="magic-todo-projection-{callId}"`, `details.magicTodoProjection=true`；prefix 合成 user 消息 `info.id="magic-todo-prefix-{guid}"`, `magicTodoPrefixProjection=true`。两者都是合成消息。

#### 2. 状态重放与缓存

`MagicReplay.fs` 纯函数，输入完整 entry 流：遇到 `manage_todo_list` 成功结果 -> `todos = details.todos`，同时把 `details.appendedReport` 追加到 backlog；输出当前 `MagicState { todos: Todo list; backlog: BacklogEntry list }`。

运行时缓存 `MagicSessionCache = Map<string, MagicState>`（key=`sessionID`）：提供 `rebuild(sessionID, client)` 从 `client.session.messages` 重放；`session.created` / `session.compacted` 事件后 rebuild；`manage_todo_list` write 成功后增量更新。缓存可被丢弃随时重建。

#### 3. manage_todo_list 工具

用 `Sdk.define` 注册，参数：`operation`（`write`/`read`）、`todoList`（`id`/`title`/`description`/`status`）、`completedWorkReport`。`write` 校验 `todoList` 与 `report`，写入缓存（增量），返回 `details` 含 `todos` / `backlog` / `appendedReport`。`read` 返回当前 todos + backlog 文本。工具对所有 agent 可用。禁用 opencode 内置 `todowrite`。

#### 4. transform 投影集成

在 `Hooks.messagesTransform` 中插入 MAGIC 投影阶段（具体顺序见第五章）。投影逻辑：对仍可见的旧 `manage_todo_list` 结果做折叠，第 1 个结果位置换成 backlog 投影，prefix user 消息折叠进合成 user 消息。

#### 5. compaction hook

注册 `experimental.session.compacting`：注入提示保留最新 `manage_todo_list` 结果与 backlog，不替换 `messagesToSummarize`。

---

### 四、模块划分与文件清单

#### Kernel（纯函数）新增
1. `src/Kernel/MagicTypes.fs`：`Todo`、`BacklogEntry`、`MagicMessage`、`MagicPart`、合成消息 ID 前缀常量。
2. `src/Kernel/MagicReplay.fs`：从 entry/message 流重放 `MagicState`（纯函数）。
3. `src/Kernel/MagicProjector.fs`：`findFoldRange`、`projectRange`、`buildBacklogText` 折叠逻辑（纯函数）。
4. `src/Kernel/MagicPrompts.fs`：工具描述、backlog 文本模板。

#### Opencode（宿主适配）新增/修改
1. `src/Opencode/MagicMessage.fs`：`obj -> MagicMessage` 归一化、合成消息构造、清理函数。
2. `src/Opencode/MagicSession.fs`：per-session 缓存 `Map<string,MagicState>`、`rebuild`、增量更新。
3. `src/Opencode/MagicTools.fs`：用 `Sdk.define` 注册 `manage_todo_list` 工具。
4. `src/Opencode/MagicHooks.fs`：transform 集成、compaction hook、事件刷新缓存。
5. 修改 `src/Opencode/NudgeHook.fs`：`collectSnapshot` 改为从 `MagicSession` 取未完成 todos（替代 `client.session.todo`）。
6. 修改 `src/Opencode/Hooks.fs`：`messagesTransform` 增加 MAGIC 投影阶段与合成消息清理。
7. 修改 `src/Opencode/Plugin.fs`：注册 MAGIC 工具与 hooks。

#### MuxPlugin（mux 宿主适配）新增
1. `src/MuxPlugin/MagicMux.fs`：Mux 消息格式适配、`manage_todo_list` 工具/wrapper 注册、Mux 消息 transform 集成。

#### fsproj 插入位置
说明插入点（不写完整 xml，用文字说明）：`MagicTypes` / `MagicReplay` / `MagicProjector` / `MagicPrompts` 插在 Kernel 的 `UnifiedContext.fs` 之后、`OpencodeHooks.fs` 之前；`MagicMessage` / `MagicSession` / `MagicTools` 插在 `Opencode/Session.fs` 之后、`NudgeHook.fs` 之前；`MagicHooks` 插在 `Hooks.fs` 之后、`Plugin.fs` 之前；`MagicMux` 插在 `MuxTools.fs` 之后、`MuxWrappers.fs` 之前。

---

### 五、与 future/PLAN.md（backtrack 方案）的相互影响

两个方案共享 `experimental.chat.messages.transform` 钩子，都做消息折叠，必须设计统一流水线。本章是本方案重点。

#### 1. 流水线合并顺序

统一 transform 顺序：

1. 清理旧合成消息（`rewrite-prelude-*` / `caps-synth-*` / `magic-todo-projection-*` / `magic-todo-prefix-*`）。
2. Read 去重（剥 `#id_:` 前缀后比较）。
3. Backtrack Projector（根据 backtrack 事件重写可见历史）。
4. MAGIC 投影（对仍可见的旧 `manage_todo_list` 结果做折叠）。
5. 注入 Backtrack Prelude。
6. 注入 CAPS。

说明 MAGIC 必须在 backtrack 之后：backtrack 语义更强（agent 显式重写历史），MAGIC 语义更弱（自动压缩），强语义先弱语义后。若 MAGIC 先跑，其合成投影消息会被 backtrack Projector 当成普通 non-user 内容误删；若 backtrack 先跑，已裁剪的旧 todo 结果不再进入 MAGIC 视野，MAGIC 只处理仍可见的剩余结果。

#### 2. Projector 语义冲突

backtrack Projector 丢弃锚点后非 user 可见内容。MAGIC 跑在 backtrack 之后天然避免：被 backtrack 删除的 todo 结果无需再折叠。MAGIC 生成的合成消息（`magic-todo-*`）必须被 backtrack 的 id 扫描函数过滤（与 `caps-synth-*` 同等），不被当成合法锚点。

#### 3. 合成消息清理清单统一

集中定义所有合成消息前缀：`rewrite-prelude-user-`、`rewrite-prelude-assistant-`、`caps-synth-`、`magic-todo-projection-`、`magic-todo-prefix-`。transform 第一步 filter 掉 `info.id` 匹配这些前缀的消息，保证上轮投影不污染下轮 id 扫描、不重复累积。

#### 4. id 分配与锚点

backtrack Seq 给每个真实工具结果分配单调递增 id（含 `manage_todo_list` 结果）。MAGIC 合成投影消息不是真实工具结果，不分配 id，只用合成前缀 id；投影消息保留原 `toolCallId` 但自身不可被 `backtrack.anchor` 引用。backtrack Codec 的 id 扫描必须跳过 `isSynthetic=true` 的消息。

#### 5. 内存状态哲学调和

backtrack 方案强调「唯一内存状态是 Seq」。MAGIC 需 per-session todo/backlog 状态，看似冲突，可调和：MAGIC 权威状态 = 原始历史重放（与 backtrack 不维护运行时可见历史栈理念一致）；per-session MAGIC 缓存只是可重建 derivative，可随时丢弃重建，不是真相源。缓存存在但不破坏 backtrack 无状态/重放架构。

#### 6. compaction 交互

backtrack 方案不在 compaction 做事（每次 transform 从原始流重放）。MAGIC 的 `session_before_compact` 价值在 backtrack 架构下大幅降低。两者在 compaction 上不冲突：MAGIC 只注入提示，backtrack 不参与 compaction。

#### 7. 并行实施建议

建议先实现 MAGIC transform 阶段，在没有 backtrack Projector 时以 identity（直通）占位 backtrack 阶段；等 backtrack 落地后合并流水线。两者共用合成消息清理清单与统一 Projector 流水线接口。

---

### 六、实施阶段

1. **阶段一（Kernel 纯逻辑）**：`MagicTypes` / `MagicReplay` / `MagicProjector` / `MagicPrompts` + 单元测试（移植 MAGIC 现有 `test/context.test.js` 逻辑到 F#，基于 `MagicMessage` 格式）。
2. **阶段二（Opencode 工具与状态）**：`MagicMessage` / `MagicSession` / `MagicTools` + `manage_todo_list` 工具上线 + 禁用 `todowrite` + `NudgeHook` 改造。
3. **阶段三（transform 集成）**：`MagicHooks` 接入 `messagesTransform`，实现投影折叠，identity 占位 backtrack。
4. **阶段四（compaction）**：注册 `experimental.session.compacting` 提示注入。
5. **阶段五（Mux 适配）**：`MagicMux` 复用 Kernel，接入 mux 宿主。
6. **阶段六（backtrack 合并）**：与 `future/PLAN.md` 落地后合并流水线，统一合成消息清理与 Projector 顺序。

---

### 七、验证要点

移植 MAGIC 现有测试语义到 F# 并扩展：

1. fold range 定位：第 1 个与倒数第 2 个 `manage_todo_list` 结果，基于 opencode `{info,parts}` 格式。
2. 投影：第 1 个结果换成 backlog 投影，prefix user 折叠进合成 user，失败调用隐藏，非 todo 工具调用保留。
3. 状态重放：从完整 entry 流重建 todos 与 backlog，幂等。
4. `manage_todo_list` 工具：`write` 校验 `completedWorkReport` 与 `todoList`；`read` 返回当前状态；`write` 后 `details` 携带 `todos` / `backlog` / `appendedReport`。
5. 合成消息清理：transform 起始清理上轮 `magic-todo-projection-*` / `magic-todo-prefix-*`。
6. backtrack 互操作：MAGIC 跑在 backtrack 之后；合成消息不参与 id 扫描；被 backtrack 删除的 todo 结果不再折叠。
7. `todowrite` 禁用：`ToolPolicy` 对所有 agent 拒绝 `todowrite`；nudge 从 MAGIC 缓存取 todos。
8. compaction：`experimental.session.compacting` 注入保留提示。
9. 宿主引用约束：`output.messages` 数组引用稳定，未变更 message 尽量保留原引用。

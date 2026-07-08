# 钩子 O(1) 审计清单

本文件记录了万象术所有核心钩子（Hooks）、消息变换（MessageTransform）、事件处理器（Event）的复杂度审计结果与优化决策。

审计目标：确保 per-turn / per-message 热路径上的所有操作均为 O(1) 或 O(log N)，
杜绝 O(N²) 列表拼接、无界线性扫描等随会话增长而退化的反模式。

---

## 1. 宿主消息变换钩子 (MessageTransform)

### Mux 消息变换 (`src/Mux/MessageTransform.fs`)

- **审计路径**：`transform` → `deduplicateReadOutputs` → `deduplicateReadOutputsWithSeenByPath`
- **复杂度**：
  - 旧实现：`List.groupBy` + 每条消息内 `List.tryFind`（对 part 线性扫描），整体为 O(M × R)，M = 消息数，R = 去重匹配数。
  - 优化后：使用 `List.fold` 一次性构建嵌套索引 `Map<msgIndex, Map<partIndex, ReadHit>>`，查找时双层 `Map.tryFind`，整体 O(R log R + M log R)。
- **状态**：**已修复**。

### Opencode 消息变换 (`src/Opencode/MessageTransform.fs`)

- **审计路径**：`transform` → `deduplicateReadOutputs`
- **复杂度**：通过 `processDedupHits` 走统一去重，利用 `foldDedup` 对 `DedupState` 进行去重。
- **状态**：**已修复**（继承内核去重优化）。

### OMP 消息变换 (`src/Omp/MessageTransform.fs`)

- **审计路径**：`transform` → `deduplicateReadOutputs`
- **复杂度**：与 Opencode 共享 `processDedupHits` 去重机制，直接受益于内核 Set 查找优化。
- **状态**：**已修复**。

---

## 2. 内核与 Shell 去重核心 (Deduplication)

### 文件去重核 (`src/Kernel/Dedup.fs`)

- **审计路径**：`deduplicate`
- **复杂度**：
  - 旧实现：对所有已读内容进行 O(N) 线性 `List.exists` 查找（含 fingerprint 匹配与子串匹配）。
  - 优化后：将 `DedupState` 拆为 `fingerprints: Set<string>` 和 `rawOutputs: string list`。指纹路径走 `Set.contains`，为 O(log N)（JS 运行时平均 O(1)）；无指纹时走 rawOutputs 的 `List.exists` 线性扫描，且 `rawOutputs` 被限制在 `maxRawOutputs = 100` 以内，最坏退化为常数 O(100)。
  - 队列溢出优化：原双重 `List.rev` 在满容时导致多余分配，已改写为单遍尾递归 `dropLast`，单次反转 O(N)。
- **状态**：**已修复**。

---

## 3. 事件日志与状态机 (EventLog & StateMachine)

### 事件追加 (`src/Shell/EventLogFiles.fs`)

- **审计路径**：`AppendEvent` / `AppendEventOrFail` / `TryClaimNudgeDispatch`
- **复杂度**：
  - 旧实现：`readAllResult <- readAllResult @ [ e ]`。每次追加复制整个事件流列表，O(N)，长会话下 O(N²) 卡顿。
  - 优化后：内存缓存变更为 `ResizeArray<WanEvent>`，追加 O(1) 均摊。仅在 `ReadAllEvents` 返回边界转为 F# List。
- **状态**：**已修复**。

### Backlog 折叠 (`src/Kernel/EventLog/Fold.fs`)

- **审计路径**：`applyEvent` → `st.Backlog @ (backlogEntryFromPayload e.Payload |> Option.toList)`
- **复杂度**：
  - 旧实现：事件流每次 fold 重放 backlog 时使用 `@` 进行 O(N) 尾部列表拷贝。
  - 优化后：改为头插法 `entry :: st.Backlog` (O(1))，仅在边界 `EventLogRuntimeSync.fs` 消费时一次性 `List.rev`。
- **状态**：**已修复**。

### 拓扑排序 (`src/Kernel/Wanxiangzhen/Dag.fs`)

- **审计路径**：`topologicalOrder`
- **复杂度**：
  - 旧实现：DFS 访问时 `Result = stDep.Result @ [ id ]`。
  - 优化后：改为 `id :: stDep.Result`，顶层 `Ok (List.rev finalState.Result)` 返回，从 O(N²) 降为 O(N)。
- **状态**：**已修复**。

---

## 4. 工具输出信息 (ToolOutputInfo)

### appendInfo (`src/Kernel/ToolOutputInfo.fs`)

- **审计路径**：`appendInfo item msg` → `render`
- **复杂度**：
  - 旧实现：`msg.info @ [item]` — O(N) 尾部追加。
  - 优化后：`item :: msg.info` — O(1) 头插。`render` 内部 `List.rev msg.info` 一次性反转，整体 O(N) 但仅执行一次。
- **状态**：**已修复**。

---

## 5. 豁免清单 (Exemptions)

以下文件包含 `@ [` 或 `@ (` 模式，但经验证为 O(1) 有界操作或非热路径，予以豁免。

### Fuzzy grep 格式化 (`src/Kernel/FuzzyFormat.fs`)

- **路径**：`grepMatchLines` 内的 `before @ [ matchLine ] @ after`
- **豁免理由**：上下文行数固定（通常 2-3 行），拼接为 O(1) 常数开销。

### Fuzzy 路径构造 (`src/Kernel/FuzzyPath.fs`)

- **路径**：`buildQuery` 内的 `pathParts @ normalizeExcludes exclude cwd @ [ pattern ]`
- **豁免理由**：一次性查询字符串构造，列表长度固定（路径部分 + 排除项 + 1 模式），O(1)。

### ReviewSession 子节点 (`src/Kernel/ReviewSession/Types.fs`)

- **路径**：`addChild` 内的 `session.childIds @ [ childId ]`
- **豁免理由**：review session 子节点数量极少（通常 0-3 个），且 addChild 仅在 review 激活时触发，非 per-turn 热路径。

### 子代理提示词构建 (`src/Kernel/SubagentPrompts.fs`)

- **路径**：`coderTargetItem` / `coderPrompt` 内的 field list 构造
- **豁免理由**：field 数量固定（2-4 个），一次性对象构造，O(1)。

### Opencode Agent 配置编码 (`src/Shell/OpencodeAgentConfigCodec.fs`)

- **路径**：`encodeAgentScalarsRecord` 内的 pairs 列表构造
- **豁免理由**：field 数量固定（2-5 个），一次性对象构造，O(1)。

### Mux AI 设置合并 (`src/Mux/AiSettings.fs`)

- **路径**：`resolveDelegatedAgentAiSettings` 内的 settings 列表构造
- **豁免理由**：配置层级浅，合并动作仅在启动或切换 workspace 时发生一次，非 per-turn 热路径。

---

## 6. 审计结论

所有 per-turn / per-message 热路径均已优化至 O(1) 或 O(log N)。
架构测试 `noQuadraticListAppend` 作为门禁持续扫描全部 src 目录，
任何新增 `@ [` / `@ (` 模式将触发构建失败，除非显式加入豁免清单并注明理由。

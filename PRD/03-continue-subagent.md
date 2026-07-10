# PRD: Continue Subagent Tool Specification

## 1. 目标与价值
目前，万象术（Wanxiangshu）中提供的 subagent-like tools（包括 `coder`、`investigator`、`meditator`、`browser`）是一次性（fire-and-forget）运行的。子代理执行完成后，即使其会话在宿主端依然存活，外层代理也无法对其进行后续追问、微调或继续交互。

本功能通过引入 **`continue` 工具**，使任意能使用 subagent-like tools 的 agent 均可在已有子会话基础上进行多轮交互。

---

## 2. 核心概念设计

### 2.1 Subagent Session Iterator
为了在多次工具调用间保存和引用子会话，我们建立 `SubagentIterator` 机制，其生命周期管理如下：
- **表示形式**：通过前缀为 `sai_s` 的字符串标识符（如 `sai_s1`）表示。
- **关联数据**：每个 iterator 绑定子会话标识 `{ childID: string; agent: string }`。
- **存储**：在 Shell 层的 `RuntimeScope` 中新增 `SubagentIteratorStore`。限制 LRU 缓存上限（默认 50 个会话），避免内存泄漏。
- **回收**：生命周期与 session 作用域绑定。外层 session 清理或重置时（通过 `clearTypedIteratorScope`），该作用域对应的 iterator 也被一并清理。

### 2.2 工具交互协议
1. **首次运行 (spawn)**：
   外层 LLM 调用 `coder`/`investigator`/`meditator`/`browser`。
   - 子代理执行返回后，将其子会话注册到 `SubagentIteratorStore`，取得 `iterator` ID（如 `sai_s1`）。
   - 将此 `iterator` 放入 YAML front matter（通过 `ToolOutputInfo.withIterator`），以 `iterator: "sai_s1"` 返回给外层 LLM。

2. **追问/继续 (continue)**：
   外层 LLM 决定对该子会话追加提问。
   - 调用新增的 `continue` 工具。
   - **输入参数**：
     - `iterator`: 上一步返回的 iterator 字符串（必填）。
     - `prompt`: 追问的内容（必填）。
   - **执行过程**：
     - 从 `SubagentIteratorStore` 中取出该 `iterator` 并回收（单次有效/防并发）。
     - 还原出 `childID` 及 `agent`。
      - 调用 `IHostAdapter.ContinueSubagent(childID, agent, prompt)` 向该子会话发送 prompt 并等待执行结果。
   - **返回结果**：
     - 返回子代理本次追问的输出。
     - 如果执行成功，重新存入 Store 并生成新的 `iterator` ID，通过 YAML front matter 的 `iterator` 返回。

---

## 3. 详细设计与模块划分

### 3.1 Kernel 领域规则
- **`Kernel.ToolCatalog.Subagent`**:
  定义 `continueSpec`，入参包含 `iterator` 和 `prompt`，均为必填。
- **`Kernel.ToolCatalog.Registry`**:
  将 `continueSpec` 添加至全局 `all` 工具列表。

### 3.2 Shell 服务与编解码
- **`Shell.SubagentIteratorStore`**:
  实现 `SubagentIteratorStore` 类，提供 `storeSubagentIterator`、`consumeSubagentIterator`、`clearScope` 方法。
- **`RuntimeScope`**:
  注入 `SubagentIteratorStore` 实例，提供访问与生命周期事件钩子。
- **`IHostAdapter`**:
  增加接口成员：
  `abstract ContinueSubagent: childID: string * agent: string * prompt: string -> JS.Promise<SubagentResponse>`

### 3.3 宿主适配层接线
- **Opencode**:
  - `SubagentIo.fs` 新增 `continueSubagentCoreResult` 方法，调用 `promptWithAbort` 向已有子会话发送追问 prompt。
  - `OpencodeHostAdapter` 继承实现 `ContinueSubagent`。
  - `SubagentTools.fs` 注册 `continueTool`，并在 `createTools` 中挂载。
- **Mux**:
  - 类似适配。
- **Omp**:
  - 类似适配。

---

## 4. TDD 计划
1. **单元测试 (`tests/SubagentIteratorStoreTests.fs`)**:
   - 验证 `SubagentIteratorStore` 的存、取、LRU 回收和 Scope 隔离。
2. **集成测试 (`tests/SubagentDispatcherTests.fs` / `tests/IntegrationSubagentSpecs.fs`)**:
   - 模拟调用 `continue` 工具，确保其能从 iterator 正确解析并在对应子会话中发送消息。

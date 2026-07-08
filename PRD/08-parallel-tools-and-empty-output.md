# PRD-08: worktree-feature1 相对于 master 的功能与设计变更

## 1. 变更概述
相对于 `master` 分支，`worktree-feature1` 引入了三项旨在保障开发纪律、提升多工具调用并发效率以及增强大模型（LLM）异常空输出容错自愈能力的关键设计变更：
1. **TDD 开发纪律参数校验升级**（`warn_tdd` 规范值更新）。
2. **并行工具调用鼓励机制**（Parallel Tool Calling Encouragement）。
3. **LLM 空输出进入 Idle 时的 Fallback 恢复与降级拦截**（Empty-Output Fallback Recovery）。

---

## 2. 设计与实现细节

### 2.1 TDD 开发纪律校验值升级 (`warn_tdd`)
- **背景**：为了防止开发者/Agent 在修改代码时绕过 todo backlogs 更新，项目加强了对 TDD 规范的静态校验。
- **设计变更**：
  - 修改了 `WarnTdd` 模块中 `warn_tdd` 参数的规范值。
  - **旧规范值**：`"i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles"`
  - **新规范值**：`"i-am-sure-i-have-followed-tdd-and-kolmolgorov-principles-and-kept-todo-updated"`
- **实现范围**：`src/Kernel/WarnTdd.fs`，所有相关 Modification 类的工具参数默认 Schema，以及 `tests/WarnTddKernelFactsTests.fs`。

### 2.2 并行工具调用鼓励机制 (FEATURE1)
- **第一性原理**：单步调试（Linear Single-Tool loops）会剧烈消耗系统响应时效和 token。应当鼓励 Agent 并发调度正交工具（如并行 fuzzy_find + read）。
- **判定逻辑**：
  - 在 host 消息转换管线中，检查过滤后的原生消息链。
  - 定位最后一条 `Assistant` 消息，若它**有且仅有一个**有效 tool call（过滤掉以 `semble-call-` 或 `caps-call-` 开头的合成辅助工具调用），且其紧随其后的是一个 `ToolResult` 消息（表示该单步工具的执行结果已返回）。
- **干预机制**：
  - 动态向 LLM 注入一条 synthetic 消息（Role = `User`，ID = `parallel-tool-synth-<callID>`）。
  - 该消息携带强硬指令，敦促 Agent 并发处理工具。
- **生命周期生命化**：
  - 标记 `source = Synthetic "parallel-tool-synth-"`，在下一轮交互中被 `stripSyntheticBySource` 自动剥离，防止污染上下文和造成窗口膨胀。
- **实现范围**：`src/Kernel/Messaging.fs`, `src/Kernel/PromptFragments.fs`, `src/Shell/MessageTransformPipeline.fs`。

### 2.3 LLM 空输出 Idle 状态 Fallback 恢复机制
- **第一性原理**：LLM 响应结束进入 idle 状态时，若未调用任何工具且没有任何可见文本输出，这属于**隐式异常/失败**。应由 Fallback 接管并发送 `continue` 指令使其尝试自愈，而不是误触常规 `nudge`。
- **设计变更**：
  - 在 `IsSessionIdle` 信号到达时，异步拉取消息历史。
  - 检测最后一条 `Assistant` 消息的 `parts`：
    - 若不存在 `type = "tool"` 或 `type = "dynamic-tool"` 的工具调用。
    - 且所有的 `type = "text"` parts 经合并 trim 之后为空（0字节，排除非文本的 `reasoning` 类型的 parts）。
  - 一旦满足条件，将原 `SessionIdle` 事件翻译并改写为 `EmptyOutputError` 错误事件。
- **恢复与降级路径**：
  - 该事件传入 Fallback 状态机，根据 perfect-square 算法以及 model chain 降级链，自动执行 `SendContinue` 指令。
  - 本次 idle 状态被 fallback 消费，输出 `Consumed = true`，**完全短路/阻止**了 nudge 调度。
- **实现范围**：`src/Shell/FallbackMessageCodec.fs`，`src/Shell/FallbackEventBridge.fs`。

---

## 3. 受影响的模块与测试覆盖

- **WarnTdd 校验测试**：`tests/WarnTddKernelFactsTests.fs` 覆盖了校验机制的升级。
- **消息与注入机制测试**：`tests/MessageTransformPolicyTests.fs`。
- **空输出 Fallback 检测测试**：`tests/FallbackMessageCodecTests.fs` 增加了针对 `isIdleNoContentAndNoTools` 行为的五套边界用例测试。
- **集成测试**：各宿主集成测试及 E2E 测试环境已完成全量验证，保证 `worktree-feature1` 的总测试通过数稳步上升且无 regression。

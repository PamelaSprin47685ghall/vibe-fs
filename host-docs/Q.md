# OpenCode 官方仓库专项调研任务清单

## 一、所有智能体共同遵守的调查规范

每项调查必须首先明确以下基线：

1. 用户当前实际使用的 OpenCode 版本。
2. 对应 release tag 的 commit SHA。
3. 官方最新 release tag。
4. 官方 `dev` 分支当前 commit SHA。
5. 结论属于：

   * 目标 release 已有；
   * 仅 dev 分支已有；
   * 尚未实现；
   * 曾经存在但后来改变；
   * 官方接口明确不稳定。

每个智能体必须提交：

* 关键调用链；
* 精确文件和行号；
* 输入、输出和事件 payload；
* 正常序列；
* Abort、异常和竞态序列；
* release 与 dev 差异；
* 对本项目修复方案的直接影响；
* 可依赖的稳定契约；
* 不可依赖的内部实现；
* 最小复现实验；
* 建议集成测试。

禁止只引用文档或 issue 下结论。必须以目标 release 的源码为主，文档、issue 和 dev 分支只用于交叉验证。

---

# 二、P0：必须优先调查的官方行为

## 调研 1：Esc 最终调用了什么官方取消接口

### 调查目标

确认用户在 OpenCode TUI 中按 Esc 后，完整调用链究竟是什么。

### 必须回答的问题

1. Esc 在 TUI 中由哪个 key handler 接收？
2. 它调用的是：

   * session abort HTTP API；
   * SDK `session.abort`；
   * 内部 `SessionPrompt.cancel`；
   * AbortController；
   * 还是其他接口？
3. 连续按 Esc 的行为是否不同？
4. 首次 Esc 是取消当前 stream，还是只关闭 UI/弹窗？
5. Esc 是否同时取消：

   * 当前 LLM stream；
   * retry；
   * tool execution；
   * subagent；
   * compaction；
   * 已排队的 prompt；
   * synthetic auto-continue？
6. 取消 API 返回时，底层 stream 是否已经真正结束？
7. 取消后是否可能仍有旧 Promise、Effect fiber 或事件继续写入 session？
8. OpenCode 是否有 cancellation generation、request ID 或 run ID？
9. 一个旧请求取消后，新 prompt 立即开始，旧请求是否可能污染新请求？

官方当前 `SessionPrompt` 明确暴露 `cancel(sessionID)`，其实现调用 session run state 的取消操作；但这只能证明存在取消入口，不能证明 Esc、服务端 API和全部在途副作用都使用了同一条路径。

### 重点搜索路径

* TUI keybind、session route、prompt route
* `packages/opencode/src/session/prompt.ts`
* session run-state 或 abort controller 实现
* `processor.ts`
* `retry.ts`
* server session routes
* SDK 生成的 session abort 方法
* ACP、CLI run、Desktop 的取消路径

### 期望产物

画出下面这条时序图：

```text
Esc
→ TUI handler
→ SDK/HTTP request
→ server route
→ SessionPrompt.cancel
→ AbortSignal/fiber interruption
→ assistant error persistence
→ session.error
→ session.status idle
→ session.idle
```

并标出每一步是否可能晚到、重复或缺失。

---

## 调研 2：Abort 在官方消息和错误模型中如何表达

### 调查目标

确定本项目应该用哪些字段稳定识别“用户主动取消”，避免把它误判为空输出或普通可重试错误。

### 必须回答的问题

1. DOM `AbortError` 最终被转换为什么官方错误对象？
2. 错误名称是否固定为 `AbortedError`？
3. payload 中是否有：

   * `name`
   * `message`
   * `data`
   * `isRetryable`
   * `cause`
   * `metadata`
   * `aborted`
4. 用户主动 Esc、网络断开、provider 中断、tool abort 是否使用同一种错误？
5. Assistant message 是否一定会被创建？
6. 如果取消发生在第一个 token 之前，是否仍会产生 assistant message？
7. Assistant message 的 `finish` 是什么？
8. 空 text part 是否仍会写入？
9. `session.error` 是否一定发出？
10. `session.status=idle` 是否一定晚于错误消息持久化？
11. 是否存在“只有 idle，没有 error/message”的取消路径？
12. retry 或 compaction 中的 Abort 是否有不同表现？

官方 `MessageV2.fromError` 当前会把 DOM `AbortError` 转换为 `AbortedError`；消息转换代码也对带内容的 aborted assistant 做了特殊保留。但仍需确认所有 Host 路径是否都会经过这里。

### 重点搜索路径

* `packages/opencode/src/session/message-v2.ts`
* `packages/opencode/src/session/processor.ts`
* `packages/opencode/src/session/llm.ts`
* provider error parser
* core session error schemas
* EventV2 bridge
* server SSE event schemas

### 关键结论用途

最终要产出一张 Abort 识别表：

| 场景                 | 官方错误类型 | assistant 是否存在 | session.error | idle | 是否可重试 |
| ------------------ | ------ | -------------: | ------------: | ---: | ----: |
| 用户 Esc             |        |                |               |      |       |
| SDK AbortSignal    |        |                |               |      |       |
| provider stream 中断 |        |                |               |      |       |
| 网络断开               |        |                |               |      |       |
| tool 被取消           |        |                |               |      |       |
| compaction 被取消     |        |                |               |      |       |

---

## 调研 3：`session.idle` 到底表示什么，事件顺序是否有保证

### 调查目标

确认 `session.idle` 能否被视为“真人轮次自然结束”。

### 已知风险

官方当前状态服务只要被设置成 idle，就发布 `session.status` 和 `session.idle`，idle payload 本身只有 session ID，没有停止原因。因此，单独观察 `session.idle` 无法区分正常完成、Abort、compaction、retry settle 或其他内部结束。

### 必须回答的问题

1. 哪些代码路径会调用 `status.set(idle)`？
2. 是否包括：

   * 正常 assistant stop；
   * Abort；
   * error；
   * retry 间隙；
   * compaction 完成；
   * tool execution 结束；
   * subagent 结束；
   * prompt loop break？
3. `session.status idle` 和 `session.idle` 是否总是相邻？
4. 它们与以下事件的相对顺序是什么：

   * `message.updated`
   * `message.part.updated`
   * `session.error`
   * tool result
   * compaction summary
   * synthetic continue
5. 插件 event handlers 是串行还是并发调用？
6. 同一 session 的事件是否保证顺序？
7. SSE 重连是否可能重复事件？
8. 插件是否可能晚于 session 状态改变才收到事件？
9. 一个 idle handler 内调用 prompt，是否会与当前 prompt cleanup 竞态？
10. 是否存在 prompt HTTP 请求已经返回、但 idle event 尚未处理的情况？

### 重点搜索路径

* `packages/opencode/src/session/status.ts`
* `prompt.ts`
* `processor.ts`
* event publisher/EventV2 bridge
* plugin trigger
* SDK SSE client
* CLI run 和 ACP event consumer

### 期望产物

给出每类终止场景的官方事件序列，而不是只给概念说明。

---

## 调研 4：插件从 idle/event hook 中再次发 prompt 是否安全

### 调查目标

确认 todo、review、fallback nudge 应通过什么官方接口发送，什么时候发送不会产生 BusyError、重入或递归。

### 必须回答的问题

1. 插件在 `session.idle` event handler 中调用 `client.session.prompt` 是否受到官方支持？
2. 此时 session 的 run state 是否已经完全释放？
3. prompt API 是否会执行 busy assertion？
4. `prompt`、`promptAsync`、command、TUI submit 的区别是什么？
5. 如果 busy，会：

   * 抛 BusyError；
   * 排队；
   * 中断当前请求；
   * 静默失败？
6. OpenCode 是否有官方 queued message API？
7. 是否能在下一次 loop iteration 注入消息而不创建新 user turn？
8. 一个 plugin-generated prompt 是否再次触发：

   * `chat.message`
   * session busy
   * message events
   * idle
   * 同一个插件的 nudge 逻辑？
9. 如何避免 nudge 自递归？
10. Prompt API 是否返回新 message ID？
11. 能否将自定义 correlation ID 写入消息并持久保存？
12. 插件 event handler 未返回时调用 prompt，会不会造成死锁？

官方 Server 以 OpenAPI 3.1 作为 SDK 类型源，因此智能体必须同时检查目标版本的 `/doc` 规范和源码实现，不能只依据手写插件文档。

---

# 三、P0：工具 Hook 与控制字段安全边界

## 调研 5：`tool.execute.before/after` 在目标版本是否真的覆盖所有工具

### 调查目标

确认 warn、warn_tdd、warn_reuse 的校验能否依赖官方 hook。

### 必须逐类核实

* 内置 read/edit/write/bash/glob/grep
* 自定义插件工具
* MCP 工具
* task 工具本身
* task 启动的 subagent 内部工具
* reviewer/compaction agent 使用的工具
* 用户直接执行的工具
* shell/PTY 类工具
* structured output 工具
* 动态注册工具

### 必须回答的问题

1. `tool.execute.before` 的所有真实 call site 在哪里？
2. 每种工具是否都经过这些 call site？
3. subagent 是否运行在同一插件实例和项目上下文？
4. task 子会话是否继承外部插件 hooks？
5. `tool.execute.after` 是否在成功和失败时都触发？
6. tool abort 时 after 是否触发？
7. before 抛错后 after 是否触发？
8. permission 拒绝发生在 before 之前还是之后？
9. schema validation 发生在 before 之前还是之后？
10. MCP 和插件工具是否使用不同 wrapper？
11. 是否存在直接调用 tool `execute`、绕过 hook 的内部路径？
12. callID 在所有工具路径中是否唯一？
13. 是否包含 messageID、agent ID 或 parent session ID？

必须特别比较目标版本和当前 dev。官方历史 issue 曾报告 subagent 可以绕过 `tool.execute.before`；另一个较新的 issue 又报告 before/after 类型已经声明但运行时没有调用点。这说明不同版本间实现曾显著变化，不能依据单个 issue 推断当前行为。

### 重点搜索路径

* `packages/plugin/src/index.ts`
* `packages/opencode/src/plugin/index.ts`
* `packages/opencode/src/session/prompt.ts`
* `packages/opencode/src/session/processor.ts`
* `packages/opencode/src/session/llm.ts`
* tool registry
* task tool
* MCP tool adapter
* subagent/session spawn

### 期望产物

输出覆盖矩阵：

| 工具类别        | before | 参数可改 | 可阻断 | after | subagent 内覆盖 |
| ----------- | -----: | ---: | --: | ----: | -----------: |
| built-in    |        |      |     |       |              |
| MCP         |        |      |     |       |              |
| plugin tool |        |      |     |       |              |
| task        |        |      |     |       |              |
| child tools |        |      |     |       |              |

---

## 调研 6：Hook 中修改或删除 args，真实 execute 是否一定收到修改结果

### 调查目标

确认 Host 是否会在 before hook 和真实 execute 之间克隆、重新解析或重新构造参数。

官方当前插件类型把 before 的参数放在 `output.args` 中，而 after 的参数放在 `input.args` 中。这说明官方设计意图允许 before 修改参数，但不能据此假设每条执行路径都传递同一对象引用。

### 必须回答的问题

1. before hook 得到的 `output.args` 来源是什么？
2. hook 返回后，系统：

   * 原样使用该对象；
   * structured clone；
   * JSON round-trip；
   * schema 再解析；
   * 根据原始 tool call 重建？
3. 多个插件依次修改时，后一个插件是否看到前一个的改动？
4. 插件执行顺序是否与 config 顺序一致？
5. 参数对象是否可能 frozen？
6. 删除未知字段后，后续 schema parse 是否重新从原始 JSON 恢复？
7. MCP tool 是否再次编码参数？
8. task/subagent 是否把参数跨进程或跨 session 序列化？
9. after hook 中的 `args` 是：

   * 原始参数；
   * hook 修改后参数；
   * decoder 归一化后的参数；
   * execute 实际收到的参数？
10. 修改嵌套字段和删除顶层字段是否行为一致？

### 必须做的动态实验

写一个官方最小插件：

* before 中记录对象 identity；
* 删除 `warn_tdd`；
* 修改一个业务字段；
* 加入一个测试字段；
* 在真实 custom tool execute 中检查收到的对象；
* 在 after 中再次检查。

分别测试 built-in、custom、MCP、task 和 subagent。

### 对本项目的决策影响

只有调查后才能决定：

* before hook 是否足够；
* 是否必须包裹 custom tool execute；
* built-in 工具能否实现“执行前最后净化”；
* 是否必须将 warn 字段改成完全不进入业务 args 的其他协议。

---

## 调研 7：官方支持的“阻止工具执行”方式是什么

### 调查目标

不要先假设应该抛 JavaScript Error，也不要先假设存在 `{ block: true }`。

### 必须回答的问题

1. before hook 有无正式 block/deny 返回结构？
2. 抛异常时 OpenCode 如何处理：

   * 作为 tool error 返回模型；
   * 作为 session.error；
   * 终止 prompt loop；
   * 触发 retry；
   * 允许 fallback；
   * 崩溃插件？
3. 不同 Error 类型是否有不同语义？
4. 插件异常是否被统一捕获？
5. 多个 before hooks 中一个抛错后，后续 hooks 是否继续？
6. tool error 会否被 LLM 自动重试？
7. 抛错后实际 tool execute 是否严格为零次？
8. subagent 中行为是否一致？
9. MCP 工具行为是否一致？
10. 是否有官方 permission API 更适合阻止危险工具？
11. 是否能返回结构化的参数验证失败结果而不形成 session-level error？

### 重点搜索路径

* plugin trigger 的错误处理
* prompt tool wrapper
* AI SDK tool execute adapter
* permission handling
* processor tool-call/tool-result 分支
* retry classifier

### 期望结论

给出 warn 字段缺失时最安全的官方阻断方式，并证明不会意外触发 fallback auto-continue。

---

## 调研 8：工具 Schema 改写的最终生效链

### 调查目标

确认 `tool.definition` 修改 parameters 后，`required`、enum 和未知字段规则是否真正传递给 LLM和执行校验器。

官方当前插件接口提供 `tool.definition`，可以修改 description 和 parameters；但需要确认 parameters 在目标版本中究竟是 Zod、JSON Schema、Effect Schema 还是中间表示。

### 必须回答的问题

1. `tool.definition` 在什么时候触发？
2. 是每次 LLM request 触发，还是注册时触发一次？
3. 它发生在：

   * tool registry 之后；
   * agent tool filtering 之后；
   * provider schema 转换之前还是之后？
4. parameters 的真实运行时类型是什么？
5. 增加 `properties.warn_tdd` 和 `required` 是否足够？
6. Zod optional/required 如何转成 JSON Schema？
7. provider 是否可能忽略 required？
8. AI SDK 是否在执行前再次按 schema 校验？
9. 未知字段默认保留、剥离还是报错？
10. MCP schema 是否也经过 `tool.definition`？
11. custom tool schema 是否也经过？
12. 动态工具和 alias 是否每次都能被改写？
13. structuredClone 或序列化是否丢失 schema 方法/原型？
14. 多插件同时改写同一 schema 的合并顺序是什么？

### 必须输出

对每种 schema 形态给出正确改写办法和最终发送给模型的 JSON Schema 实物。

---

# 四、P0：Compaction 与错误 Nudge

## 调研 9：官方 Compaction 完整生命周期与事件序列

### 调查目标

确定插件如何可靠识别：

* 正在 compaction；
* compaction 已成功；
* 即将自动 continue；
* auto-continue 已创建；
* continuation 已 settle。

官方文档确认存在 `experimental.session.compacting`，它在生成 continuation summary 前触发，可注入 context 或替换 prompt。当前 dev 插件接口还包含 `experimental.compaction.autocontinue`，但必须调查它在哪个 release 首次出现。

### 必须回答的问题

1. compaction 是由什么条件触发？
2. 手工 `/compact` 和自动 compaction 的区别是什么？
3. compaction 是否创建专门的 user message/part？
4. summary assistant 是否有：

   * `agent=compaction`
   * `mode=compaction`
   * `summary=true`
   * 特定 finish reason？
5. `experimental.session.compacting` 的触发位置和异常行为是什么？
6. 是否存在 compaction completed 官方事件？
7. `experimental.compaction.autocontinue` 哪个版本开始支持？
8. 插件将 `enabled=false` 后，session 如何结束？
9. compaction 完成与 synthetic continue 创建之间是否可能被 Esc 打断？
10. synthetic continue 创建后，是否立即进入 prompt loop？
11. 是否经过 `chat.message` hook？
12. 是否发布普通 `message.updated`？
13. 是否导致普通 `session.idle`？
14. compaction 失败、Abort、overflow 时序有什么不同？
15. compaction 自己再次 overflow 时如何处理？
16. compaction 是否可能连续执行两次？

官方当前 dev 的 compaction 实现会创建 synthetic user continuation，沿用原 user message 的 agent/model，并在 text part 上写入 `synthetic: true` 和 `metadata.compaction_continue=true`；但源码注释明确说这个 metadata 不是稳定插件契约。因此只能作为目标版本调查对象，不能直接写死依赖。

### 期望产物

分别输出：

* 自动 compaction 正常时序；
* 手动 compaction 时序；
* compaction Abort 时序；
* compaction 失败时序；
* auto-continue 被关闭时序；
* compaction 后再次 overflow 时序。

---

## 调研 10：插件能否稳定识别 synthetic/compaction/fallback 消息来源

### 调查目标

确定是否有官方 provenance，而不是继续依赖零宽字符、英文文本或时间戳。

### 必须回答的问题

1. User message schema 是否包含 `synthetic`？
2. `synthetic` 在 message info 还是 part 上？
3. API、SSE 和 plugin hook 是否都能看到？
4. 自定义插件创建 prompt 时能否设置 synthetic？
5. 自定义 metadata 是否允许写入？
6. metadata 是否持久化到数据库？
7. metadata 是否出现在：

   * `chat.message`
   * `message.updated`
   * `experimental.chat.messages.transform`
   * SDK message query？
8. 未知 metadata 是否会被 schema 删除？
9. OpenCode 自己有哪些 synthetic message 类型？
10. compaction、title、system reminder、retry、assistant prefill 是否有官方标记？
11. 是否有可用的 messageID/parentID/callID 来做 correlation？
12. 插件能否给消息加稳定的 continuation ID？
13. 如果不能，最稳定的官方替代是什么？

当前 `chat.message` hook 输入已经可以携带 sessionID、agent、model、messageID 和 variant，这是建立轮次投影的重要候选入口，但仍需核实 synthetic 消息是否都会经过该 hook。

---

## 调研 11：如何跨 Compaction 持久保留原始 Review Task

### 调查目标

决定 review task 应通过哪条官方渠道进入压缩结果。

### 必须回答的问题

1. `experimental.session.compacting` 能否在 hook 中通过 client 查询完整 session messages？
2. hook 调用时是否允许异步读取 EventLog 或文件？
3. `output.context` 的内容最终放在什么位置？
4. 如果设置 `output.prompt`，为什么 context 会被忽略？
5. 插入的 original task 是否会出现在 summary 的模型输入中？
6. summary 模型是否可能仍然遗漏它？
7. 是否能在 compaction 后的 synthetic continue 中再次注入 task？
8. `experimental.chat.messages.transform` 是否会对 compaction 输入执行？
9. 是否会对 compaction 后普通 continuation 再次执行？
10. 任意 YAML front matter 是否会被原样保留？
11. compaction summary 边界如何选择消息？
12. 最后若干 user turns 是否一定保留？
13. 规则、原始任务和 system prompt 是否可能被压缩掉？
14. 对 child/reviewer session 是否同样工作？

官方 compaction 代码会在生成 summary 前调用 `experimental.session.compacting`，然后对选中的消息调用 `experimental.chat.messages.transform`。官方自身的 compaction 改进计划也承认任务、规则和用户约束曾可能在压缩后丢失。

### 调查结论应比较三种方案

* 依赖 summary 保存任务；
* compaction hook 注入任务；
* compaction 后从本地 EventLog/ReviewProjection 重新注入任务。

---

# 五、P0：模型路由

## 调研 12：OpenCode 中“当前模型”的官方权威来源

### 调查目标

明确 todo/review nudge 应使用哪个模型，避免依赖旧 assistant 或 stale fallback model。

### 必须回答的问题

1. UserMessage 中模型的标准结构是什么？
2. AssistantMessage 是否使用不同结构：

   * user: `model.providerID/modelID`
   * assistant: 顶层 `providerID/modelID`
3. variant 存在哪里？
4. reasoning effort、thinking、temperature 等设置存在哪里？
5. UI 切换模型时更新的是：

   * 全局 config；
   * session；
   * 下一条 user message；
   * TUI 本地状态？
6. 是否发出 `session.updated`？
7. `session.updated` 是否包含 model？
8. Host 是否提供“当前 session active model”查询 API？
9. 当前 busy request 的真实 model 能否通过事件取得？
10. message override 与 session default 哪个优先？
11. 插件调用 prompt 时不传 model，会选：

    * session 当前模型；
    * 全局默认；
    * 上一条 user model；
    * agent 默认？
12. 插件显式传 model 时，具体请求字段是什么？
13. variant 是否能显式传递？
14. compaction continuation 使用什么模型？
15. retry/fallback 切换模型后，session 当前模型是否永久改变？
16. child session 是否继承 parent model？
17. reviewer/compaction agent 是否使用专用小模型？

官方当前 `chat.message` hook 输入包含可选 model 与 variant；`chat.params` 则能看到本次真正发送给 LLM 的 model 和 provider。这两者可能比 session runtime 缓存更适合作为模型观测来源，但要调查二者触发顺序和覆盖范围。

### 必须产出

一张路由优先级表：

| 场景                       | 推荐权威来源 | 明确传 model 是否必要 |
| ------------------------ | ------ | -------------: |
| 真人普通消息                   |        |                |
| todo nudge               |        |                |
| review nudge             |        |                |
| fallback attempt         |        |                |
| compaction               |        |                |
| compaction auto-continue |        |                |
| child agent              |        |                |

---

# 六、P0：Token 与 Context Budget

## 调研 13：官方 token usage 字段的准确语义

### 调查目标

确定 Context Budget 公式该使用哪个数，避免重复加 cache token 或把累计费用当作当前上下文。

### 必须回答的问题

1. Assistant message 的 tokens schema 是什么？
2. 是否包含：

   * input
   * output
   * reasoning
   * cache.read
   * cache.write
3. input 是否已经包含 cache.read？
4. cache.read 是费用统计还是上下文占用？
5. 每个 assistant message 的 token 是：

   * 该次 request；
   * 当前完整上下文；
   * 当前 step；
   * session 累计？
6. 多个 tool step 是否产生多个 assistant message？
7. 最后一个 assistant message token 是否代表下一轮将发送的上下文？
8. compaction summary token 如何记录？
9. title/compaction agent usage 是否混入普通 session？
10. provider 不返回 usage 时字段是什么？
11. OpenAI-compatible、Anthropic、Gemini、Copilot 的映射是否一致？
12. `includeUsage` 关闭时会怎样？
13. usage 在 stream 哪个时刻写入？
14. Abort 时是否有部分 usage？
15. cache token 是否可能重复累计？

官方近期 issue 已出现 cache.read 导致 session token 统计异常增长、compaction token 计算失真等报告，因此必须以目标版本源码和实际 provider 测试为准。

### 重点搜索路径

* core session Assistant token schema
* processor finish-step
* provider usage normalization
* session summary/database aggregation
* compaction `isOverflow`
* token utility
* model cost calculation

---

## 调研 14：模型上下文上限和有效预算如何由官方计算

### 调查目标

避免本项目使用错误的 100K/120K 通用默认值，也避免重复保留 output reserve。

### 必须回答的问题

1. Provider Model 的 limit schema 是什么？
2. context 与 output 上限分别存在哪里？
3. limit 是否已经扣除输出预算？
4. OpenCode 的 `usable`、`isOverflow` 如何计算？
5. 是否考虑 provider 特定上限？
6. 是否考虑模型 variant？
7. provider list API 的正式返回结构是什么？
8. 自定义 provider/OpenAI-compatible 模型如何配置 limit？
9. 无 limit 时 OpenCode 使用什么默认值？
10. 未知模型是禁用 compaction，还是使用 fallback limit？
11. tool definitions、system prompt、attachments 是否计入估算？
12. `experimental.chat.messages.transform` 增加内容后，OpenCode 是否重新计数？
13. 插件是否能访问最终 outbound token estimate？
14. compaction 判断是在 tool result 加入前还是后？
15. 大型 tool output 是否会在下一轮才被发现？

官方已有 issue 指出大型 tool 输出可能没有及时计入 compaction 判断，以及 compaction 自身可能超过模型限制。

### 期望产物

给出本项目获取 `MaxInputTokens` 的官方优先级，并说明每个 provider 的缺失行为。

---

# 七、P1：事件投影和消息身份

## 调研 15：MessageID、ParentID、CallID 能否构建稳定的 TurnIdentity

### 调查目标

确定是否需要自建 humanTurnId，还是可以可靠复用 OpenCode 官方 ID。

### 必须回答的问题

1. User 和 assistant message ID 如何生成？
2. ID 是否单调递增？
3. 是否能按字符串比较时间顺序？
4. assistant `parentID` 是否总是对应本轮 user message？
5. retry 产生的新 assistant 是否仍指向同一个 user？
6. compaction summary parent 是谁？
7. synthetic continue 是否创建新 user message ID？
8. tool callID 是否与 assistant message 关联？
9. 当前 plugin hook 的 tool before 是否带 messageID？
10. 不带 messageID 时能否从 callID 查询？
11. subagent 是否有 parent session ID？
12. session fork/revert 后 ID 关系如何变化？
13. message 删除或重写是否保留 ID？
14. EventV2 是否可能 dual-write 两套 ID？
15. messageID 是否跨 session 唯一？

官方当前 tool hook 类型只有 sessionID 和 callID，没有 messageID，而相关官方 issue曾专门请求增加 messageID。这会直接影响控制字段审计和 turn correlation。

### 期望结论

明确：

* 哪些官方 ID 可直接使用；
* 哪些只能作为辅助；
* 本项目是否仍必须自建 generation 和 continuation ID。

---

## 调研 16：事件回放、重复、乱序和插件并发语义

### 调查目标

判断本地 EventLog projection 能否直接按收到顺序折叠。

### 必须回答的问题

1. plugin `event` hook 是否同步阻塞事件发布？
2. 多插件 event hook 是否串行？
3. 同一插件的两个 event 调用是否可能并发？
4. 同一 session 是否保证顺序？
5. 不同 session 是否并发？
6. hook 慢时会阻塞主循环吗？
7. hook 抛错会影响事件后续处理吗？
8. SSE 和直接 plugin event 是否使用同一源？
9. 断线重连会否 replay？
10. 是否有 event ID 或 sequence number？
11. message.updated 是否可能对同一 message 多次发出？
12. part delta 和 completed 是否可能晚于 idle？
13. EventV2 bridge 是否会产生重复 legacy/V2 事件？
14. 进程崩溃时事件与数据库写入谁先谁后？

### 对本项目的影响

这决定本地 NDJSON 必须采用：

* 幂等键；
* sequence；
* event version；
* duplicate suppression；
* generation validation；

还是可以简单按文件顺序折叠。

---

# 八、P1：插件生命周期与异常语义

## 调研 17：插件加载顺序、Hook 顺序和异常隔离

### 调查目标

确认多个插件、多个 Hook 和本插件内部 wrapper 的优先级。

### 必须回答的问题

1. 插件按配置数组顺序加载吗？
2. 本地插件和 npm 插件谁先？
3. hooks 是否按插件加载顺序执行？
4. 一个 hook 修改 output 后，下一个是否看到修改？
5. 一个 hook 抛错，后续 hook 是否执行？
6. plugin 初始化失败是否跳过其他插件？
7. dispose 何时调用？
8. workspace、project、server 是否各有独立插件实例？
9. child session 是否复用插件实例？
10. plugin reload 是否存在？
11. hook 是否有 timeout？
12. event handler 内部未捕获 Promise rejection 如何处理？
13. 插件调用 SDK 回到同一个 server 是否可能重入？
14. 插件能否安全保存 per-session 内存状态？

官方曾有插件加载循环被某插件影响、后续插件被跳过的 issue，因此插件顺序和异常隔离不能凭假设。

---

# 九、P2：日志与诊断

## 调研 18：OpenCode 官方日志出口及插件日志规范

### 调查目标

决定如何替换裸 `printfn DEBUG` 和 `console.log`。

### 必须回答的问题

1. OpenCode 内部使用什么 logger？
2. 日志默认写到哪里？
3. stdout、stderr 和日志文件的职责是什么？
4. TUI、`opencode run --format json`、MCP/ACP 模式下 stdout 是否是协议通道？
5. 外部插件是否获得 logger？
6. 没有 logger 时官方建议是什么？
7. `console.log` 会显示在 TUI、server console 还是日志文件？
8. 是否有 `OPENCODE_LOG_LEVEL` 等配置？
9. 是否支持结构化字段？
10. 如何脱敏 session、prompt 和 tool args？
11. 如何进行 rate limit？
12. debug 日志是否会影响 JSON 输出？

### 期望产物

给出本项目在 Opencode Host 下的日志适配方案：

* trace/debug/warn/error；
* stdout/stderr/file；
* 默认级别；
* 环境变量；
* 敏感字段规则。

---

# 十、哪些问题不需要等待官方仓库调查

下列问题可以先在本项目直接修复，不必等官方结论。

## 1. 本地 gate 漏拦 `Cancelled`

`needFallbackContinue`、terminal observation 等本地纯逻辑遗漏，可以立即修复。

## 2. Review task 在本地 Snapshot 链条中丢失

`ReviewLoopFold.Active task` 已经存在，本地只需正确透传为 `original_task`。

## 3. 本地 Nudge model 优先级明显错误

旧 injected model 无条件覆盖新真人消息模型，是本地策略错误。即使官方模型 SSOT 尚未调查完，也应先禁止陈旧 injected model 跨 human turn 生效。

## 4. 本地默认输出大量 `DEBUG:`

裸调试输出可以立即移除或接入本地 logger。官方调查只决定最终 Opencode adapter 应写到哪里。

## 5. Context Budget 在全部测量缺失时直接停摆

应先加入保守估算和显式 degraded 状态。官方调查用于优化精确测量，不影响“绝不能静默关闭”的本地原则。

## 6. Active review loop 缺原始任务时仍发送 nudge

应立即 fail closed，不必等待官方行为。

---

# 十一、推荐的并行派工方案

## 第一组：取消与事件时序

* 调研 1：Esc 调用链
* 调研 2：Abort 错误结构
* 调研 3：idle 语义与事件顺序
* 调研 4：idle 中再次 prompt 的安全性

这四项最好由同一个智能体组协同，因为需要拼成统一时序图。

## 第二组：工具安全边界

* 调研 5：hook 覆盖范围
* 调研 6：args 克隆与修改
* 调研 7：阻断语义
* 调研 8：schema 生效链

这组必须同时做源码审计和动态插件实验。

## 第三组：Compaction

* 调研 9：生命周期
* 调研 10：synthetic provenance
* 调研 11：原始任务持久化

这组需要重点比较目标 release 与 dev，因为 `experimental.compaction.autocontinue` 可能存在版本差异。

## 第四组：模型与上下文预算

* 调研 12：模型路由
* 调研 13：token usage
* 调研 14：context limit

这组三项强耦合，应同时检查多 provider。

## 第五组：基础设施契约

* 调研 15：ID 和 turn identity
* 调研 16：事件并发与重放
* 调研 17：插件生命周期
* 调研 18：日志

---

# 十二、最终汇总智能体必须回答的决策问题

所有专项报告完成后，再派一个汇总智能体只回答以下问题：

1. Esc 后，阻止旧 `SendContinue` 的最可靠官方信号是什么？
2. `session.idle` 是否可以触发 nudge；若可以，还必须联查哪些事实？
3. 插件创建 synthetic prompt 的官方方式是什么？
4. 是否能给 synthetic prompt 附加稳定 provenance？
5. tool before hook 能否作为安全边界？
6. 控制字段最终应在哪一层剥离？
7. warn 字段缺失时，如何阻止工具且不触发 session fallback？
8. subagent 是否能绕过 hook？
9. todo/review nudge 应使用哪个官方模型来源？
10. compaction auto-continue 如何被稳定识别？
11. review original task 应通过哪个官方 hook 跨 compaction 保存？
12. token usage 中是否应加 cache.read？
    13.模型 limit 缺失时官方如何处理？
13. OpenCode 是否提供足够的 message/run identity；不足部分由本项目补什么？
14. 哪些当前实现属于稳定 API，哪些只是 dev 内部细节？

汇总报告必须给出一张最终兼容矩阵：

| 能力                           | 目标 release | 最新 release | dev | 本项目适配策略 |
| ---------------------------- | ---------- | ---------- | --- | ------- |
| Abort 识别                     |            |            |     |         |
| synthetic provenance         |            |            |     |         |
| tool before                  |            |            |     |         |
| tool after                   |            |            |     |         |
| subagent hook                |            |            |     |         |
| compaction hook              |            |            |     |         |
| compaction autocontinue hook |            |            |     |         |
| explicit model routing       |            |            |     |         |
| token usage                  |            |            |     |         |
| context limit                |            |            |     |         |

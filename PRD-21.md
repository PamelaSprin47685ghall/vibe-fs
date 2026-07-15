# 六项行为修复方案

本附录与七个核心问题正交：七个问题关注 hard gate（安全/正确性），六项行为修复关注 soft-required（用户体验/协议兼容）。

## 一、统一设计原则

### 规则 1：Projection、CAPS、Hint、Budget 必须拆成独立策略

当前 `ProjectionPolicy.ExcludeProjection` 会同时造成：不投影 backlog、不注入 CAPS、不注入并行工具 user hint、部分 Host 不加载 subagent 临时文件、Context Budget 行为也和投影流程耦合。这正是 investigator 丢 CAPS、单工具调用丢 user hint 的共同根因。

应将 `MessageTransformPlan` 中单一的 `ProjectionPolicy` 拆成至少四个独立决策：

| 策略 | 控制内容 |
| :--- | :--- |
| BacklogProjectionPolicy | 是否折叠、注入 todowrite backlog |
| CapsInjectionPolicy | 是否注入 CAPS 和 subagent 临时文件 |
| ParallelHintPolicy | 是否注入"并行调用工具"提示 |
| ContextBudgetPolicy | 是否计算预算、是否允许提醒 todowrite |

agent 策略矩阵：

| Agent | Backlog | CAPS | Parallel Hint | Todo Budget |
| :--- | ---: | ---: | ---: | ---: |
| 主工作/manager/build | 是 | 是 | 是 | 是 |
| investigator | 否 | 是 | 是 | 否 |
| reviewer | 视现有设计 | 是 | 视工具能力 | 否或专用策略 |
| browser | 否 | 否 | 否 | 否 |
| executor | 否 | 否 | 否 | 否 |
| title/compaction | 否 | 否 | 否 | 否 |

绝不能简单把 `"investigator"` 从 `defaultExcludedAgents` 删除。该函数还被工具权限判断复用，直接删除会扩大 investigator 的工具权限。

### 规则 2：强提示不等于硬拒绝

`warn_tdd`、`warn`、`warn_reuse`、todowrite 的五个报告字段及 1024 字要求都应采用"软要求"。schema MUST 通过 description、examples 与 `x-wanxiangshu-soft-required` / `x-wanxiangshu-soft-min-length` 元数据强调填写；不得使用 Host 强制执行的 `required`、`minLength` 或单值 `enum`。LLM 漏填/短填时工具仍执行；工具结果在最终规范化后追加一次严厉的协议违例批评；不把结果标记为工具失败；不抹掉工具原始输出；不要求 LLM 重复执行刚刚已经成功的工具。

### 规则 3：真正的 JSON Schema required、minLength 与"漏填也执行"冲突

如果 Host 严格执行 JSON Schema，required 缺失会在工具执行前被拒绝，minLength: 1024 不满足也会在工具执行前被拒绝，万象术的 tool-before/tool-after 根本收不到调用。

推荐 schema 表达方式：字段继续存在；description 明确写 MUST 和"至少 1024 字"；增加 Wanxiangshu 自有元数据 `x-wanxiangshu-soft-required`、`x-wanxiangshu-soft-min-length` 及 examples。唯一推荐值如需展示，只能放在不参与 Host 校验的 metadata/examples 中；不得放入真正的 `required`、可执行 `minLength` 或会硬拒绝的 `enum`。

真正的 fail-closed 只保留给 malformed business args、权限/安全拒绝、parse failure，以及终极 sanitizer 发现控制字段仍泄漏到业务参数的情形。软字段缺失、空白、非规范值和短报告绝不能进入这些硬门禁。

### 规则 4：批评必须发生在所有结果规范化之后

当前多个 Host 会在 tool-after 中重写工具结果（OpenCode `ProgressObserver` 替换 todowrite 输出；OMP `TodoHooks` 替换 todowrite 输出；Mux wrapper 重新构造 output）。如果先追加批评、后做标准输出替换，批评会再次丢失。

统一顺序必须是：
1. before hook 提取并删除控制字段，保存原始值及 missing/blank/non-canonical 状态；
2. 工具真实执行；
3. 网络错误、语法诊断、livelock 等现有处理；
4. todowrite 等工具进行标准输出规范化；
5. 在 finally 性质的路径将原始控制字段恢复到所有历史可见 args；
6. 最后追加协议违例批评；
7. 删除临时 compliance envelope。

## 二、investigator 也注入 CAPS

### 现有根因

`Kernel/MessageTransformPolicy.fs` 当前把 investigator 放在 `defaultExcludedAgents`。三个 Host 据此设置 `ProjectionPolicy.ExcludeProjection`。CAPS 被错误绑定到该 policy：`Shell/MessageTransformHostHooks.loadCapsForScope` 遇到 Exclude 直接返回空列表；OMP 的 `loadCaps` 也直接根据 ProjectionPolicy 返回空列表；`injectSubagentFilesIfAny` 同样受 ProjectionPolicy 限制。

### 正确修改

修改重点：
* `Kernel/MessageTransformPolicy.fs`：保留 projection 排除规则用于 backlog 和某些工具权限，新增 `CapsInjectionPolicy.Include`、`ParallelHintPolicy.Include`、`ContextBudgetPolicy.DisableTodoEmergency` for investigator。
* `Shell/MessageTransformCore.fs` + 三个 Host 的 `MessageTransform.fs`：计划中显式携带四个 policy。
* `Shell/MessageTransformHostHooks.fs`、`Omp/MessageTransform.fs`、`Opencode/MessageTransform.fs`、`Mux/MessageTransform.fs`：`loadCapsForScope` 只看 `CapsInjectionPolicy`。

不要顺手扩大 investigator 权限：investigator 仍不可调用 todowrite、写文件、调用 coder。ToolPermission 中依赖 projection exclusion 的逻辑不能被误改。

### 验收

* investigator 输入历史开头存在 CAPS synth 消息，内容与主 agent 看到的一致。
* investigator 是 child session 时使用 parent session 的 CAPS scope。
* 连续运行 message transform 三次 CAPS 不重复叠加。
* CAPS 内容改变时 transform cache 失效。
* investigator 工具列表没有新增写权限。

## 三、单工具调用后稳定注入并行工具 user hint

### 现有根因

`Shell/MessageTransformPipeline.tryInjectParallelToolPrompt` 三个脆弱点：
1. 整个功能被 ProjectionPolicy 关闭；
2. 统计所有 ToolPart 而非实际工具调用（synthetic internal part 导致 allToolParts.Length > 1）；
3. 结果匹配没有关联 call ID（旧结果可能被误认为当前工具结果）。

### 正确修改

1. 用独立 `ParallelHintPolicy`，investigator 启用，title/compaction/browser 不启用。
2. 定义"真实工具调用"：非 synthetic call ID、非 CAPS/prefetch/internal 伪造调用、属于当前 native assistant turn 的调用。触发条件只看 `triggerableToolCallCount = 1`，不再要求所有 ToolPart 数量等于 1。
3. 按 call ID 找到对应结果：最新 native assistant message → 提取真实 call ID → 真实调用数恰好为 1 → 后续消息找到该 call ID 对应 ToolResult → 该 ToolResult 后没有更新的 native assistant message → 当前输出还没有对应 hint → 追加 synthetic user hint。
4. hint ID 稳定：基于 assistant message ID 或唯一 tool call ID，如 `parallel-tool-hint:<callID>`。同一 transform 多次运行不会重复。
5. hint 内容保持"提醒"，不成为硬拒绝。

### 验收矩阵

| Assistant 工具组成 | 应否提示 |
| :--- | ---: |
| 1 个真实 read | 是 |
| 1 个真实 read + 1 个 synthetic internal part | 是 |
| 2 个真实 read | 否 |
| 1 个真实工具但尚无结果 | 否 |
| 1 个真实工具，结果已返回 | 是 |
| 结果后已经有新 assistant message | 否 |
| title/compaction agent | 否 |
| investigator 单独调用一次 read | 是 |
| 同一消息 transform 重复执行 | 只出现一次 |

## 四、warn、warn_tdd、warn_reuse 在 tool-after 恢复

### 现有根因

`Shell/ToolHookRuntime.executeBeforeGateway` 删除 warn 字段。before hook 通过 `saveCompliance` 将 envelope 持久化到 transient store。但 tool-after 不知道被删除的 warn 值。

### 正确修改

1. 引入统一的 Tool Compliance Envelope，保存 tool name、tool call ID、warn_tdd/warn/warn_reuse 原值及状态（present/missing/blank/non-canonical）、todo 报告质量问题、是否已输出批评。
2. 建立 transient `ToolComplianceStore`：键 session ID + tool call ID，值 compliance envelope。before hook 写入，after hook 读取并删除。
3. 存储必须有回收机制：session abort、turn end、session shutdown、tool call 被拒绝、child session 清理都要删除残留 envelope。
4. tool-after 恢复到所有历史可见 args：OpenCode（input args、output args、decoded args）；Mux（decoded args、input args、output args）；OMP（tool_result event input/args）。恢复的是 LLM 原始提供值，缺失字段不要伪造。
5. 恢复必须发生在 finally 性质的路径：无论结果是 success、error、network error、syntax diagnostic、livelock，都要恢复；若硬门禁在 before 阶段拒绝且没有可见结果，也必须清理 envelope，不能留下半截状态。

## 五、warn 字段缺失时不拒绝，只在结果中严厉批评

### 现有根因

`executeBeforeGateway` 当前对 FileMutation 缺 warn_tdd、ProcessExecution 缺 warn、SubagentDelegation 缺 warn_reuse 返回 `Result.Error`。OpenCode 设置 hook error + 抛 Tool validation exception；OMP 返回 block=true；Mux 设置 hook error。这与期望行为完全相反。

### 正确修改

1. before gateway 不再因 warn 缺失返回 Error。warn 三字段属于 compliance 问题，不属于执行合法性问题。
2. schema 改为 soft-required（保留字段、description 用 MUST 文案、使用 examples 与 `x-wanxiangshu-soft-required` 标记；不进入真正 required，不使用可执行 minLength；enum 仅可作非校验 metadata）。
3. tool result 末尾追加统一批评块：不替换原输出、不把 success 改成 false、不设置 hook error、明确指出缺少哪个字段和适用能力、明确告诉 LLM 工具已执行不要重复调用、要求下一次调用改正。
4. 只追加一次：使用固定 marker `WANXIANGSHU_COMPLIANCE_REPRIMAND`，已有 marker 不再追加。
5. 三个 Host 的追加位置：OpenCode 放在 `lifecycleObserver.handleToolExecuteAfter` 之后；OMP 放在 TodoHooks 标准输出重写之后；Mux wrapper 和 hook after 统一由最后的结果装饰器追加。

## 六、todowrite 报告不足 1024 字时不拒绝，只批评

### 现有根因

多层旧实现曾造成硬拒绝（这些门禁必须删除，不能作为目标行为）：
* 旧 Schema 将五个字段放入 `required` 并设置 `minLength=1024`；
* 旧 Runtime decoder 将少于 1024 视为 InvalidIntent；
* 旧 Host 直接执行层检查长度并返回 error，或将 decoder 失败映射为 success=false/failwith。

### 正确修改

1. 分离"结构合法性"和"报告质量"。硬错误保留（todos 不是数组、todo item 缺 content、status 无法解析、priority 非法、session ID 缺失、权限问题）。软错误为五个报告字段缺失/空/不足 1024。decoder 返回两部分：可执行的 `TodoWriteArgs` 和 `ReportComplianceViolation list`。
2. schema 改为软要求：五个字段继续展示，description 明确要求不少于 1024 字，不使用真正的 required/minLength/enum 门禁，增加 `x-wanxiangshu-soft-min-length` 元数据及示例。
3. 工具必须继续完成核心工作：todos 状态更新、native todo 工具调用、EventLog 记录 work backlog commit、已提供的报告内容原样保存、缺失内容保存为空。
4. 批评必须列出每个字段的实际长度。
5. 各 Host 修改：OpenCode（ProgressObserver 不再因短报告 failwith；先写 EventLog；标准化输出；最后追加批评）；Mimocode（去掉五个长度拒绝分支）；Mux（soft decoder 后调 native todos、捕获报告、append EventLog、返回 success、结果末尾追加批评）；OMP（去掉 execute 中五个长度错误；TodoHooks 覆盖后追加）。

### 验收

1. 五个字段均超过 1024：工具成功，无批评。
2. 一个字段 1023：工具成功，todo 状态更新，EventLog 有 commit，批评只列该字段。
3. 五个字段全部缺失：工具成功，todos 正常更新，批评列五项，不抛异常。
4. todo item status 非法：仍然硬拒绝。
5. after hook 执行两次：批评只出现一次。

## 七、修复 Context Budget 开局误触发 todowrite

### 现有根因

`rebuildPhaseState` 在第一次观测且 backlog 为空时将 `phaseBaseTokens = 0`。随后同一次 message transform 立即调用 `classifyPressure`。对于默认 `foldAfterFirst=false`，N=3，初始阈值约为有效窗口的 25%。CAPS、系统提示、用户首条消息本身可能已超过窗口 25%，立刻触发紧急 todowrite。

### 正确修改

1. 第一次观测只做校准，绝不触发提醒：`rebuildPhaseState` 返回额外信息（是否刚初始化 baseline），若刚初始化则保存 state/usage，不调用 emergency injection。
2. backlog 为空时也使用真实初始 baseline：删除 `State=None && backlog.IsEmpty → phaseBaseTokens=0`，统一为 `phaseBaseTokens = stableTokens`。
3. 引入 UsageConfidence（Observed/CalibratedEstimate/BootstrapEstimate），第一次只有粗估时不触发。
4. 必须有正的阶段增长：`currentTokens > phaseBaseTokens` 或新增量超过最小噪声区间。
5. investigator 不得进入 Todo Emergency。
6. NudgeTrack 表示 episode 而不是 transform 次数：记录 episode ID、signal 时 todo ordinal、signal 时 tokens、stable synthetic nudge ID。压力仍成立且 todo ordinal 未推进时输出中继续包含同一条提醒。
7. 成功 todowrite 后正确重置 baseline。

### 验收

1. CAPS 很大占窗口 30%：第一轮不提醒。
2. 没有真实 usage 只能 bytes/2：第一轮不提醒。
3. 首次 transform 连续调用两次：仍不提醒。
4. baseline 之后新增 token 未达阈值：不提醒。
5. 新增 token 跨过 F 阈值：注入一次紧急提醒。
6. 同一消息集合重复 transform：提醒仍可见，不增加 episode 数。
7. 成功 todowrite：baseline 重建，提醒消失。
8. investigator 上下文超过阈值：不出现提醒。
9. phase base 已超过窗口 80%：走 compaction。

## 八、建议新增的共享模块

### 1. AgentProjectionPolicy

职责：给定 host、agent、是否 child session，输出四个独立 policy，所有 Host 统一调用。禁止每个 Host 自己写一套 investigator 特判。

### 2. ToolCompliance

职责：根据 tool capability 判断哪些软字段适用；提取控制字段；产生 compliance envelope；检查 warn 字段；检查 todo 报告长度；生成统一批评文本；判断结果中是否已经有批评 marker。Kernel 保持纯函数：输入 tool/args，输出净化 args/envelope/violations。Shell/Host adapter 负责保存 envelope、执行工具、恢复字段、修改结果文本、清理 transient store。

## 九、推荐实施顺序

1. **第一阶段**：先解除错误拒绝（warn 软化 + todowrite 软化 + schema 修改 + 确认工具仍正常执行）。
2. **第二阶段**：建立 after compliance 闭环（envelope/store + before 保存 + after 恢复 + 批评 + 清理）。
3. **第三阶段**：拆分消息策略（ProjectionPolicy 拆四个 + investigator CAPS/Parallel Hint/Todo Emergency off + 保持工具权限）。
4. **第四阶段**：重做 Context Budget bootstrap（初次观测只初始化 + 初始 phase base 使用当前 tokens + usage confidence + 稳定 emergency episode + 成功 todo 后重置）。
5. **第五阶段**：跨 Host 契约测试（同一组输入依次运行 OpenCode/Mimocode/Mux/OMP，断言行为一致）。

## 十、最终验收标准

1. investigator 收到 CAPS，但没有新增写权限。
2. 一个真实工具调用完成后，下一轮稳定出现并行工具提示。
3. 一个真实调用加任意 synthetic ToolPart，仍视为单工具调用。
4. warn 三字段在 tool-before 对真实工具隐藏，在 tool-after 恢复到历史 args。
5. warn 缺失不阻止工具执行。
6. warn 缺失时结果保留原输出并追加一次严厉批评。
7. todowrite 五个报告字段不足 1024 时仍更新 todos。
8. 短报告仍写入 EventLog，并追加实际长度清单。
9. invalid todo status 等真正业务错误仍然拒绝。
10. 第一轮无论 CAPS 多大都不触发 emergency todowrite。
11. 无真实 token usage 的 bootstrap 阶段不触发。
12. 达到真实增长阈值后能正确触发。
13. investigator 永远不会收到 todowrite emergency。
14. 重复 transform 不重复 CAPS、hint 或批评。
15. OpenCode、Mimocode、Mux、OMP 行为完全一致。

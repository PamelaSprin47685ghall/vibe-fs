# Prompt Authority — 理由

物理 `role=user` 廉价且可伪造。若不把 Authority 收成 typed 来源，continuation 与 repair 会自我抬升为 HumanRoot，Fallback/Review 预算被反复重置。

Dispatcher 四阶段切开「我打算发」与「Host 真留下了 `msg_*`」：`accepted-*` 只是 transport 收据。未决恢复选 at-most-once 而非重发，因为 Host 可能已开始 provider run——重发制造第二次逻辑效果，比挂起更糟。

`AttemptExecutionProfile` 必须原子：从 session cache 拼装会让 Role、工具面、probe 选择在同一请求内不一致，后续 seal/review 全部失真。

Session execution binding 以 parent 边界分权：无 parent 的 user-facing session 允许真实用户下一条消息显式选择新的 agent/model，并由 Host 继续沿用；插件自产 request 不是用户选择，仍须沿用最近真实用户 binding。有 parent 的 managed session 绑定属于创建事实，普通 hook/prompt 参数不是重绑授权。拒“这次 request 写了什么就跟什么”：一次疏忽即可把 session 静默带到另一模型，且后续消息继续沿用错误状态。内部临时 fallback/assistance 换档必须由 typed execution override 明示，不能改写 frozen base binding。

万象术没有单一 system prompt，只有语言系统。每个 provider-facing 文本恰属一个权威：World / Role / Library / Runtime / Mission。冲突按语义所有权边界裁决，不设「更靠近 system 者胜」全序。

## 备选与被拒

**身份类型与错误表达：领域身份 vs 诊断详情。** `SessionId`、`LogicalRunId`、`ToolCallId` 等身份用独立包装类型，防止跨域误传。当前 Host 发送边界只提供不透明错误详情，因此 `PromptAbandonReason` 用有限 case 区分 `SendFailed` 与 `UnresolvedAfterRecovery`，内部字符串只供诊断，禁止据其散文分叉。不得在文档中虚构实现并不存在的 `DispatchError`；若 Host 边界未来能闭合分类，应原子修改类型、发送端与证明。

**独立拼装的子记录 vs 原子 profile + 窄投影。** 拒绝分别构造 `AuthorityProfile`、`RequestProfile`、`ProjectionContext` 后再拼回请求：多写入口会让同一 attempt 的 Authority、Agent、工具面与 probe 互相矛盾。选择由唯一 builder 构造 `AttemptExecutionProfile`，其中嵌套稳定的 `AuthorityExecutionProfile`；跨边界只传所需字段或从完整 profile 做纯投影，不建立第二构造来源。

**恢复语义：exactly-once / 重发 / at-most-one。** 拒 exactly-once：Host 已可能开始 provider run，无法单边保证物理一次性。拒重发：重发在 Host 留下的 `msg_*` 之外产生第二次逻辑效果。选 at-most-once + fail-closed unknown（PROMPT-011）：未证明落地就保持 Pending，不自动补投。

**PromptKey：内容 digest vs 时间/随机窗口身份。** 拒时间窗口：跨崩溃不可靠，且无法区分「同一 Guard 连发两次」。`ClaimSequence` 由 journal fold 派生的单调序号，使同 payload 重发成为两个 key。

**恢复预算与窗口取值。** PROMPT-011 选择有限尾部窗口，以 PromptKey 证据判定物理落地而不依赖容量估算；有限启动预算避免未知结局永久挂起，同时不伪装成功或盲目重发。数值只在 PROMPT-011 定义。

**载体：metadata vs body 标签。** 拒 body：body 是 provider-visible prompt 的一部分，放恢复键会改变对话字节。放 metadata：不进入模型输入，恢复时按 key 检索（PROMPT-011）。

**System prompt：Life 内 byte-identical vs Activation / T1 / Strength 切换。**  
拒切换：`The system prompt names the office. The conversation tells you which road is yours.` Planning→Working 或 T1 改 system 会废 prefix cache、制造第二份 Role Law，并把 Manager BlindPlan 退化成旧 Activation。T1 revelation 只走 conversation tool result。

**Office Library：knowledge ≠ authority vs 书扩大 Role 权。**  
拒书授职权：书可教识别缺陷，不授修复权；可述验证技术，不授执行权。`Information may cross authority boundaries. Authority does not travel with it.` Library ≠ Common Law / 身份 / 工具面 / 运行时 / mission / 隐藏编排 / 证据替代 / 第二规范源。若他处已有 canonical，Library 组合引用，不复制第二真源。

**权威冲突：语义所有权分类 vs World>Role>Library>… 全序覆盖。**  
拒 later-text-wins / 高层覆盖一切：Mission 不能授予 Role 没有的权；Library 不能扩大 Role；Handbook 遇 concrete requirement 时具体要求胜；Rulebook 不是 present-case evidence。

**Closing / 报告：散文 vs 固定报告 schema。**  
拒 `### Summary` / `### Files Changed` / 逐角色 DTO：约束诚实，不约束骨架。machine 结构只留协议真需处。工具名、参数名、wire 字段、enum literals、路径、命令等 protocol identifiers 永不翻译。

**ProviderLanguage：session 创建绑定 EN|zh-CN vs 运行中切语言 / 译 protocol id。**  
拒中途改语言：破坏已 seal 前缀与 replay。全局偏好只影响未来 session；child/attached/internal 继承 owner。翻译改世界的语言，不改机械的标识符。WorkRecord headings 与 prose 可本地化；tool names / argument names / wire fields 不可。

**生命周期文本：orient-only vs 教育 Host 实现 / Manager Activation phase。**  
拒把 generic Activation 写成 Manager Planning/Working stage 或触发 system prompt 替换。六种生命周期文本（Activation/Reawakening/Continuation/Handoff/Fission/Departure）只 orient，不 educate，不叠第二套 envelope。

**Tool description：tooltip vs 调用合同（PROMPT-020）。**  
拒一句正向描述。调用方看不见被调用方 Role Law；`inspect` 若只说 "Ask an Inspector to establish a repository fact"，Coder 会把修复写进 charge。选 positive + negative affordance + 返回后果 + 参数语义。`calling` 是 authority 选择，不是裸 enum。

**关键区别呈现：单点 vs 每个决策面（PROMPT-021）。**  
拒 DRY 掉调用方合同。Single semantic ownership ≠ single presentation。机器已知的 office ontology 必须完整成为 participant 能够据以行动的世界知识。

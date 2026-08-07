# Prompt Authority — 理由

物理 `role=user` 廉价且可伪造。若不把 Authority 收成 typed 来源，continuation 与 repair 会自我抬升为 HumanRoot，Fallback/Review 预算被反复重置。

Dispatcher 四阶段切开「我打算发」与「Host 真留下了 `msg_*`」：`accepted-*` 只是 transport 收据。未决恢复选 at-most-once 而非重发，因为 Host 可能已开始 provider run——重发制造第二次逻辑效果，比挂起更糟。

`AttemptExecutionProfile` 必须原子：从 session cache 拼装会让 Role、工具面、probe 选择在同一请求内不一致，后续 seal/review 全部失真。

## 备选与被拒

**身份类型与错误表达：领域身份 vs 诊断详情。** `SessionId`、`LogicalRunId`、`ToolCallId` 等身份用独立包装类型，防止跨域误传。当前 Host 发送边界只提供不透明错误详情，因此 `PromptAbandonReason` 用有限 case 区分 `SendFailed` 与 `UnresolvedAfterRecovery`，内部字符串只供诊断，禁止据其散文分叉。不得在文档中虚构实现并不存在的 `DispatchError`；若 Host 边界未来能闭合分类，应原子修改类型、发送端与证明。

**独立拼装的子记录 vs 原子 profile + 窄投影。** 拒绝分别构造 `AuthorityProfile`、`RequestProfile`、`ProjectionContext` 后再拼回请求：多写入口会让同一 attempt 的 Authority、Agent、工具面与 probe 互相矛盾。选择由唯一 builder 构造 `AttemptExecutionProfile`，其中嵌套稳定的 `AuthorityExecutionProfile`；跨边界只传所需字段或从完整 profile 做纯投影，不建立第二构造来源。

**恢复语义：exactly-once / 重发 / at-most-one。** 拒 exactly-once：Host 已可能开始 provider run，无法单边保证物理一次性。拒重发：重发在 Host 留下的 `msg_*` 之外产生第二次逻辑效果。选 at-most-once + fail-closed unknown（PROMPT-011）：未证明落地就保持 Pending，不自动补投。

**PromptKey：内容 digest vs 时间/随机窗口身份。** 拒时间窗口：跨崩溃不可靠，且无法区分「同一 Guard 连发两次」。`ClaimSequence` 由 journal fold 派生的单调序号，使同 payload 重发成为两个 key。

**恢复预算与窗口取值。** PROMPT-011 选择有限尾部窗口，以 PromptKey 证据判定物理落地而不依赖容量估算；有限启动预算避免未知结局永久挂起，同时不伪装成功或盲目重发。数值只在 PROMPT-011 定义。

**载体：metadata vs body 标签。** 拒 body：body 是 provider-visible prompt 的一部分，放恢复键会改变对话字节。放 metadata：不进入模型输入，恢复时按 key 检索（PROMPT-011）。

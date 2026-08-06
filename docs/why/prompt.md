# Prompt Authority — 理由

物理 `role=user` 廉价且可伪造。若不把 Authority 收成 typed 来源，continuation 与 repair 会自我抬升为 HumanRoot，Fallback/Review 预算被反复重置。

Dispatcher 四阶段切开「我打算发」与「Host 真留下了 `msg_*`」：`accepted-*` 只是 transport 收据。未决恢复选 at-most-once 而非重发，因为 Host 可能已开始 provider run——重发制造第二次逻辑效果，比挂起更糟。

`AttemptExecutionProfile` 必须原子：从 session cache 拼装会让 Role、工具面、probe 选择在同一请求内不一致，后续 seal/review 全部失真。

## 备选与被拒

**恢复语义：exactly-once / 重发 / at-most-one。** 拒 exactly-once：Host 已可能开始 provider run，无法单边保证物理一次性。拒重发：重发在 Host 留下的 `msg_*` 之外产生第二次逻辑效果。选 at-most-once + fail-closed unknown（PROMPT-011）：未证明落地就保持 Pending，不自动补投。

**PromptKey：内容 digest vs 时间/随机窗口身份。** 拒时间窗口：跨崩溃不可靠，且无法区分「同一 Guard 连发两次」。`ClaimSequence` 由 journal fold 派生的单调序号，使同 payload 重发成为两个 key。

**恢复预算与窗口取值。** `RecoveryTailWindow=50`：读取目标 Session 尾部足够判定 PromptKey 是否已物理落地，50 远大于一次 Logical Run 内同 key 可能重复的次数，静态常数不随容量估算（CTX-001）。`RecoveryAttemptBudget=3`：跨 3 次插件启动仍无法证明 → `Abandoned`；有限抑制永挂起，不 pretend 成功。

**载体：metadata vs body 标签。** 拒 body：body 是 provider-visible prompt 的一部分，放恢复键会改变对话字节。放 metadata：不进入模型输入，恢复时按 key 检索（PROMPT-011）。


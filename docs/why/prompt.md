# Prompt Authority — 理由

物理 `role=user` 廉价且可伪造。若不把 Authority 收成 typed 来源，continuation 与 repair 会自我抬升为 HumanRoot，Fallback/Review 预算被反复重置。

Dispatcher 四阶段切开「我打算发」与「Host 真留下了 `msg_*`」：`accepted-*` 只是 transport 收据。未决恢复选 at-most-once 而非重发，因为 Host 可能已开始 provider run——重发制造第二次逻辑效果，比挂起更糟。

`AttemptExecutionProfile` 必须原子：从 session cache 拼装会让 Role、工具面、probe 选择在同一请求内不一致，后续 seal/review 全部失真。

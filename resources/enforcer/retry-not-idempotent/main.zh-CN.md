# retry-not-idempotent — Main

让 logical identity 穿过 transport repetition。

在第一次 side effect **之前**分配 idempotency identity，同一次 intention 的所有 retry 都复用它。Receiver——或者离真正 side effect 最近、能够阻止 duplicate commit 的边界——必须靠这个 identity 把多个 attempt 折成一个 effect。

核心不变量：

> **同一个 logical intention 的任意多个 physical attempt，最多只能留下一个 business effect。**

这通常不是 client retry wrapper 自己能保证的。如果 remote system 可能同时收到两次 request，dedupe 必须发生在 effect commit owner，或一个能真正挡住第二次 commit 的 protocol layer。

健康做法包括：

- provider-supported idempotency key，并在 first attempt 前 durable；
- natural business key + create-if-absent；
- command ID 与 effect 在同一 atomic commit 中记录；
- inbox/dedup table 与 effect 共用 transaction boundary；
- replay 返回原 committed outcome 或明确 `already applied`。

如果 provider 根本不支持幂等，而 first attempt outcome 已 unknown，诚实修法可能是**禁止自动 retry**。返回 `UnknownOutcome`，读取 authoritative state 做 reconcile，只有新的显式 intention 才允许再制造 effect。

千万不要在 retry loop 里每次生成新 ID。那是在主动告诉 receiver：“这是另一件新业务”，与 retry 的语义完全相反。

常见假修复：

- “duplicate 概率很低”；
- “只 retry 一次”；
- 用短时间窗口或 payload similarity 去重；
- effect 已经发出后，只在 log/database 里清 duplicate；
- idempotency key 在 retry loop 内分配；
- process-local memory dedupe，但 restart 会 replay command；
- 先做 effect，再单独写 dedupe record，留下 crash window；
- 看到 HTTP `PUT` 就自动相信 idempotent，而 server 实际实现是 additive semantics。

验证必须包含 acknowledgement loss：

1. deliver request K；
2. side effect commit；
3. 丢掉 response；
4. retry K；
5. 断言最终只有一个 logical effect，第二次 attempt 解析为同一 outcome。

还要测 concurrent duplicate delivery、effect 后 restart、以及 payload 完全相同但 identity 新的 K2。K2 必须还能产生第二个 effect；dedupe 应按 identity，不是按“内容看起来像”。

如果 dedupe record 与 effect 分开存，必须注入两者之间的 failure。若会造成 duplicate effect 或永久 false suppression，atomicity boundary 就不对。

完成时 transport 可以在 policy 内自由 retry，一次还是十次都不会改变 business multiplicity。Domain 只看见一个 intention。

> Idempotency 不是“跑两次通常没事”，而是 protocol-level 证明：重复不会改变业务含义。
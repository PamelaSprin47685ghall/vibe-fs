# retry-not-idempotent — Lesson

## 评价

### 定义

Retry 真正危险，不是因为“调用了两次”，而是同一个 logical intention 被允许产生多个 physical attempt，却没有 stable identity 告诉 effect owner：**这些 attempt 其实是一件事。**

所以缺陷不是 “POST 不好” 或 “retry 不好”，而是**重复之间丢了身份**。

Network 会丢 acknowledgement；process 可能 effect 已完成但 reply 前 crash；client 可能 timeout 时 server 仍在 commit。一旦这些情况可能发生，retry loop 就必须回答一个业务问题：

> 这是同一次 operation 的另一个 attempt，还是一个全新的 operation？

Receiver 如果分不出来，transport uncertainty 就会泄漏进 business history，变成 duplicate charge、duplicate publication、duplicate prompt、duplicate resource、duplicate journal fact、重复 external side effect。

### 支配原则

正确设计要在**第一次 effect 之前**分配 logical identity，所有 retry 都复用它，并在真正拥有 side effect 的边界 dedupe。只在 client memory 里 dedupe 没意义，如果两次 request 都已经可能到 remote system。

### 何时触发

以下情形触发：

- effectful call 在 timeout/connection loss/5xx 后自动 retry，而重复执行能再产生 durable effect；
- 每次 retry 都生成新 request ID，虽然 business intention 没变；
- 所谓 dedupe 只发生在 log/metric，duplicate effect 早已逃出；
- 因为没收到 response 就假设“第一次大概失败了”；
- workflow crash/replay command，但 receiver 无法认出它是同一个 logical command；
- API 明明有 natural business identity，retry path 却不传。

### 不应触发

不要因为有 retry 就触发。Pure read、真正 idempotent 的 PUT/set-by-key、monotonic set membership、或者稳定 idempotency key 都可能完全安全。

还要区分**intent idempotency**与“response bytes 必须一样”。Replay 可以返回原 committed outcome、同一 committed object 的更新 representation、或者 `already applied`。关键是不管 transport attempt 多少次，business history 只有一个 logical effect。

### 与相邻规则区分

邻近规则：

- `optimistic-retry-assumption`：上一次 outcome unknown，代码却擅自当成 failed；
- `partial-write-assumption`：effect 被打断，却假设一定 all-or-nothing；
- `lost-update`：不同 intent 通过 stale replacement 相撞；
- `repeat-until-pass`：verification 抽样直到 green，不是 effect retry safety。

### 判定程序

只有当中心问题是这句时才用本规则：**同一个 intent 因 physical retry 没有 stable identity，可能被执行两次。**

决定性实验：给同一个 logical request identity 投递两次，并包含“第一次 effect 已 commit、但 acknowledgement 被丢掉”的情况。最后 business history 有几个 effect？

如果答案是“运气好一个，运气差两个”，就不具备 retry safety。

### 例子

workflow crash/replay command，但 receiver 无法认出它是同一个 logical command。每次 retry 都生成新 request ID，虽然 business intention 没变。

## 建议

### 现在该做什么

让 logical identity 穿过 transport repetition。

在第一次 side effect **之前**分配 idempotency identity，同一次 intention 的所有 retry 都复用它。Receiver——或者离真正 side effect 最近、能够阻止 duplicate commit 的边界——必须靠这个 identity 把多个 attempt 折成一个 effect。

核心不变量：

> **同一个 logical intention 的任意多个 physical attempt，最多只能留下一个 business effect。**

这通常不是 client retry wrapper 自己能保证的。如果 remote system 可能同时收到两次 request，dedupe 必须发生在 effect commit owner，或一个能真正挡住第二次 commit 的 protocol layer。

### 为什么重要

健康做法包括：

- provider-supported idempotency key，并在 first attempt 前 durable；
- natural business key + create-if-absent；
- command ID 与 effect 在同一 atomic commit 中记录；
- inbox/dedup table 与 effect 共用 transaction boundary；
- replay 返回原 committed outcome 或明确 `already applied`。

如果 provider 根本不支持幂等，而 first attempt outcome 已 unknown，诚实修法可能是**禁止自动 retry**。返回 `UnknownOutcome`，读取 authoritative state 做 reconcile，只有新的显式 intention 才允许再制造 effect。

千万不要在 retry loop 里每次生成新 ID。那是在主动告诉 receiver：“这是另一件新业务”，与 retry 的语义完全相反。

### 修复策略

如果 dedupe record 与 effect 分开存，必须注入两者之间的 failure。若会造成 duplicate effect 或永久 false suppression，atomicity boundary 就不对。

### 决策分支

还要测 concurrent duplicate delivery、effect 后 restart、以及 payload 完全相同但 identity 新的 K2。K2 必须还能产生第二个 effect；dedupe 应按 identity，不是按“内容看起来像”。

### 常见假修复

常见假修复：

- “duplicate 概率很低”；
- “只 retry 一次”；
- 用短时间窗口或 payload similarity 去重；
- effect 已经发出后，只在 log/database 里清 duplicate；
- idempotency key 在 retry loop 内分配；
- process-local memory dedupe，但 restart 会 replay command；
- 先做 effect，再单独写 dedupe record，留下 crash window；
- 看到 HTTP `PUT` 就自动相信 idempotent，而 server 实际实现是 additive semantics。

### 验证

验证必须包含 acknowledgement loss：

1. deliver request K；
2. side effect commit；
3. 丢掉 response；
4. retry K；
5. 断言最终只有一个 logical effect，第二次 attempt 解析为同一 outcome。

### 完成条件

完成时 transport 可以在 policy 内自由 retry，一次还是十次都不会改变 business multiplicity。Domain 只看见一个 intention。

> Idempotency 不是“跑两次通常没事”，而是 protocol-level 证明：重复不会改变业务含义。

### Nudge

> Retry 复制的是 transport attempt；正确 protocol 必须把这些 attempt 折回同一个 business intention。

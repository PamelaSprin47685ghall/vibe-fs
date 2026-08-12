# retry-not-idempotent — Enforcer

Retry 真正危险，不是因为“调用了两次”，而是同一个 logical intention 被允许产生多个 physical attempt，却没有 stable identity 告诉 effect owner：**这些 attempt 其实是一件事。**

所以缺陷不是 “POST 不好” 或 “retry 不好”，而是**重复之间丢了身份**。

Network 会丢 acknowledgement；process 可能 effect 已完成但 reply 前 crash；client 可能 timeout 时 server 仍在 commit。一旦这些情况可能发生，retry loop 就必须回答一个业务问题：

> 这是同一次 operation 的另一个 attempt，还是一个全新的 operation？

Receiver 如果分不出来，transport uncertainty 就会泄漏进 business history，变成 duplicate charge、duplicate publication、duplicate prompt、duplicate resource、duplicate journal fact、重复 external side effect。

以下情形触发：

- effectful call 在 timeout/connection loss/5xx 后自动 retry，而重复执行能再产生 durable effect；
- 每次 retry 都生成新 request ID，虽然 business intention 没变；
- 所谓 dedupe 只发生在 log/metric，duplicate effect 早已逃出；
- 因为没收到 response 就假设“第一次大概失败了”；
- workflow crash/replay command，但 receiver 无法认出它是同一个 logical command；
- API 明明有 natural business identity，retry path 却不传。

不要因为有 retry 就触发。Pure read、真正 idempotent 的 PUT/set-by-key、monotonic set membership、或者稳定 idempotency key 都可能完全安全。

还要区分**intent idempotency**与“response bytes 必须一样”。Replay 可以返回原 committed outcome、同一 committed object 的更新 representation、或者 `already applied`。关键是不管 transport attempt 多少次，business history 只有一个 logical effect。

邻近规则：

- `optimistic-retry-assumption`：上一次 outcome unknown，代码却擅自当成 failed；
- `partial-write-assumption`：effect 被打断，却假设一定 all-or-nothing；
- `lost-update`：不同 intent 通过 stale replacement 相撞；
- `repeat-until-pass`：verification 抽样直到 green，不是 effect retry safety。

只有当中心问题是这句时才用本规则：**同一个 intent 因 physical retry 没有 stable identity，可能被执行两次。**

决定性实验：给同一个 logical request identity 投递两次，并包含“第一次 effect 已 commit、但 acknowledgement 被丢掉”的情况。最后 business history 有几个 effect？

如果答案是“运气好一个，运气差两个”，就不具备 retry safety。

正确设计要在**第一次 effect 之前**分配 logical identity，所有 retry 都复用它，并在真正拥有 side effect 的边界 dedupe。只在 client memory 里 dedupe 没意义，如果两次 request 都已经可能到 remote system。

> Retry 复制的是 transport attempt；正确 protocol 必须把这些 attempt 折回同一个 business intention。
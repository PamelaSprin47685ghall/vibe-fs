# command-event-confusion — Enforcer

Command/event confusion 是 epistemic category error：把“**请尝试让 X 成真**”与“**X 已经发生**”装进同一种 record，结果 intention 获得了不该有的 certainty，history 又变成可以被今天 policy 重新否决的意见。

Command 与 event 的地位正好相反：

- command 属于现在，可以因为 authorization、state、validation、capacity 而被拒绝；
- event 属于已经发生的过去，replay 的任务是承认并重建它，不是重新问“今天还允许吗”。

以下情形触发：

- `PlaceOrder` 在 validation 前就作为 “event” append；
- event replay 再跑 current authorization/business rule，决定旧 event 是否仍算合法；
- 同一个 message shape 靠 `isValidated/isApplied` flag 同时扮 command 与 event；
- consumer 收到 durable history 后还可以 veto 过去事实；
- policy 改变后，同一 event stream replay 出不同 history；
- 为省 type，把 request payload 原样当 occurrence 保存，导致缺少真正 committed outcome facts。

不要误杀 durable command/inbox。一个 request 可以被 durable 持久化，只要它**明确仍是 intent**，有 Pending/Accepted/Rejected 等自己的 lifecycle，并与最终 `OrderPlaced`/`PaymentCaptured` 这类 occurrence 分开。

Read projection 因版本兼容忽略 unknown event，也不等于重新授权历史；关键是 source event 仍保持 fact 身份，没有被 today policy 改成“其实没发生”。

与 `overwrite-history` 区分：那里直接改写 past record；本规则即使 bytes 不改，只要 replay 重新把 event 当 command 审批，也会让 past 的意义随今天 policy 漂移。与 `guessed-migration` 区分：后者是对旧数据意义猜测；这里从一开始就混错了 intention/fact 类型。

判定问题很锋利：**系统现在是否还可以合法回答“no”？** 可以，说明它仍 command-like。**它是否已经发生、replay 必须承认？** 是，说明它 event-like。一个 record 不应同时回答 yes。

> Validate intention in the present; record occurrence for the future. 请示可以被拒绝，历史不能每次重播都重新请示。
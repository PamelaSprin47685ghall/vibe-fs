# mixed-side-effect-boundaries — Main

先拆 policy，再拆 effects。

把“应该发生什么”抽成可根据显式 facts 决定的 core；再给不同 external world 各自一个窄 adapter/port，让每个 boundary 自己拥有 failure、lifetime、retry、serialization semantics。最后由 thin shell 按明确顺序执行已经决定的 commands。

典型形状：

```text
facts → policy → [Persist X, Publish Y, Run Z]
                    ↓       ↓       ↓
                  store    http    process
```

Shell 可以知道执行顺序，但不应把 SDK-specific failure 重新变成散落的 business rule。Adapter 应返回 typed outcome，让 orchestration 能明确决定下一步。

常见假修复：

- 造一个万能 `InfrastructureService`，把 DB/HTTP/Git/FS 全藏进同一 interface；
- 只把调用搬到 helper，原 policy body 仍需要知道每种 effect 的低层 error/status；
- 所有 effect error 统一 catch 成字符串 “operation failed”；
- 把 thin shell 再拆十层，制造 forwarding ceremony；
- 为了 pure core，command 类型直接泄漏 SDK request object；
- transaction 与 irreversible external effect 混在一个“看起来 atomic”的函数里，却没有真正 atomicity/reconciliation protocol。

验证应分层：core policy 不需要 external resources 就能跑；每个 adapter 可独立 contract-test；orchestration test 能看见 effect ordering 与 failure branch，但不需要复制 adapter 内部实现。

特别注意跨 effect atomicity。DB commit + external email/payment/process 不可能仅靠一段 try/catch 变成一笔 transaction。需要 outbox/idempotency/reconciliation 时，把这些 protocol 明确建模，而不是让 mixed imperative body 假装“要么全成功要么全失败”。

如果一个 shell 只负责 sequencing、没有 domain branching，就允许它保持薄而直接；不要因为它触及多个 effect 就再造 architecture。RuleBook 的目标是让不同 failure law 可隔离，不是追求每个 function 只 import 一个 module。

完成时，一个 effect SDK/transport 改变只影响对应 adapter；业务 policy 仍按 domain facts 说话；shell 只负责把已经决定的 intent 放进不同世界并处理 typed consequence。

> Effect boundary 的价值，是让数据库的失败像数据库、网络的失败像网络，而业务规则不用假装它们都是同一种异常。
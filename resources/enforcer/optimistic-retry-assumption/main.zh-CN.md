# optimistic-retry-assumption — Main

把 `UnknownOutcome` 当成正式状态。

不要让 timeout/disconnect/crash path 与 known pre-effect failure 落进同一个 branch。Type/control flow 必须把两者分开，在发出第二个 externally visible effect 之前，先通过 recovery protocol 解决原 attempt。

Recovery 顺序应当是：

1. 保留 original logical operation identity；
2. 向 authoritative source 查询这个 identity 是否 commit；
3. 已 commit → 恢复/使用原 outcome；
4. 明确未 commit → policy 允许时 retry；
5. 仍 unknown → 只有在“same identity 重复不会制造第二个 effect”的 protocol 下才 retry，否则保持 unknown，后续 reconcile/escalate。

可用机制包括 provider idempotency lookup、transaction status、business-key query、durable command inbox、authoritative external state、domain-specific reconciliation。

如果 provider 没有任何 attempt identity / status query，而 duplicate effect 又不可接受，那么 automatic retry 就不安全。这个限制应成为 operation contract，而不是藏在 “best effort” 里。

常见假修复：

- exponential backoff + fresh request identity；
- “只 retry 一次，duplicate 概率很低”；
- timeout 比 provider normal latency 短，就推断 request 肯定没到；
- restart 后只检查 local state，却不看 uncertain remote effect；
- first attempt 是否发生都不知道，就先 compensation 再 retry；
- reconcile remote 前就把 local command 标 failed；
- `Cancelled` / `Timeout` 全部映射成 `NotExecuted`；
- 查 stale cache，cache 里没有就当 remote 没 commit。

验证必须把 uncertainty 做真：让 remote effect 已 commit，但 acknowledgement 丢失。Recovery 不得制造第二个 logical effect。然后另测 known pre-effect rejection，证明这种情况可以直接 retry，不需要无意义 reconcile。

还要测“无法解决 unknown”的路径：status lookup 自己失败/provider 不可用。系统必须诚实停在 unknown，而不是为了让 state machine terminal 就硬编 success/failure。

Operator/UI 也要诚实。把 unknown 显示成 “Failed” 会诱导用户再点一次，反而制造 backend 正想避免的 duplicate effect。若真实状态是“我们暂时不知道 payment/order/publication 是否已提交”，就应该明确表达这一点。

完成时，每个 retry decision 都必须能拿出证据证明至少一件事：

- previous effect 确定没有 commit；或
- 用同一 logical identity 重复不会产生第二个 business effect。

两者都证明不了，就不要制造确定性。

> Uncertainty recovery 首先是 epistemology 问题，其次才是 retry policy 问题。
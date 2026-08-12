# optimistic-retry-assumption — Enforcer

Optimistic retry assumption 的核心错误，是把“没有收到 acknowledgement”偷偷解释成“effect 没发生”。

真正的 epistemic jump 是：

```text
我不知道 remote 是否 commit
        ↓
所以它大概没 commit
        ↓
当成新 effect 再做一次
```

中间那一步完全是发明出来的知识。

Timeout、disconnect、process death、lost response、tool interruption 经常摧毁的是**我们的 observation**，不是 remote history。Provider 可能在 response 丢失前一微秒刚刚 commit。一旦这种情况可能发生，系统至少有三种状态：

```text
known success
known failure-before-effect
unknown outcome
```

把 `unknown` 压成 failure，就制造 duplicate-history risk。

以下情形触发：

- payment/publication/create/send/write timeout 后，没有证据证明 first attempt 在 effect 前失败，却直接再发；
- interrupted tool/process 因 caller 没看到 completion，就被认定“肯定没改世界”；
- acknowledgement-loss path 上 retry 使用新的 logical identity；
- local state 写着 “not completed”，就重发 external work，却从未 reconcile remote state；
- crash/restart 仅因 success ack 缺失就 replay command；
- comment 写 “timeout 可安全 retry”，却说不出 idempotency/reconciliation protocol。

不要误杀真正 known-pre-effect failure。Local validation reject、协议可证明 request bytes 尚未被接受的 connection failure、transaction 明确 abort、provider 返回 typed pre-effect rejection，都可以直接判失败。Read-only / naturally idempotent / stable dedupe identity 的 operation 也不是本规则。

这条规则与 `retry-not-idempotent` 邻近但不同：

- `optimistic-retry-assumption` 审的是**知识错误**：unknown 被当成 failed；
- `retry-not-idempotent` 审的是**operation property**：same logical intent 的重复 attempt 会产生多个 effect。

两者常常连着发生：timeout 先制造 unknown，non-idempotent retry 再把 unknown 变成 duplicate effect。

`partial-write-assumption` 则是反方向错误：它发明 boundary 没暴露的 partial state；本规则发明“uncertain attempt 已失败”的确定性。

决定性场景就是 acknowledgement loss：

1. remote effect commit；
2. response 被丢；
3. caller timeout；
4. recovery 开始。

Step 4 时真正知道什么？如果唯一 evidence 只是“我没收到 success”，那它对 remote 是否 commit **一无所知**。

正确 protocol 要么根据 original identity 查询/resolve，要么在同一 idempotency identity 下安全 retry，要么保留 `UnknownOutcome` 并拒绝自动制造第二个 effect。

Backoff 解决不了。等更久只改变 load 与 retry timing，不会改变 first effect 是否已经发生。

> Silence 不是 negative acknowledgement。Unknown 是真实状态，直到 evidence 把它解决之前，都应该保持 unknown。
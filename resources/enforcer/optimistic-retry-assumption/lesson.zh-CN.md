# optimistic-retry-assumption — Lesson

## 评价

### 定义

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

### 支配原则

把 `unknown` 压成 failure，就制造 duplicate-history risk。

### 何时触发

以下情形触发：

- payment/publication/create/send/write timeout 后，没有证据证明 first attempt 在 effect 前失败，却直接再发；
- interrupted tool/process 因 caller 没看到 completion，就被认定“肯定没改世界”；
- acknowledgement-loss path 上 retry 使用新的 logical identity；
- local state 写着 “not completed”，就重发 external work，却从未 reconcile remote state；
- crash/restart 仅因 success ack 缺失就 replay command；
- comment 写 “timeout 可安全 retry”，却说不出 idempotency/reconciliation protocol。

### 不应触发

不要误杀真正 known-pre-effect failure。Local validation reject、协议可证明 request bytes 尚未被接受的 connection failure、transaction 明确 abort、provider 返回 typed pre-effect rejection，都可以直接判失败。Read-only / naturally idempotent / stable dedupe identity 的 operation 也不是本规则。

### 与相邻规则区分

这条规则与 `retry-not-idempotent` 邻近但不同：

- `optimistic-retry-assumption` 审的是**知识错误**：unknown 被当成 failed；
- `retry-not-idempotent` 审的是**operation property**：same logical intent 的重复 attempt 会产生多个 effect。

两者常常连着发生：timeout 先制造 unknown，non-idempotent retry 再把 unknown 变成 duplicate effect。

`partial-write-assumption` 则是反方向错误：它发明 boundary 没暴露的 partial state；本规则发明“uncertain attempt 已失败”的确定性。

### 判定程序

决定性场景就是 acknowledgement loss：

1. remote effect commit；
2. response 被丢；
3. caller timeout；
4. recovery 开始。

Step 4 时真正知道什么？如果唯一 evidence 只是“我没收到 success”，那它对 remote 是否 commit **一无所知**。

正确 protocol 要么根据 original identity 查询/resolve，要么在同一 idempotency identity 下安全 retry，要么保留 `UnknownOutcome` 并拒绝自动制造第二个 effect。

Backoff 解决不了。等更久只改变 load 与 retry timing，不会改变 first effect 是否已经发生。

### 例子


## 建议

### 现在该做什么

把 `UnknownOutcome` 当成正式状态。

不要让 timeout/disconnect/crash path 与 known pre-effect failure 落进同一个 branch。Type/control flow 必须把两者分开，在发出第二个 externally visible effect 之前，先通过 recovery protocol 解决原 attempt。

### 为什么重要

Recovery 顺序应当是：

1. 保留 original logical operation identity；
2. 向 authoritative source 查询这个 identity 是否 commit；
3. 已 commit → 恢复/使用原 outcome；
4. 明确未 commit → policy 允许时 retry；
5. 仍 unknown → 只有在“same identity 重复不会制造第二个 effect”的 protocol 下才 retry，否则保持 unknown，后续 reconcile/escalate。

### 修复策略

可用机制包括 provider idempotency lookup、transaction status、business-key query、durable command inbox、authoritative external state、domain-specific reconciliation。

如果 provider 没有任何 attempt identity / status query，而 duplicate effect 又不可接受，那么 automatic retry 就不安全。这个限制应成为 operation contract，而不是藏在 “best effort” 里。

### 决策分支


### 常见假修复

常见假修复：

- exponential backoff + fresh request identity；
- “只 retry 一次，duplicate 概率很低”；
- timeout 比 provider normal latency 短，就推断 request 肯定没到；
- restart 后只检查 local state，却不看 uncertain remote effect；
- first attempt 是否发生都不知道，就先 compensation 再 retry；
- reconcile remote 前就把 local command 标 failed；
- `Cancelled` / `Timeout` 全部映射成 `NotExecuted`；
- 查 stale cache，cache 里没有就当 remote 没 commit。

### 验证

验证必须把 uncertainty 做真：让 remote effect 已 commit，但 acknowledgement 丢失。Recovery 不得制造第二个 logical effect。然后另测 known pre-effect rejection，证明这种情况可以直接 retry，不需要无意义 reconcile。

还要测“无法解决 unknown”的路径：status lookup 自己失败/provider 不可用。系统必须诚实停在 unknown，而不是为了让 state machine terminal 就硬编 success/failure。

Operator/UI 也要诚实。把 unknown 显示成 “Failed” 会诱导用户再点一次，反而制造 backend 正想避免的 duplicate effect。若真实状态是“我们暂时不知道 payment/order/publication 是否已提交”，就应该明确表达这一点。

### 完成条件

完成时，每个 retry decision 都必须能拿出证据证明至少一件事：

- previous effect 确定没有 commit；或
- 用同一 logical identity 重复不会产生第二个 business effect。

两者都证明不了，就不要制造确定性。

> Uncertainty recovery 首先是 epistemology 问题，其次才是 retry policy 问题。

### Nudge

> Silence 不是 negative acknowledgement。Unknown 是真实状态，直到 evidence 把它解决之前，都应该保持 unknown。

# dispatch-protocol

> 一句话 WHY：**已获授权的 interaction 穿过不可靠 Host 时，transport receipt 或 uncertain
> outcome 不能许可重复发送同一逻辑动作。**

```text
已经决定发生后，如何穿过 unreliable Host 而不复制逻辑效果？
```

本包回答「发送」：logical prompt dispatch 的 durable claim、Claim/Submitted/PhysicalAccepted 分型、
PromptKey 幂等身份、unknown outcome 不自动重发、fire-and-forget 只改等待。
核心 guarantee = **at-most-one logical effect**，不虚构 exactly-once physical delivery。

```text
这个逻辑 interaction 有资格发生吗？
```

那是 [`interaction-authority`](../interaction-authority/README.md) 的问题。两包按 HANDOFF §7.4 硬拆。

## 阅读顺序

1. [`WHY.md`](WHY.md) —— 为什么必须独立存在、历史上 RED 长什么样。
2. [`WHAT.md`](WHAT.md) —— 唯一 normative 合同：编号命题 `DISPATCH-PROTOCOL-0NN`。
3. [`HOW.md`](HOW.md) —— 实现模型：Dispatcher / PromptKey / PromptRecovery 的当前形态；历史与弃权。
4. [`PROOF.md`](PROOF.md) —— 每条命题 → 测试落点；`authority.test.mjs` SPLIT 计划的 dispatch 半边。
5. `tests/` —— 本包拥有的可执行 proof（MOVE 自 fire-and-forget + 2 个 NEW 文件）。

## 概览

| 层 | 内容 |
|---|---|
| WHY | 收据/unknown 不得变成重发许可；一次 logical send 一次 logical effect |
| WHAT | `DISPATCH-PROTOCOL-001..011`：唯一写入口、四态、receipt≠身份、PromptKey、at-most-one、Detached |
| HOW | `Application/Prompting/{PromptDispatcher,PromptDispatcherSend}.fs`、`Interaction/Authority/PromptFactFold.fs`、`Interaction/Dispatch/Recovery.fs` |
| PROOF | 10 个测试落点（NEW 2 文件 14 断言 + MOVE 1 文件 + REUSE 锚点）；`authority.test.mjs` SPLIT@cutover |
| 依赖 | `interaction-authority`、`effect-accounting`、`host-boundary`、`durable-events` |

## RED 长什么样

一次 logical send 因 receipt 混淆、restart 或 retry 产生重复 logical effect；或无真实物理证据时
被宣称 accepted → 本包 RED。

## 不归我（DOES NOT OWN）

- interaction 是否有 authority
- generic effect-accounting law（Requested/Accepted 分型）
- provider representation、attempt recovery
- `RecoveryTailWindow=50` 精确物理证据窗口（HOW）；restart-count recovery budget 已退役

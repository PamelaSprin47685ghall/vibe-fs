# WHY —— 为什么 dispatch-protocol 必须独立存在

## 一句话

已获授权的 logical interaction 穿过不可靠 Host 时，transport receipt 或 uncertain outcome
不能许可重复发送同一逻辑动作（boundary card WHY）。

## 不可替代性：为什么别的包解释不了

`interaction-authority` 回答「这条消息有资格开/续 logical interaction 吗」，它在**发送之前**完成；
一旦决定发送，穿过 Host 的可靠性问题交给本包。把 claim 生命周期塞进 authority，每次重写发送
机制都会顺带改变「谁能当 root」——两个问题可独立变化（INDEPENDENT CHANGE：Host 原生提供可靠
idempotency key 时，可整体替换当前 send HOW，authority WHAT 不动）。

`effect-accounting` 回答「副作用 Requested/物理发生/Accepted 如何分型」——是**通用**副作用记账律；
本包只拥有 prompt 特有的「一次 logical send 不得产生两次 logical effect」防护（boundary card
DOES NOT OWN：generic effect-accounting law）。`durable-events` 提供事件 substrate；本包消费它，
不拥有它。

## 历史上 RED 长什么样（失败模式考古）

1. **`accepted-*` 被当物理落地**：旧测试曾断言 transport receipt 能携带 authority/证明落地。
   `PROMPT-005` 后禁止：`accepted-*` 只是 Host 调用返回的收据；`isAdmissionShaped` 区分
   admission 与真实 `msg_*`（`docs/shape/prompt.md` 四阶段表）。
2. **崩溃后重发 = 第二次逻辑效果**：Host 可能已接受消息并开始 provider run。恢复协议选
   at-most-once 而非重发（`docs/why/prompt.md` 备选与被拒：拒 exactly-once、拒重发）。
   未证明物理落地就保持 Pending；只有预算耗尽才 Abandoned。
3. **时间窗口/随机身份**：用时间窗口找落地跨崩溃不可靠，且无法区分「同一 Guard 连发两次」。
   选 `ClaimSequence` 单调序号，使同 payload 重发成为两个 key。
4. **fire-and-forget 旁路**：独立 `postPromptFireAndForget` 会绕过 claim/持久化/幂等/错误记录。
   统一为 `AwaitMode.Detached`——只改调用方等待，不改发送链（PROMPT-007）。
5. **recovery 在插件构造函数里跑**：与 Host 启动抢事件循环，8-way 并发下 reviewer-restart 红。
   改为 post-init 单飞门（`PromptRecovery.RecoveryGate`）——机制归 HOW，语义不变。

## 独立变化测试（INDEPENDENT CHANGE）

Host 原生提供可靠 idempotency key：本包可整体替换当前 send/recovery HOW，而
interaction-authority（谁有资格）与 effect-accounting（分型律）WHAT 不动。

## 边界（DOES NOT OWN）

| 看似邻近的事实 | 真正 owner |
|---|---|
| interaction 是否有 authority（Root/Continuation/UnknownOrigin） | `interaction-authority` |
| generic effect-accounting law（Requested/Accepted 分型） | `effect-accounting` |
| provider representation、attempt recovery | `provider-projection` / `provider-attempt-recovery` |
| `RecoveryTailWindow=50` / `RecoveryAttemptBudget=3` 精确常数 | HOW（WHAT 只要求 bounded + no blind resend） |

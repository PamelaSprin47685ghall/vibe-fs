# provider-attempt-recovery — 存在理由

## 一句话 WHY

单次 provider attempt 已确认失败后，系统必须能在**不重新选择 Authority、不改变 participant
身份**的前提下有界地换物理执行绑定继续，同时防止无限自动消耗资源。

## 为什么这个 WHY 不可替代

「换执行者继续」与「崩溃后恢复现场」是两种完全不同的故障（见 `crash-reconciliation` 的 WHY）：
attempt 失败是**业务层面已经确认的失败**——Host snapshot 证明了这一次物理请求没有成功；崩溃是
**进程丢失了临时状态**——需要从 durable facts 重建。把两者合并成一个 recovery 概念，会让
「失败计数」与「恢复预算」互相污染，也会让 crash 后的临时内存冒充恢复权威。

本包只回答一个问题：**一次已确认的 provider attempt 失败之后，下一步怎么走，走几次，走到哪停。**

## 世界什么时候 RED

- provider 失败后系统重选了 Authority、换了 Persona、改了 system prompt 或换了语言（换人/换世界语）；
- 同一次失败被两个观察者重复记账，预算被错误消耗（一次失败花掉两次预算）；
- 预算耗尽后系统仍自动发出新的物理请求（无限烧钱）；
- Host 的传输层 Attempt 序号被当成领域连续失败计数（重启时错误清零或错误耗尽预算）；
- 成功停在奇数 Offset 后被误判为 armed，每轮都压缩历史（把历史碾到预算地板）；
- 恢复预算、Offset、SideA/B 等机器代数泄漏进 provider horizon（participant 被迫解码机器状态）。

## 与相邻包的边界

| 看似邻近的事实 | 归属 | 为什么 |
|---|---|---|
| 进程/插件中断后的恢复 | `crash-reconciliation` | 不是「一次失败」，是「临时状态丢失」 |
| 病态重复输出的提前止损 | `degeneration-guard` | 是止损，不是换 binding 继续 |
| cursor 推进只改哪个 agent | `participant-identity`（binding 本体）+ 本包（推进机制） | 换执行者 ≠ 换人；身份字节不变是 identity 的 guarantee |
| 发送 continuation 的 wire 协议 | `dispatch-protocol` / `interaction-authority` | 本包只决定「发不发、发几次」，不拥有 prompt wire 语义 |
| 压缩槽（squash/probe）的产物语义 | `context-compression` | armed 合取是恢复槽门，产物是压缩域 |
| abort 是否等于 terminal | `effect-accounting` | 本包消费「已确认失败」，不定义失败分型 |
| 恢复槽内是否压缩 | `context-compression`（CTX-006..012） | 本包只负责失败→推进→预算→终止 |

# shared-mutable-concurrency — Main

不要先选“更聪明的锁”。先决定谁拥有 mutation。

真正的 repair target 是 semantic invariant，不是 synchronization primitive。找出必须保持 coherent 的状态、哪些 operation 有权改变它、以及**唯一能决定这些 transition 的 authority**。然后让其他并发 participant 通过 command / immutable fact 传递 intent，而不是直接伸手改同一份 state。

一个健康形状通常是：

```text
many concurrent callers
        ↓ command / immutable fact
one mutation owner
        ↓ serialized transition law
owned mutable state
        ↓ immutable observation / event
many concurrent readers
```

这个 owner 可以是 actor、queue consumer、aggregate、workflow state machine、process-local coordinator。名字不重要，关键是只有它执行 mutation；caller 不能因为拿到了 underlying reference 就绕过 owner。

以下情况优先 single writer：

- 多个 field 共同构成一个 invariant；
- transition legality 依赖 current state；
- cancellation / supersession 会改变谁还有资格 commit；
- order 有业务语义，但应由 owner 决定而不是 lock arrival；
- recovery 需要 replay 一条 authoritative history。

如果 concurrent primitive 的原生语义已经完整匹配问题，就直接用它。Atomic monotonic counter 不一定需要 actor；如果 membership 操作彼此独立，concurrent set 可能正合适；一个短生命周期 OS handle 用 mutex 保护，也可能比再造一层 service 简洁。不要为了“ownership 风格”制造新的 ceremony。

常见假修复：

- 一个巨大 global mutex 把整个世界锁起来；
- 每个 field 都换 atomic，但 cross-field invariant 依旧不是 atomic；
- 用“thread-safe facade”包住共享对象，sequencing decision 仍然散在 caller；
- 写一大篇 lock order 文档，却不减少 writer 数量；
- 把共享 mutable state 放进 singleton，然后所有 caller 仍然能直接 mutate；
- 用 database transaction 当万能挡箭牌，但 application-level authority 仍然模糊。

验证重点应是 authority，而不只是 race。尝试从 non-owner path 改状态：API 应让它做不到，或明确拒绝。Permutation scheduler order，确认结果遵循 command semantics。注入 cancellation / stale callback，证明 owner 已转移后旧 participant 再也写不进去。

最终一个 maintainer 应能直接回答：

- 谁能 mutate 这份 state；
- command 怎么到 owner；
- transition legality 在哪里；
- reader 能看到什么；
- ownership 如何结束或转移。

如果答案仍然从“先拿 lock X，除非……”开始，系统还在用 synchronization folklore 替代 domain ownership。

> Concurrency 应该增加 progress，不应该增加同一事实的 authority 数量。
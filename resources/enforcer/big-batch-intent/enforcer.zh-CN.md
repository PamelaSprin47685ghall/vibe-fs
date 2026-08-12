# big-batch-intent — Enforcer

Big-batch intent 的问题，不是“一个 PR 改了很多文件”，而是**多个本可独立判断、独立回滚、独立验证的 semantic change 被绑成一笔 all-or-nothing intention**。

大 batch 最大的风险不是 diff review 累，而是因果归因消失。结果变好/变坏时，你很难知道是哪一项改变造成；rollback 只能整包撤；validation 失败也不知道是哪一个 obligation 未满足。Batch 越大，`guess-based-fix` 越容易伪装成“整体方案终于 green”。

以下情形触发：

- 同一 change 同时重构、换 dependency、改 protocol、改 behavior、清 legacy，却没有真实 atomic reason；
- 多个 speculative fix 一起落，谁真正必要无法区分；
- 需要“一次性做完，否则不好测”只是因为 test/architecture 没有可组合 seam；
- review 只能按文件看，无法按 independent obligation 判断；
- release/rollback 必须接受一堆 unrelated risk 一起进退。

不要误杀真正 atomic migration。Schema + all callers、provider clean-break、cryptographic format cutover、rename crossing generated surfaces，有时必须一笔完成才能保持 invariant。关键是所有 edits 是否由**同一个不可分割 obligation**约束，而不是“顺便一起做更省事”。

与 `wholesale-rewrite` 区分：wholesale rewrite 是以重写整个现有系统为策略；big batch 可以由很多小 edit 组成，只是 semantic intents 被不必要绑在一起。与 `scope-creep` 区分：batch 内工作可能都在 scope，只是本来可以分开证明。

判定问题：删掉其中一项 change，其他项是否仍有独立正确含义、能单独验证和交付？如果 yes，batch 很可能在购买不必要的耦合风险。

> Batch size 真正昂贵的不是行数，而是同时变化的独立假设数量。
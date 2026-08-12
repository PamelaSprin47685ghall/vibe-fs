# in-place-mutation — Enforcer

In-place mutation 的问题，不是“用了 mutable variable”，而是一个**其他组件能够观察、持有、竞争或依赖的 shared identity**被原地改写，使 transition 本身从数据模型中消失。

状态从 A 变 B 其实包含三份信息：A、transition、B。原地把 A 的 field 一个个改成 B，只留下“现在 object 里是什么”，把“原来是什么、如何变到这里”藏进执行瞬间。

这会带来几类真实代价：

- reader 可能看到 field-by-field update 的中间世界；
- 两个 caller 持有同一 reference，一个变化会从另一个视角突然发生；
- concurrency 必须靠 lock/defensive copy 重建 coherence；
- audit/replay 只能从 log 猜 transition；
- test 需要关注 object identity 与 mutation timing；
- rollback 变成“再把字段改回去”，而不是处理一条显式 transition。

以下情形触发：

- shared domain record 被多个 field 逐步改写；
- authoritative object 的 reference 被其他模块长期持有；
- mutation 期间 observer 能看到 intermediate combination；
- correctness 依赖“所有人都知道什么时候这个 object 会被修改”；
- 为了并发安全到处 clone/snapshot，却仍保留同一个 mutable authority；
- transition 只能从 before/after logging 推断，模型里没有显式值/fact。

不要误杀 local mutation。函数内部的 array buffer、accumulator、builder，只要不 escape、没有 semantic identity、最终返回一个纯结果，完全可以是高效 implementation detail。FFI/hardware buffer 原生 mutating 也可以在 wrapper 内存在，只要 domain boundary 仍按 coherent value/transition 交流。

与 `mutable-public-state` 区分：后者重点是 caller **拥有直接 write authority**；本规则即使 write 被藏在 method 里，只要 shared current identity 被 destructive update、transition 对 observer 有语义，仍可触发。`overwrite-history` 则专门保护 committed past，不是 current shared state。

最锋利的问题是：**旧值是否有任何 observer 有资格继续依赖？** 如果有，原地覆盖就会让 old/new/transition 的边界模糊。优先计算完整 next value 或显式 event，在 transition 已完成（以及需要时 durable）后一次性替换 authoritative reference。

> Mutation 可以是局部实现技巧；一旦它能被世界看见，就已经变成状态协议。
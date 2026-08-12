# in-place-mutation — Main

把 shared transition 变成完整 value/fact，再让 authority 一次性前进。

典型修法：

```text
next = transition(current, command)
validate / persist if required
swap authoritative reference to next
publish consequence
```

这里的关键不是“immutable coding style”，而是 observer 只看到 coherent old 或 coherent new，不需要参与 field-by-field construction。

如果 domain 需要保留 causality，就让 transition 产生显式 event/fact；current state 可由 event fold 得出。若只需要 current value，也至少让 next state 作为完整值先构造完，再替换 reference。

Local mutation 继续可以用。你完全可以在 `transition()` 内部用 mutable buffer/array 优化，只要它不逃逸、不被 observer 看见、最终对外仍表现成一条 atomic semantic transition。

常见假修复：

- 先 shallow clone，再把 nested shared object 原地改掉；
- 对每个 field update 发 observer callback，假装这就叫 transactional；
- 到处 defensive copy，但真正 authority 仍是同一 mutable object；
- 给 mutation 加 global lock，却继续让 transition semantics 只存在于 imperative steps；
- “修改前先 log 一份旧值”，用 observability 补偿模型没有 transition；
- 把 object `freeze()` 放在 mutation 之后，但旧 reference 在冻结前早已被其他代码观察。

验证要证明三件事：

1. transition 后，old value/reference 的 observable contents 不再变化；
2. reader 不可能看到 intermediate combination；
3. 同一个 declared input state + command 的 reasoning 不依赖 hidden object identity/timing。

并发场景下，故意让 reader 在 transition 中间被调度。如果实现仍是 shared field mutation，这个测试很容易暴露 torn semantic state；完整 next-value + atomic authority swap 则自然避免。

如果 state 需要 durability，还要与 `memory-before-disk` 对齐：candidate next 可以先算，但 authoritative swap 不得早于 durable fact 的 commitment。

不要为了 immutable purity 制造巨量复制成本。如果结构很大，可以使用 persistent data structure、copy-on-write、内部 mutable builder + immutable publish，或者其他能保证**semantic atomicity**的技术。RuleBook 关心的是 transition 是否被世界完整观察，不是 allocation 数量。

完成时，mutation 若存在，只是一段局部不可见 machinery；shared/domain 世界看到的是清楚的 before、transition、after，而不是一个 object 在眼前逐字段变脸。

> 旧状态不是垃圾，它是 transition 的一半证据。别在别人还可能依赖它时原地擦掉。
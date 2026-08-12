# callback-pyramid — Enforcer

Callback pyramid 的病，不是缩进难看，而是 **sequence、resource lifetime、cancellation、failure propagation 被 lexical nesting 共同编码**，导致 operation 没有一个可以从上到下读完的 owner。

`open → read → parse → write → close` 如果分散在四层 callback，读者要同时追两个维度：代码嵌套在哪，运行时什么时候发生。每加一个 error branch，就要重新回答哪个 closure owns cleanup、哪个 cancellation 仍有效、later callback 会不会在 outer operation 已结束后继续。

以下情形触发：

- 主要 workflow 由多层 callback/`.then` nesting 表达；
- cleanup/error handling 分散在不同 inner closure；
- caller cancel 后某个 nested callback 仍可能继续 effect；
- resource acquisition 在外层，release 只能在多个内层 branch 手工记得；
- parallel branches 通过互相嵌套 callback 汇合，没有明确 join point；
- 读者必须来回跳 closure 才能说清 success/failure/cancel path。

不要误杀 callback API 本身。Foreign library 只有 callback 完全正常；把它在 adapter edge promisify/包装后，内部用 structured async 即可。一个浅 callback、lifetime 一眼可见，也没必要重构。

与 `implicit-control-flow` 区分：那里 sequencing 藏在 registration/framework lifecycle；本规则的 sequencing 其实在 source 中，但被**continuation nesting**打碎。与 `resource-not-scoped` 区分：callback pyramid 常导致 lifetime 不清，但即使资源都最终 release，只要 causal path 仍难以整体理解，pyramid 仍存在。

诊断方法：先用几行普通句子写 operation causal sequence。如果无法把 source 从上到下映射到这条 sequence，而必须不断“进入这个 callback，再回来，再看另一个 closure”，控制流 representation 已经妨碍推理。

> Nested callback 把时间变成代码拓扑。结构化 async 的价值，是重新让因果、失败、取消与资源拥有同一个可见 scope。
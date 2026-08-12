# phase-flag-accumulation — Enforcer

Phase-flag accumulation 的病，是 lifecycle bug 每来一次，就再加一个 boolean/counter 去补丁，最后一组 flag 的组合偷偷变成了没人正式命名的 state machine。

`started / waiting / retrying / cancelled / done / hasLease` 看起来每个都简单；但六个 boolean 理论上已经允许 64 个世界。真实 lifecycle 也许只有五个。剩下几十个 combination 全是 representation 自己制造的虚构状态，之后每个 reader 都要靠条件表达式把它们重新排除。

以下情形触发：

- 新 bug 的标准修法是“再加一个 `isX`”；
- 同一 lifecycle 需要同时检查三四个 flag 才知道当前 phase；
- `started && done && retrying` 这类 contradiction 可被构造；
- transition 不是 `Running → Completed`，而是 scattered assignments：`waiting=false; done=true; retrying=false`；
- recovery 要根据 flag combination 猜 resume semantics；
- test 大量覆盖 bit combination，而不是命名 state/transition。

不要误杀真正独立 predicate。`notifyEmail` 与 `notifySms`、几个独立 capability、feature preference 如果所有组合都有意义，就应该保持独立，不需要硬塞进一个 enum。问题只在多个 flag 共同回答“**我们现在处于同一个 lifecycle 的哪里**”。

与 `boolean-blindness` 区分：那条更广，只要 boolean 抹掉命名 domain choice 就可能触发；本规则专门针对 flags 逐年累积成隐形 automaton。与 `program-counter-state` 区分：后者把 interpreter 下一步直接存进 durable/shared state；phase flags 仍可能描述真实 lifecycle，只是 representation 失控。

判定方法最简单：把所有 meaningful flag combinations 列出来并命名。如果合法组合只占整个 boolean product 的小部分，而且每个组合其实都有 phase 名，直接建模那些 phase 更诚实。

> 如果读者必须在脑中计算几个 boolean 才知道“现在在哪”，state machine 已经存在，只是代码拒绝承认它。
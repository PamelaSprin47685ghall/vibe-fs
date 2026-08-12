# cyclic-dependency — Enforcer

Cyclic dependency 真正的问题，不是图上“有个圈不好看”，而是两个 component 互相要求对方先存在，说明**双方都需要的那个事实/协议没有独立 owner**。

Dependency edge 是知识箭头：A → B 表示 A 的定义部分依赖 B。A ↔ B 就意味着两边都必须先理解对方才能完整定义自己。Runtime 可以用 lazy init、service locator、DI container、callback registry 把 physical cycle 藏起来，但 conceptual cycle 会换个地方出现：初始化顺序、nullable partial state、startup race、whole-graph test fixture、无法单独构造。

以下情形触发：

- package/module/project 出现 compile/import cycle；
- service A 构造时需要 B，B 又需要 A；
- mutual initialization 靠先塞 `None/null` 再回填；
- event callback/registry 只是为了绕 import cycle，却仍然双方拥有彼此 policy；
- interface 抽到第三个 package，但里面装的仍是 A/B 私有概念，cycle 只是文字消失；
- test 一个 component 必须启动整个 graph，因为它没有独立可解释的 boundary。

不要误杀双向业务通信。两个 peer 可以通过一个双方都不拥有的 protocol/bus/contract 来回发送 message，而 compile-time/ownership dependency 仍是 acyclic。Runtime message 往返不是 architectural dependency cycle。

也不要把所有 mutual domain relation 都拆成 mediator。关键是**谁拥有双方共同需要的 invariant**。有时答案是抽出独立 protocol/value；有时是承认 A/B 本来就是一个 aggregate，应合并而不是强行保持假独立。

与 `boundary-collapse` 区分：boundary collapse 可以是单向越权，不必有 cycle；cycle 更具体地说明 knowledge ownership 无法定向。与 `implicit-control-flow` 区分：cycle 常导致 startup order 魔法，但后者关注 happens-before 隐藏。

诊断时问：如果必须删一条 dependency edge，哪一边真正应该知道另一边？或者两边共同依赖的概念其实属于第三个独立 owner？回答不出，通常说明 architecture 还没决定 sovereignty。

> Cycle 不是“箭头画成圆了”；它是在说系统无法回答谁先定义谁，因为共同事实还没有真正主人。
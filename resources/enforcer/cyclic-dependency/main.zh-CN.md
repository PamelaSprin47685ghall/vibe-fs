# cyclic-dependency — Main

修 cycle 时先找 ownership，不要先找“能让 compiler 通过的 indirection”。

把 A ↔ B 拆开问：

- A 真正需要 B 的哪个 fact/capability？
- B 真正需要 A 的哪个 fact/capability？
- 这些 crossing facts 谁应该拥有？
- 两边共同依赖的是不是一个应独立存在的 protocol/value？
- 或者 A/B 其实从来不是两个可独立变化的 owner？

常见正确形状有三种：

1. **提取独立 contract**：A、B 都依赖更稳定的 C，而 C 不依赖任一方实现；
2. **dependency inversion**：policy owner 定义需要的 capability，effect adapter 实现；
3. **合并假边界**：若两边共享同一 invariant/lifecycle，承认它们属于一个 aggregate，而不是继续维护形式独立。

常见假修复：

- lazy import / service locator / global registry 把 compile cycle 变 runtime lookup；
- 接口抽到 `common` package，但 interface 字段仍是 A/B 私有 representation；
- 两边 constructor 先收 nullable reference，startup 后互相 backfill；
- 用 event bus 隐藏 direct call，但 event contract 仍由发送者/接收者私下耦合；
- 为“打破 cycle”造一个 mediator，实际它只是知道 A/B 全部 internals，转成新的 god owner；
- 把一个真实 aggregate 硬拆成两个 package，再靠十个 callback 保持同步。

验证不能只看 `rg import` 无环。尝试独立构造/测试每个 owner：它应该只依赖自己正式声明的 contract，不需要整个 runtime graph 才有意义。Startup order 也应不再靠“先注册 A 再回填 B”。

再做 change test：改变 A 内部 implementation，B 若只依赖稳定 contract 就不应跟着改；反之亦然。

如果抽出 C，确认 C 真的是稳定共同语言，而不是两边 internal types 的垃圾桶。一个能被 A/B 同时依赖的第三 owner，必须比双方 implementation 更基础、更少知道它们，而不是更多。

完成时 dependency graph 有可解释方向：knowledge 从 policy/contract 指向 implementation，而不是双方互相需要完整存在才能被定义。

> 真正打破 cycle 的不是多一层 interface，而是终于决定“这个共同事实到底归谁”。
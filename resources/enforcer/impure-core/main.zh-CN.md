# impure-core — Main

把 observation 推到 shell，把 decision 留在 core。

先列出 policy 真正需要的 facts：current instant、account status、config value、remote eligibility、existing record、random choice。让 adapter/orchestrator 负责观察这些事实，再作为 explicit inputs 交给 deterministic decision function。

典型形状：

```text
shell observes world
      ↓ facts
domain decides
      ↓ commands/events
shell performs effects
```

这不要求所有流程一次把世界读完。有些 decision 天然是多轮：先根据已有 fact 决定需要查询什么，再观察，再继续。关键是每个 decision step 都只根据自己显式拥有的 facts 作答，而不是 deep inside 偷抓 ambient capability。

常见假修复：

- 把 DB/network client 通过 DI 注入 domain class，于是“依赖显式了”，但 policy 仍自己决定何时观察外界；
- 给 core 一个巨大 `Context`，里面塞 clock/env/db/http/fs，签名虽只有一个参数，真实 dependency 仍全隐藏；
- mock 所有 effects 后宣称函数 deterministic；
- 为了 purity 把每个微小 operation 拆成几十个 command/event，反而让 orchestration 比 domain 更难读；
- observation 已经是 explicit fact，却又在 core 重新查询“确认一下”；
- 把 logging/metrics 也一律禁止，混淆 semantic effect 与非决策 observability。

验证要证明相同 explicit inputs 得到相同 decision，不需要真实 DB/network/clock/env。然后单独 contract-test adapters：它们是否正确把 external world 翻译成 core 所需 facts。

Replay 是最强验收之一。只要 recorded facts 足够，过去的 decision 应可以在没有当时 external services 的情况下重演。若重放还必须再次访问今天的 provider/DB，说明昨天的输入并未真正被保存。

如果某个 external observation 本身非常昂贵或 volatile，明确由 shell 决定 observation timing/cache policy；不要让 domain policy 因方便偷走 effect ownership。

完成时 core 的 signature/typed input 能诚实描述决定所依据的世界；shell 可以 impure，但 impurity 有边界、有 owner、有可测试 contract。

> 不是把 effects 从程序赶出去，而是别让 effects 混进“什么应该发生”的判断里，直到判断本身无法被独立理解。
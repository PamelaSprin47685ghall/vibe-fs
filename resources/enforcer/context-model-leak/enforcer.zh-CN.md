# context-model-leak — Enforcer

Context-model leak 的病，是因为两个 context 的 data **长得像**，就让它们共享同一个 master type，结果 shape similarity 被误当 semantic identity。

Authentication 里的 User、Billing 里的 AccountHolder、Session 里的 Participant 可能都有 `id/name/email`，但它们回答的问题、invariant、authority、lifetime 完全不同。一个通用 `User` 一旦横跨所有 context，某个地方为了 billing 加的字段，其他 context 立刻也“看得见”，于是看起来仿佛有资格依赖。

最后 master model 往往长成 optional-field 集合：`billingBalance?`, `authClaims?`, `theme?`, `sessionRole?`。大量 `null` 不是灵活性，而是在告诉你：这些字段在大多数 context 根本没有语义。

以下情形触发：

- auth/billing/UI/session/reporting 共用一个 “User/Context/Request” 巨型 model；
- 加一个 context-local field 导致多个 unrelated package 跟着变；
- caller 频繁判断“这个字段在我这里有没有意义”；
- authorization/lifecycle rule 因共享 model 开始混用 foreign fields；
- persistence row 被一路传进 domain/UI，而不是在 owning context 映射；
- 为避免 split type，不断新增 nullable field/context flag。

不要误杀真正 stable value object。`Money`, `EmailAddress`, opaque `UserId` 如果在各 context 中意义与 invariant 真正一致，可以共享。共享一个小事实，不等于共享整个 context model。

与 `boundary-collapse` 区分：boundary collapse 更广，state/lifecycle/authority 都可能越界；本规则只打**一个 representation 冒充多个 domain concept**。与 `primitive-obsession` 不同：这里不是 string 太弱，而是 type 太“通用”。

判定问题：对每个 context 问“这个 model 必须回答哪些问题？”如果答案明显不同，就不该由同一个 type 假装统一。

> 同样字段不代表同样概念。Model 应服务于一个 context 的问题，而不是服务于数据库里恰好有这些列。
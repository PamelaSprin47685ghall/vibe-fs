# context-model-leak — Main

从每个 bounded context 的问题出发重新建模，不要从 master field list 出发切片。

Auth 需要哪些事实才能判断 identity/permission？Billing 需要哪些事实才能结算？Session 需要哪些事实才能管理 participant？分别建立最小 context-owned type，然后只在 boundary 翻译真正共享的 facts：opaque ID、Money、EmailAddress、明确 event/contract。

Translation 不是重复劳动，而是在声明：跨 context 后，**同一段 bytes 现在被赋予什么不同意义**。

常见假修复：

- 把 universal model 复制三份，字段/semantics 仍手工同步；
- 继续保留 master type，只加 `context=Billing|Auth`；
- 给所有 foreign field 加 nullable，然后用 runtime check 判断“这里有没有”；
- namespace field：`authEmail`, `billingEmail` 全塞同一 object；
- persistence entity 直接当所有 context 的 domain model；
- 为避免 translation，所有 bounded context 直接依赖 shared package 的 mega DTO。

验证应做独立演化实验。给 Billing 加一个只属于 billing 的 invariant/field，Auth/Session 应完全不需要改变。反过来，Auth 的 credential/lifecycle 调整不应污染 Reporting model。

Context 间传递时，只允许 explicit boundary contract。可以共享 ID，但 ID 只是 identity，不携带 foreign context 的整套 state。需要更多事实时，通过 query/event/translation 获取，而不是把整个 master object 偷渡过去。

如果两个 context 经过审视后实际上拥有相同 invariants、相同 lifecycle、总是一起变化，可能说明它们本来就不该是两个 bounded context。不要为了“DDD 形式”制造无意义 split。

完成时每个 model 有一个 semantic owner、一个主要 reason to change；foreign context 看不到对自己无意义的字段，也无法因为“类型里有”就误以为可依赖。

> Context boundary 最有价值的能力之一，就是让某些字段彻底不存在。不存在比 nullable 更能阻止错误知识传播。
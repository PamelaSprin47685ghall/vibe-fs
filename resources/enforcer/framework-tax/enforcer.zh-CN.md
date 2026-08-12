# framework-tax — Enforcer

## 定义
当 framework 的 lifecycle、registration model、configuration vocabulary、extension points、generated artifacts 或 indirection，比它原本要支持的 domain operation 更显眼、更难理解时，framework tax 已经失控。

Framework 不再只是 infrastructure，而开始要求问题先被翻译成它自己的 ontology 才允许存在。

## 支配原则
Framework 的价值由它**删除了多少真正 complexity**决定，不由它“引入了多少看起来很架构化的东西”决定。

有价值的 framework 会吸收困难的 cross-cutting work：transport、scheduling、rendering、persistence driver、protocol compliance、dependency construction、platform integration。Tax 开始病态化，是当一个简单 operation 必须完成一串 ceremony，而这些 ceremony 唯一的 consumer 只是 framework 自己。

“Standard pattern” 从来不是免费的。每个 container registration、decorator、provider、middleware layer、hook adapter、generated binding、config key、abstract base、lifecycle callback，都会成为 behavior 可以躲藏的新地点。

问题不是 framework 是否流行、是否 idiomatic。问题是：这些 machinery 在这个 boundary 到底买到了什么真实 capability？

## 何时触发
当 framework mechanics 主宰 understanding/change cost，却没有保护 independent contract 时触发。常见形式：

- 一个 direct function call 被变成 interface → implementation → provider → container registration → resolver，而没有真实 runtime substitution requirement；
- business control flow 散落在 annotation、middleware、hook、interceptor、decorator、config 中，没有 single semantic owner；
- 增加一个 domain field，要同步修改多份 framework metadata/schema/registration 对同一事实的表示；
- generated scaffolding 被当成 architecture，实际只是镜像 source declaration；
- tiny component 无法单独 test，必须 boot 大 application/container，因为 domain decision 与 framework lifecycle 粘死；
- internal module 直接使用 transport DTO、ORM entity、request context、framework exception、plugin shape 当 domain vocabulary；
- 只有一个 implementation/consumer，却提前造 generic framework abstraction；
- 如果未来换 framework，连 core domain logic 都得一起重写，而不是只换 boundary adapter。

## 不应触发
- Framework machinery 直接 enforce 真实 external protocol、transaction、security、lifecycle 或 isolation boundary。
- Dynamic discovery/substitution 是真实 runtime requirement，并确有多个 independent implementations/deployment contexts。
- Boilerplate 被局部关在 adapter edge，core decision 保持 framework-agnostic。
- Framework feature 删除了大量 bespoke machinery，而且其 semantics 明显比自行实现更简单。
- 项目故意把 framework convention 作为 public integration contract；这时 convention 本身就是 boundary knowledge。

## 与相邻规则区分
`incidental-complexity-dominates` 更广。`framework-tax` 明确指出 accidental burden 的来源是 framework ontology。

`pattern-sprawl` 更多是 hand-built/inherited design patterns 在语言已有更直接能力时仍扩散。`dependency-bloat` 看不必要 package，即使 integration 本身很简单。`facade-hides-mess` 则用漂亮入口盖住内部 tangled structure。

## 判定程序
先完全不用 framework nouns 描述 desired operation。

再列出 path 上每个 framework construct，逐个问：

> 如果用 host language 的直接 construct 或一个窄 adapter 取代它，会丢掉什么 capability？

有效答案包括 transaction scope、host hook contract、runtime plugin discovery、request isolation、protocol decoding。无效答案包括“framework 就要求这样”“以后可能有用”“这样看起来 architecture 比较统一”。

如果大多数 constructs 的存在理由只是互相满足，而不是满足 problem，tax 已经主导 design。

## 例子
- positive：只有一个 repository implementation，却套 interface、provider class、token string、container module、factory、resolver，只因为“所有东西都应该 DI”。
- positive：一个 domain validation rule 分散在 request decorator metadata、middleware、ORM hook、serializer config，没人拥有完整语义。
- positive：core domain function 接收 web framework request object，只因为显式传一个小 context 需要少写几条 annotation。
- near-miss：plugin host 强制一种 hook object；一个 adapter 把它翻成 domain command，系统其他部分不认识 host type。
- counterexample：transaction framework 真正拥有多次 persistence operation 的 commit/rollback semantics，并删除了大量 bespoke failure machinery。

## Nudge
Framework 应当让问题变小。

如果工程师必须先学会 framework 神话，才看得到一个简单 domain action，你正在给工具交长期利息。

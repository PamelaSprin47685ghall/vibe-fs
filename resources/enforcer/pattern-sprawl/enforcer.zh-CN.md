# pattern-sprawl — Enforcer

## 定义
当 design-pattern machinery 变成叠在 host language 之上的第二门编程语言，而 host language 本身已经能更直接表达真正需要的 distinction 时，就是 pattern sprawl。

Factory、visitor、strategy、builder、command class、registry、interface hierarchy、mediator、template-method scaffolding 本身都不是罪。真正 defect 是**没有买到 capability 的 ceremony**：最初支持这些 pattern 的 language/platform limitation 已经不存在，甚至从未存在过，但 objects 与 indirection 仍继续活着。

## 支配原则
Pattern 是对 constraint 的解法，不是可以收藏的 architecture shape。

Pattern 背后往往确实有真实 idea：closed variation、late binding、traversal、construction invariant、effect substitution、protocol dispatch。可如果语言已经有 algebraic data type、pattern matching、first-class function、module、record、iterator、closure、trait/interface、普通 constructor 能直接表达同一 law，继续复制旧 object choreography 反而会让 law 更难看见。

真正问题从来不是“这是不是一个著名 pattern”，而是“这个 pattern 买到了什么 direct language form 没有的 semantic capability”。

## 何时触发
当 pattern machinery 只是模拟语言已有能力，而 indirection 开始主导理解/change cost 时触发。常见形式：

- closed cases 用一堆 subclasses + visitor 表达，而 data + exhaustive match 已可直接说明；
- stateless one-method strategy classes 存在，但 first-class functions 已完整保留同一 contract；
- factory 只在几个 statically known constructors 之间选择，没有 runtime discovery/configuration requirement；
- builder 用几十个 mutable flags 拼一个其实可以 validated immutable construction 的 object；
- command object 只包一层 function call，没有 persistence/queue/serialization/undo semantics；
- mediator/event bus 只为了避免显式 dependency name，就 routing 普通 synchronous calls；
- interface-per-class，每个 interface 永远只有一个 implementation，也没有 independent substitution boundary；
- 为了让 code 看起来 enterprise/clean/hexagonal，提前加 pattern layer，却没有相应 semantic boundary。

## 不应触发
- Runtime plugin discovery、open extension、serialization、distributed dispatch、undo/history、independent substitution 真正要求该 machinery。
- Host language 没有更安全/直接的 representation 能表达需要的 variation。
- Visitor 用于 stable external object hierarchy，operations 需要独立扩展而 hierarchy owner 不能修改。
- Builder 真正 enforce nontrivial staged construction/validation，普通 constructor/type system 难以清楚表达。
- Command object 是 durable message/event，拥有 identity、replay、queue、audit 等 function call 之外的 first-class semantics。
- Pattern 创建了 real capability/authority boundary，而不是只加 indirection。

## 与相邻规则区分
`framework-tax` 来自 framework ontology；`pattern-sprawl` 完全可以 hand-written、零 external dependency。

`premature-unification` 是 semantics 尚未证明 common abstraction 就先统一。`pattern-sprawl` 也可能攻击曾经合理、但随着 language/system 演进已失去必要性的 pattern。

`implicit-control-flow` 常由 mediator/event pattern 过度使用造成；如果更锋利的问题是 execution order 变 invisible，用它。

## 判定程序
对每个 pattern layer，不提 pattern 名字，直接说 semantic job：

- “runtime 选择一个 behavior”；
- “表示这些 closed states 之一”；
- “只构造 valid value”；
- “遍历结构但不修改 owner”；
- “queue/replay 一个 operation”。

然后用 host language 的 direct constructs 在脑中重写一次。会失去哪一种 capability？

如果答案只是“class 让 pattern 看起来更 explicit”“OO 一般这么写”“以后也许加 implementation”，machinery 没有赢得成本。

## 例子
- positive：12 个 AST node subclasses 各自 `accept(visitor)`，整个 hierarchy 完全由 repository 控制；F# DU + match 更 closed/exhaustive/direct。
- positive：三个 classes 实现 `IRetryStrategy.Execute()`，没有 state，每个只调用一个 function，再由 local match 选择。
- positive：`FooFactoryFactory` 构造唯一的 `FooFactory` implementation，只为了“所有东西都经过 abstraction”。
- near-miss：external third parties 会在 runtime register plugins，registry/factory boundary 真正 open。
- near-miss：command objects 会 persist/replay across restart，拥有 durable identity。
- counterexample：closed workflow 用 algebraic states + exhaustive transitions + ordinary functions 表达。

## Nudge
Patterns 是已经解决过的 constraint 留下的化石。

Constraint 仍在就保留。语言已经能直接说出 law 时，把化石扔掉。

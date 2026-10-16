# catch-all-swallows-future — Lesson

## 评价

### 定义

Catch-all swallows future 的病，不是“代码里出现 `_` / `default`”，而是一个 **closed 或受控扩展的 case set** 在今天新增成员时，旧代码无需重新作出 semantic decision，就自动把它塞进昨天的 fallback。

这相当于给未来 ontology 写了一张空白授权书：无论以后增加什么 case，都默认“和目前这些剩余情况一样处理”。Compiler/build 本来有机会把新 case 变成 review obligation，catch-all 却把这份压力吞掉了。

### 支配原则

Exhaustiveness 是一台维护警报。在有限 domain 里，新 case 应该在每个含义可能改变的地方制造 compile-time 或 test-time 压力。Wildcard 通过断言——通常是无意的——所有未知未来在语义上等价，把这份压力静音。这个 branch 不只处理今天的剩余，它给自己授予了对明天 case 的权威。

### 何时触发

以下情形触发：

- closed union/enum 用 `_ -> ignore/default/error`；
- protocol 新 variant 会自动落入 generic fallback，而没人审它是否安全；
- permission/role/tool case 新增后旧 gate 继续 compile，因为 `_` 接住；
- recovery 新 state 自动被 “unknown → continue” 处理；
- test 只证明现在 cases 都能跑，却无法在新增 case 时迫使 policy 更新。

### 不应触发

不要误杀真正 open-world boundary。Vendor extension、unknown JSON field、versioned protocol extension 若 contract 明确规定“未知 case 按 X 处理”，catch-all 就是在执行**正式 unknown-case law**。关键是世界是否真的开放，以及 fallback 是否就是那个开放协议的 stable semantics。

也不要误杀经过前置 typed narrowing 后的 unreachable remainder；如果 type system 已证明 `_` 只覆盖一个被正式等价化的集合，并且新增 case 会迫使 narrowing 更新，就不属于本规则。

### 与相邻规则区分

与 `non-exhaustive-transition` 区分：那条专门审 finite `state × event` relation 的未决 cell；本规则更广，任何 closed vocabulary 都可能被 catch-all 吞掉 future obligation。

### 判定程序

决定性 experiment：临时新增一个 plausible case。若 code 仍 compile/test green，而且没人被迫回答“这个新 case 在这里意味着什么”，fallback 正在替未来作政策决定。

### 例子

- positive：closed domain 上写 `switch (kind) { case A: …; default: ignore }`，明天的 `B` 因此继承 ignore。
- near-miss：versioned protocol 文档写明“未知字段必须保留”，fallback 就是这个 contract。
- counterexample：closed type 上穷尽列举 case，新增 variant 在语义被选定前无法编译。

## 建议

### 现在该做什么

让 closed vocabulary 的新增 case 重新变成显式工作。

Closed union/enum/policy 用 exhaustive matching；每个 case 要么命名处理，要么被一个**明确、已审过的等价 group**覆盖。不要用 generic fallback 把未来 case 自动塞进去。

若 boundary 本来 open-world，就把 unknown semantics 正式命名：`Unknown raw -> preserve`、`Unknown extension -> reject`、`Unknown field -> ignore`。让人一眼看出这不是偷懒 default，而是 protocol law。

### 为什么重要

Wildcard 用当下的便利换未来的盲区。没有它，扩展一个 finite type 会产生一张每个假设都必须重新考虑的点的地图。有了它，编译器保持沉默，旧行为被一个写 branch 时根本没想到的 case 静默继承。

### 修复策略

在合适处把 domain 建模为 closed set、逐 case 列举，并为真正允许未知值的 protocol 保留显式 `Unknown` 或 extension 处理。

### 决策分支

- 如果 domain 是 closed、新 case 需要人的判断，移除 catch-all，强制 exhaustive。
- 如果 protocol 有意 open，把 unknown path 写成显式命名 contract，而不是偶然的 `default`。
- 如果 default 仍存在，它绝不能对从未见过的 future variant 施加 domain 语义。

### 常见假修复

常见假修复：

- `_ -> fail` 就自称安全；未来 case 仍然未经 review 自动获得 failure semantics；
- 在 catch-all 里加 log/warning；
- test 当前 enum count，却新增 case 后 policy 仍不用改；
- 把所有未来 case 映成 `Other`，但 `Other` 没有真正 open-world contract；
- 关闭 compiler exhaustiveness warning；
- 用反射/string switch 绕过 closed type，未来 case 更难被发现。

### 验证

验证方式是 ontology mutation：加一个假 case。Build 或 focused test 必须暴露所有需要 semantic decision 的地方；做完决定后才重新 green。

对 truly open protocol 再做 opposite test：喂一个从未见过的 extension，系统应严格执行 documented unknown law，而不是 crash 或误当已知 case。

### 完成条件

完成时，新 ontology 无法悄悄借用旧 fallback。Closed world 会迫使 review；open world 则有一条明确、稳定的 unknown-case contract。

> Exhaustiveness 的价值不是多写 branch，而是让世界新增一种可能时，旧政策必须重新表态。

### Nudge

> Catch-all 可以处理已知的“其余”，不能替尚未出生的 case 提前签署语义。

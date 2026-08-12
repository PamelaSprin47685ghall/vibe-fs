# catch-all-swallows-future — Enforcer

Catch-all swallows future 的病，不是“代码里出现 `_` / `default`”，而是一个 **closed 或受控扩展的 case set** 在今天新增成员时，旧代码无需重新作出 semantic decision，就自动把它塞进昨天的 fallback。

这相当于给未来 ontology 写了一张空白授权书：无论以后增加什么 case，都默认“和目前这些剩余情况一样处理”。Compiler/build 本来有机会把新 case 变成 review obligation，catch-all 却把这份压力吞掉了。

以下情形触发：

- closed union/enum 用 `_ -> ignore/default/error`；
- protocol 新 variant 会自动落入 generic fallback，而没人审它是否安全；
- permission/role/tool case 新增后旧 gate 继续 compile，因为 `_` 接住；
- recovery 新 state 自动被 “unknown → continue” 处理；
- test 只证明现在 cases 都能跑，却无法在新增 case 时迫使 policy 更新。

不要误杀真正 open-world boundary。Vendor extension、unknown JSON field、versioned protocol extension 若 contract 明确规定“未知 case 按 X 处理”，catch-all 就是在执行**正式 unknown-case law**。关键是世界是否真的开放，以及 fallback 是否就是那个开放协议的 stable semantics。

也不要误杀经过前置 typed narrowing 后的 unreachable remainder；如果 type system 已证明 `_` 只覆盖一个被正式等价化的集合，并且新增 case 会迫使 narrowing 更新，就不属于本规则。

与 `non-exhaustive-transition` 区分：那条专门审 finite `state × event` relation 的未决 cell；本规则更广，任何 closed vocabulary 都可能被 catch-all 吞掉 future obligation。

决定性 experiment：临时新增一个 plausible case。若 code 仍 compile/test green，而且没人被迫回答“这个新 case 在这里意味着什么”，fallback 正在替未来作政策决定。

> Catch-all 可以处理已知的“其余”，不能替尚未出生的 case 提前签署语义。
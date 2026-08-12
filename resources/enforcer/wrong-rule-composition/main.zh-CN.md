# wrong-rule-composition — Main

先画 premise dependency，再选 combinator。

把每条 rule 看成一个 proposition：它需要哪些 facts？成功后又建立哪些新 facts？若 B 的 required fact 只有 A 成功才能提供，那么关系就是 sequential；A fail 后 B 没有资格运行。若多条 rule 都只依赖同一个完整 input，则可以独立 evaluate，并按 caller 需要积累结果。

健康例子：

```text
parse email
  → success 后 validate domain       // dependent, short-circuit

validate name
validate age
validate address                       // independent, accumulate
```

常见假修复：

- 全项目规定“永远 fail fast”；
- 全项目规定“用户体验必须一次显示所有错误”，于是连 premise 不存在的 error 也硬算；
- cascading errors 先全部制造，再在 UI 用 regex/filter 去掉；
- 每条 downstream rule 重复检查自己的 prerequisite，导致 dependency knowledge 又复制；
- independent rules 为了 accumulation 被强行串行，白付 latency；
- dependent rules 被 parallelize，结果后者只能处理大量 `None/invalid` 假输入。

Composition 应尽量在 type 上体现 premise。Parse 成功后返回更强的 `ParsedEmail`，下一条 rule 接这个类型，比所有 rule 都接原始 string 再自己判断 “能不能运行” 更能表达 dependency。

验证应覆盖两类场景：

1. prerequisite fail + downstream potential errors：只报告仍然有意义的 reachable failure；
2. 多个 independent violation 同时存在：返回完整的 independent set，且 evaluation order 不应改变集合语义。

如果 caller 只需要 first error（例如 machine protocol 明确 fail-fast），也可以对 independent rules 选择 first，但这是 caller-facing contract，应明确命名，不要混成 domain “只有第一条是真的”。

完成时，每个 error 都能解释自己所需 premise 已经成立；同时 independent facts 不会仅因某个无关 check 先失败就被隐藏。

> Combinator 不是代码风格。它是在回答一个逻辑问题：一个失败发生后，哪些命题仍然有资格被判断？
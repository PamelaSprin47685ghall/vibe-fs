# missing-rule-combinator — Enforcer 中文版

## 定义
当多条规则已经拥有相同的输入/输出 algebra，却在多个 caller 反复手写“遇错返回、累计错误、map 成下一值”等控制流时，缺的不是 helper，而是**composition law 的 owner**。

例如若多条规则都是 `A -> Result<A,E>`，那么顺序 short-circuit 已经是一种稳定语义；继续在每个 caller 写 `if error return`，等于让每个 caller 都重新解释一次这套 algebra。

## 何时触发
- 三处以上手写同一种 validator chaining；
- 同形 rules 在多个地方重复 accumulate / short-circuit / map；
- 改变 error composition semantics 时需要修改多个 caller；
- 临时变量和 nested result handling 占据了比 rule 本身更多的代码。

## 不要误判
- 只有一两处、形状尚未稳定，不要预抽象；
- 签名相似但 failure meaning 不同，不要硬共用 combinator；
- policy 本身被复制，应先处理 policy owner，不是抽 composition helper；
- 简单函数调用已经足够清楚时，不需要创造 DSL。

## 刀口
问：**caller 反复写的到底是业务规则，还是“这些规则应该怎样组合”的同一条 meta-rule？**

若后者已经稳定重复，meta-rule 应该有名字、有测试、有单一 owner。

## 与近邻区分
`rule-spaghetti` 是 propositions 尚未清晰；`wrong-rule-composition` 是已有/拟有 combinator 选择了错误的 logical law。

这里的前提是：规则已经有共同形状，缺的是 composition vocabulary。

## 例子
- 正例：五个 `Input -> Result<Input,Error>` validators 在三个 endpoint 各写一套 nested match。
- 近邻：两个 checks 一个是 prerequisite、一个是 advisory，签名像但语义不共用。
- 反例：`andThen` 与 `collectAll` 分别表达 dependent 与 independent laws，并有 law tests。

## 提醒
当重复的是**组合语义**，抽象才有价值；不要为了少几行代码发明 rules framework。

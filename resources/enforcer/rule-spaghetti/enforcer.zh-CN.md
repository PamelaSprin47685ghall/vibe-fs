# rule-spaghetti — Enforcer 中文版

## 定义
Rule spaghetti 不是“if 太多”。真正的问题是：业务规则只存在于 nested branch、mutable flags、early return 和临时变量的执行轨迹里，读者必须在脑中跑程序，才能还原 policy 本身。

Policy 本质是“哪些事实推出哪些结论”。Control flow 只是其中一种解释器。当解释器成为唯一 specification，规则的语义和执行拓扑绑死，任何小改动都要求重新模拟整张路径图。

## 何时触发
- eligibility/permission/validation/routing 只能靠逐行跟踪条件才能解释；
- 多个 bool 在函数里被 set/unset，最后拼成 verdict；
- domain reviewer 无法把需求句子对应到 named predicate/case；
- 新增一个 policy clause 需要在多个 early-return 路径插条件；
- comments 开始逐段解释“这里为什么先判断 X 再判断 Y”。

## 不要误判
- 真正有因果依赖的 sequential checks 可以顺序写，只要 premise 与结论清楚命名；
- 一个简单 `if/else` 比抽象 DSL 更清晰时，当然保留；
- performance-critical imperative implementation 若有同等清楚的 specification/property，可作为经过验证的翻译；
- pattern match/decision table 本身也有 branches，但它们可能正是在直接陈述 domain cases。

## 刀口
让一个懂业务、不熟实现的人读这段代码。**他是在读规则，还是在模拟机器？**

如果必须记住“flag A 在第 37 行可能被改、然后第 52 行 early return”，规则已经消失在 interpreter 里。

## 与近邻区分
`missing-rule-combinator` 是规则已经命名清楚，但组合机制反复手写；这里更早：连单个 policy 的可读形态都没有。

`wrong-rule-composition` 是组合 law 选错；这里是 policy 根本没被表达成可组合 propositions。

## 例子
- 正例：资格判断 80 行 nested `if`，中途修改 `eligible/reason/override` 三个 flags。
- 近邻：`parse -> authorize -> validate` 每一步有命名结果，后一步确实依赖前一步成功。
- 反例：closed cases / decision table 与业务条款一一对应。

## 提醒
代码应该让规则被**阅读**，而不是要求读者先成为 CPU。

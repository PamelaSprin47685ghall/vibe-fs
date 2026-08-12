# rule-spaghetti — Main

先把 policy 从 control flow 中“抄出来”，再重构代码。

用 domain language 写出每条 proposition：谁满足什么条件、哪些 premise 依赖前一步建立的 fact、哪些 constraint 彼此独立、最终如何得出 verdict。然后给 proposition 命名，让 source structure 尽可能与这些句子一一对应。

可选工具很多：small predicate、pattern match、decision table、closed cases、组合器。不要先选“rules engine”；先让 policy 变得可读，再选择最小 representation。

常见假修复：

- 每个 `if` 抽成 helper，但 caller 仍是一模一样的 maze；
- 用注释给每层 branch 写说明，policy 依旧只能通过执行路径恢复；
- 把所有条件压成一条超级 boolean；
- 引入 generic rules DSL/framework，结果 domain clause 反而藏进 configuration；
- 为避免 nesting 到处 early return，逻辑依赖仍然没有名字；
- 只追求函数变短，把同一 rule 拆散到更多文件。

验证可以让一个 domain reviewer 不看实现细节，只拿业务条款逐条对 source：每条 clause 应能定位到 named predicate/case；composition 应显示 prerequisite 与 independent checks 的关系。

测试也要围绕 rule truth，而不是 temporary flags。对关键 combination 断言 meaningful verdict/reason；如果 rule 有普遍 law，再用 property test 扩展 input space。

改一条业务 clause 时，影响应集中在对应 proposition/composition，而不是需要重新模拟整个 maze 找所有相关 branch。

如果某些 imperative form 出于 performance 必须保留，可以维护一个等价、可读的 declarative specification/property，并证明 optimized interpreter 与它一致。性能是实现理由，不应让 policy 本身消失。

完成时，阅读 source 像阅读一份可执行 policy：事实有名字、组合有意义、branch 只是承载逻辑，不再是逻辑唯一存在的地方。

> 好规则代码不是没有 `if`，而是每个 `if` 都在执行一条已经说得清楚的规则，而不是替规则本身保密。
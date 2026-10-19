# rule-spaghetti — Lesson

## 评价

### 定义

Rule spaghetti 不是“if 太多”。真正的问题是：业务规则只存在于 nested branch、mutable flags、early return 和临时变量的执行轨迹里，读者必须在脑中跑程序，才能还原 policy 本身。

Policy 本质是“哪些事实推出哪些结论”。Control flow 只是其中一种解释器。当解释器成为唯一 specification，规则的语义和执行拓扑绑死，任何小改动都要求重新模拟整张路径图。

### 支配原则

Policy 是事实与结论之间的关系。Control flow 只是这个关系的一种解释器。当解释器成为唯一的 specification，读者必须模拟执行路径才能发现规则，而任何小改动都会同时改变语义与执行拓扑。当 proposition 被直接命名、组合直接暴露逻辑、读者不必在心里维护一个私有程序计数器时，规则才得到了更好的表达。

### 何时触发

- eligibility/permission/validation/routing 只能靠逐行跟踪条件才能解释；
- 多个 bool 在函数里被 set/unset，最后拼成 verdict；
- domain reviewer 无法把需求句子对应到 named predicate/case；
- 新增一个 policy clause 需要在多个 early-return 路径插条件；
- comments 开始逐段解释“这里为什么先判断 X 再判断 Y”。

### 不应触发

- 真正有因果依赖的 sequential checks 可以顺序写，只要 premise 与结论清楚命名；
- 一个简单 `if/else` 比抽象 DSL 更清晰时，当然保留；
- performance-critical imperative implementation 若有同等清楚的 specification/property，可作为经过验证的翻译；
- pattern match/decision table 本身也有 branches，但它们可能正是在直接陈述 domain cases。

### 与相邻规则区分

`missing-rule-combinator` 是规则已经命名清楚，但组合机制反复手写；这里更早：连单个 policy 的可读形态都没有。

`wrong-rule-composition` 是组合 law 选错；这里是 policy 根本没被表达成可组合 propositions。

### 判定程序

让一个懂业务、不熟实现的人读这段代码。**他是在读规则，还是在模拟机器？**

如果必须记住“flag A 在第 37 行可能被改、然后第 52 行 early return”，规则已经消失在 interpreter 里。

### 例子

- 正例：资格判断 80 行 nested `if`，中途修改 `eligible/reason/override` 三个 flags。
- 近邻：`parse -> authorize -> validate` 每一步有命名结果，后一步确实依赖前一步成功。
- 反例：closed cases / decision table 与业务条款一一对应。

## 建议

### 现在该做什么

先把 policy 用 domain sentences 写出来，再把句子映射成 named predicates、closed cases 或小型 decision table。让 control flow 服务规则，而不是让规则藏在 control flow 里。

### 为什么重要

当 policy 只能靠执行轨迹理解，维护者会开始“局部修 branch”。每个局部 patch 都可能改变另一条隐藏路径，久而久之规则本身没人能完整复述，只剩 tests 与 comments 在外围猜测。

可读 policy 缩短 proof：读者直接看到 proposition 与 composition，而不必保存临时变量和路径历史。

### 修复策略

- 抽出有业务名字的 propositions，不抽 `check1/check2`；
- 区分 dependent prerequisites 与 independent constraints；
- 对 closed alternatives 用 cases/pattern matching；
- 对稳定 rule algebra 用小 combinators；
- orchestration/effects 留在 policy 外；
- 保持简单，不为几条规则引入通用 rules engine。

### 决策分支

- 若 policy 仍只存在于嵌套分支里，抽出命名的 propositions 并组合它们；
- 若 propositions 已经命名却被 mutable flags 粘在一起，用 combinators 或 cases 替换 flags；
- 若组合已经存在但用了错误的失败法则，那是 `wrong-rule-composition`——修 combinator，不是改名字。

### 常见假修复

- 每个 `if` 抽成 helper，但顶层仍是同一个迷宫。
- 用 comment 给迷宫配旁白。
- 压成一个超长 boolean expression；CPU 更短，读者更痛苦。
- 引入 YAML/JSON rules engine，把 policy 从代码搬到另一个更难检查的 interpreter。
- 为追求 declarative 而隐藏真正 sequential dependency。

### 验证

业务条款应能逐条指向 source 中的 named predicate/case。修改一条条款时，影响范围应围绕该 proposition，而不是遍历所有 control paths。

Tests 应围绕 policy combinations，而不是中间 flags。

### 完成条件

source 本身就是可读的 policy statement；理解规则不再要求模拟临时状态与 early-return 路径。

### Nudge

代码应该让规则被**阅读**，而不是要求读者先成为 CPU。

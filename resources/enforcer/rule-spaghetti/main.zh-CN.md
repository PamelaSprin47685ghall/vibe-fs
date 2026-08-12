# rule-spaghetti — Main 中文版

## 现在该做什么
先把 policy 用 domain sentences 写出来，再把句子映射成 named predicates、closed cases 或小型 decision table。让 control flow 服务规则，而不是让规则藏在 control flow 里。

## 为什么这很重要
当 policy 只能靠执行轨迹理解，维护者会开始“局部修 branch”。每个局部 patch 都可能改变另一条隐藏路径，久而久之规则本身没人能完整复述，只剩 tests 与 comments 在外围猜测。

可读 policy 缩短 proof：读者直接看到 proposition 与 composition，而不必保存临时变量和路径历史。

## 修复策略
- 抽出有业务名字的 propositions，不抽 `check1/check2`；
- 区分 dependent prerequisites 与 independent constraints；
- 对 closed alternatives 用 cases/pattern matching；
- 对稳定 rule algebra 用小 combinators；
- orchestration/effects 留在 policy 外；
- 保持简单，不为几条规则引入通用 rules engine。

## 常见假修复
- 每个 `if` 抽成 helper，但顶层仍是同一个迷宫。
- 用 comment 给迷宫配旁白。
- 压成一个超长 boolean expression；CPU 更短，读者更痛苦。
- 引入 YAML/JSON rules engine，把 policy 从代码搬到另一个更难检查的 interpreter。
- 为追求 declarative 而隐藏真正 sequential dependency。

## 验证
业务条款应能逐条指向 source 中的 named predicate/case。修改一条条款时，影响范围应围绕该 proposition，而不是遍历所有 control paths。

Tests 应围绕 policy combinations，而不是中间 flags。

## 完成条件
source 本身就是可读的 policy statement；理解规则不再要求模拟临时状态与 early-return 路径。

# missing-invariant-documentation — Enforcer 中文版

## 定义
Invariant 未记录，不是“文档不够多”，而是 correctness 依赖一条非显然、可证伪的关系，却没有在 semantic owner 处留下 durable statement。未来修改者只能从 defensive code、事故或口头传统重新发现它。

## 何时触发
- ordering、uniqueness、ownership、durability、state relation 很关键却无 canonical statement；
- rule 散在几个 comments，没人知道哪句 authoritative；
- 类型无法表达的约束也没有 behavioral/property test；
- 新人必须问“这里为什么绝对不能这样改”才能知道规则。

## 不要误判
- 强类型已让 invariant 一眼可见且 illegal state 无法构造，不必重复写长文；
- 缺的是为什么选择某方案，属于 `unrecorded-decision`；
- purely local implementation detail 无跨路径义务；
- tutorial/example 缺失不是 invariant debt。

## 刀口
把规则写成一句能被证明为 false 的话。若连这句话都没有 canonical owner，correctness 只存在于人的记忆里。

## 提醒
Documentation 的价值不是 prose 数量，而是让重要 correctness knowledge 能离开原作者继续存活。

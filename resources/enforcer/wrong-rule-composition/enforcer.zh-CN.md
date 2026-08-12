# wrong-rule-composition — Enforcer 中文版

## 定义
Rule composition 选错，发生在系统把一种 evaluation law 当成所有规则的统一教条：要么凡事 fail-fast，要么凡事 collect-all，却不看规则之间是否有逻辑依赖。

真正决定 composition 的不是编码风格，而是 premise graph：若 B 只有在 A 成功后才有意义，A 失败后继续执行 B 会制造垃圾错误；若 A/B 独立，第一条失败就停止则会隐藏同一输入已经证明的其它事实。

## 何时触发
- parse 失败后仍报告 parsed value 的业务约束错误；
- authorization 失败后继续执行只有授权主体才有意义的 checks；
- form 的多个独立 field errors 只返回第一条，caller 明明需要完整集合；
- 一个 generic validation pipeline 对所有规则固定 short-circuit 或 fixed accumulation；
- cascading errors 需要 UI 再过滤，因为 evaluator 本身不懂 prerequisite。

## 不要误判
- 有 failed premise 时停止是正确的；
- 安全/成本 policy 可能明确要求 fail-fast，即使理论上可继续，此时是 contract；
- 有些 independent checks 很昂贵，可有明确 staged policy，但要承认这是 operational choice；
- 单条 rule 没有 composition 问题。

## 刀口
对任意两条规则问：**B 的问题在 A 失败时仍然有真值吗？**

没有：顺序、short-circuit。
有：可以独立判断；若 caller 需要完整 evidence，应 accumulate。

## 与近邻区分
`missing-rule-combinator` 是 law 没有 owner；这里是 law 选错。

`rule-spaghetti` 是 propositions/依赖本身埋在 imperative maze 中；先把依赖看清，才能谈正确 composition。

## 例子
- 正例：email 缺失后仍报“email domain 不允许”；或者三个独立 field violations 只回第一个。
- 近邻：parse 失败后不运行 semantic validation，因为根本没有 parsed object。
- 反例：dependent chain 用 `andThen`，independent constraints 用 `collectAll`。

## 提醒
不要选择“fail fast 派”或“collect all 派”。让事实之间的依赖关系决定 evaluator。

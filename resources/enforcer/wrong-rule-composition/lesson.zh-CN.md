# wrong-rule-composition — Lesson

## 评价

### 定义

Rule composition 选错，发生在系统把一种 evaluation law 当成所有规则的统一教条：要么凡事 fail-fast，要么凡事 collect-all，却不看规则之间是否有逻辑依赖。

真正决定 composition 的不是编码风格，而是 premise graph：若 B 只有在 A 成功后才有意义，A 失败后继续执行 B 会制造垃圾错误；若 A/B 独立，第一条失败就停止则会隐藏同一输入已经证明的其它事实。

### 支配原则

组合的语义跟随逻辑依赖。若规则 B 假设 A 已经确立了一个事实，`A ∧ B` 就是顺序的：A 的失败会移除赋予 B 意义的前提。独立校验则不同：每个 proposition 都可以在同一份输入上被求值，因此短路销毁了有用信息却没能提高正确性。根因是不看逻辑依赖、对一切套用同一种 evaluation law。一个通用的 validation pipeline 无法只凭语法选择这些语义；依赖图决定代数。

### 何时触发

- parse 失败后仍报告 parsed value 的业务约束错误；
- authorization 失败后继续执行只有授权主体才有意义的 checks；
- form 的多个独立 field errors 只返回第一条，caller 明明需要完整集合；
- 一个 generic validation pipeline 对所有规则固定 short-circuit 或 fixed accumulation；
- cascading errors 需要 UI 再过滤，因为 evaluator 本身不懂 prerequisite。

### 不应触发

- 有 failed premise 时停止是正确的；
- 安全/成本 policy 可能明确要求 fail-fast，即使理论上可继续，此时是 contract；
- 有些 independent checks 很昂贵，可有明确 staged policy，但要承认这是 operational choice；
- 单条 rule 没有 composition 问题。

### 与相邻规则区分

`missing-rule-combinator` 是 law 没有 owner；这里是 law 选错。

`rule-spaghetti` 是 propositions/依赖本身埋在 imperative maze 中；先把依赖看清，才能谈正确 composition。

### 判定程序

对任意两条规则问：**B 的问题在 A 失败时仍然有真值吗？**

没有：顺序、short-circuit。
有：可以独立判断；若 caller 需要完整 evidence，应 accumulate。

### 例子

- 正例：email 缺失后仍报“email domain 不允许”；或者三个独立 field violations 只回第一个。
- 近邻：parse 失败后不运行 semantic validation，因为根本没有 parsed object。
- 反例：dependent chain 用 `andThen`，independent constraints 用 `collectAll`。

## 建议

### 现在该做什么

画出 rule prerequisite graph。依赖链用 sequential short-circuit；可在同一输入上独立成立的 constraints 用 accumulation（必要时并行）。不要让一个 generic pipeline 替 domain 决定 logical dependence。

### 为什么重要

错误的 composition 会制造两种相反的谎言：

- cascading nonsense：前提不存在，却继续报告后续“错误”；
- evidence suppression：多个独立事实都已经成立，却只告诉 caller 第一条。

两者都不是 UX 小问题，而是 evaluator 对“哪些 proposition 现在有意义”判断错了。

### 修复策略

- 给每条 rule 明确 required facts 与 produced facts；
- 依赖边形成 `andThen`；
- 同层独立 constraints 形成 `collectAll`；
- error algebra 保留 prerequisite failure 与 independent violation 的区别；
- operational staging（成本/安全）若影响 composition，要明确记录为 policy，而不是偷偷等同逻辑依赖。

### 决策分支

若规则 B 需要 A 确立的事实，顺序执行，A 失败即短路。
若规则在同一份输入上彼此独立，累积它们的结果，而不是在第一次失败就停止。

### 常见假修复

- 全项目统一 fail-fast。
- 全项目统一 collect-all。
- 继续跑所有 rule，最后在 UI 根据 error code 丢掉 cascading errors。
- 每条 downstream rule 重新检查 prerequisite，复制逻辑。
- 用 priority numbers 模拟依赖，却没有 produced/required fact 关系。

### 验证

构造两类 fixture：

1. prerequisite 失败 + downstream 本可报错：只能出现 prerequisite failure；
2. 多个 independent constraints 同时违反：应得到完整独立 error set。

### 完成条件

reported errors 恰好等于当前 facts 下有意义且为真的 violations；evaluation order 来自 dependency，而非编码习惯。

### Nudge

不要选择“fail fast 派”或“collect all 派”。让事实之间的依赖关系决定 evaluator。

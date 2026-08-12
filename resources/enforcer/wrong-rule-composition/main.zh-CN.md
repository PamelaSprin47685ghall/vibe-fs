# wrong-rule-composition — Main 中文版

## 现在该做什么
画出 rule prerequisite graph。依赖链用 sequential short-circuit；可在同一输入上独立成立的 constraints 用 accumulation（必要时并行）。不要让一个 generic pipeline 替 domain 决定 logical dependence。

## 为什么这很重要
错误的 composition 会制造两种相反的谎言：

- cascading nonsense：前提不存在，却继续报告后续“错误”；
- evidence suppression：多个独立事实都已经成立，却只告诉 caller 第一条。

两者都不是 UX 小问题，而是 evaluator 对“哪些 proposition 现在有意义”判断错了。

## 修复策略
- 给每条 rule 明确 required facts 与 produced facts；
- 依赖边形成 `andThen`；
- 同层独立 constraints 形成 `collectAll`；
- error algebra 保留 prerequisite failure 与 independent violation 的区别；
- operational staging（成本/安全）若影响 composition，要明确记录为 policy，而不是偷偷等同逻辑依赖。

## 常见假修复
- 全项目统一 fail-fast。
- 全项目统一 collect-all。
- 继续跑所有 rule，最后在 UI 根据 error code 丢掉 cascading errors。
- 每条 downstream rule 重新检查 prerequisite，复制逻辑。
- 用 priority numbers 模拟依赖，却没有 produced/required fact 关系。

## 验证
构造两类 fixture：

1. prerequisite 失败 + downstream 本可报错：只能出现 prerequisite failure；
2. 多个 independent constraints 同时违反：应得到完整独立 error set。

## 完成条件
reported errors 恰好等于当前 facts 下有意义且为真的 violations；evaluation order 来自 dependency，而非编码习惯。

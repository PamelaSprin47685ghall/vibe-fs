# missing-rule-combinator — Main 中文版

## 现在该做什么
写下共同 rule signature 与真正需要的 composition laws，只实现最小集合：例如 dependent `andThen`、independent `collectAll`、`map`。然后让 callers 使用这套 vocabulary，而不是继续手写 nested control flow。

## 为什么这很重要
重复 combinator logic 复制的是 failure policy：到底何时停止、哪些错误可以同时成立、顺序是否有意义。它比普通 helper duplication 更危险，因为 caller 看起来只是在“写 plumbing”，实际每处都拥有了一点 policy。

## 修复策略
- 从已重复的稳定 shape 抽象，不从未来想象抽象；
- combinator 名字表达 logical law，不叫 `processRules`；
- dependent/independent semantics 分开；
- rule 自身保持 domain-specific；
- 对 combinator 写 law-like tests：ordering、short-circuit、accumulation；
- 新 rule 不需要 escape hatch 才算 algebra 真稳定。

## 常见假修复
- 上一个通用 rules engine / DSL，只为替代几行函数组合。
- 把所有 rules 强行塞进一个 signature，靠 `obj/any` 逃逸差异。
- 只抽 `runRules()`，内部仍由 caller-specific flags 决定 composition。
- 统一成 fail-fast，因为实现最简单，即使规则彼此独立。
- 统一成 accumulate-all，即使后续 rule 缺少 prerequisite。

## 验证
不同 caller 给相同 rule set 应获得相同 composition semantics；新增 caller 不应重新实现 short-circuit/accumulate policy。

对 combinator 本身测试其 law，而不是只通过某个业务 fixture 间接碰到。

## 完成条件
composition semantics 有一个小而清楚的 vocabulary；callers 负责选择规则，不再各自重新发明规则怎样组合。

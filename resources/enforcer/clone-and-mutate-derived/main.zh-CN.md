# clone-and-mutate-derived — Main 中文版

## 现在该做什么
先写清 source 与 derived value 的 semantic relation，然后用 constructor/record transformation 显式表达哪些 facts 保留、哪些重算、哪些丢弃。不要让 source 的完整 future shape 自动成为继承政策。

## 为什么重要
Clone-by-default 把未来字段默认为“应该传播”。这会让新增字段绕过 review：security token、lifecycle marker、ownership metadata、cache state 都可能无声进入一个从未打算拥有它的 derived concept。

显式 construction 则反过来：新字段出现时，compiler 或 constructor 会迫使作者做决定。

## 修复策略
- 区分“同一 value 的 immutable update”与“新 semantic value 的 derivation”；
- 后者列出 preserved facts；
- state-specific data 由 target constructor 拥有；
- local mutation 若用于性能，封闭在 constructor 内；
- source 新增字段时要求 explicit keep/drop/recompute decision。

## 决策分支
- 若 derivation 是 clone 一个 mutable prototype 再 patch，改用基于预期 facts 的 constructor；
- 若 source 与 derived 是同一值类型、所有字段都有意保留，用 constructor-safe 的 record copy，而不是 mutable clone；
- 若 source 出现新字段，要求在 derivation 里显式作出 keep/drop/recompute 决定。

## 常见假修复
- 换更安全的 deep clone library。
- clone 后 `Object.freeze`；accidental inheritance 已经发生。
- 写 comment 列“这些字段应该一样”。
- 封装成 `withChanges()` helper，内部仍全量复制。
- 为所有 immutable record copy 都建笨重 builder，误杀简单同值更新。

## 验证
给 source model 增加一个有语义的新字段。真正 derived constructors 应出现明确决策点，而不是测试仍全绿、字段悄悄传播。

## 完成条件
每个 derived value 的内容都能由 target concept 的 constructor/relation 解释；没有字段仅因为“prototype 当时有它”而进入新值。

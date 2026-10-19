# clone-and-mutate-derived — Lesson

## 评价

### 定义

Clone-and-mutate 的核心危险不是 mutation 本身，而是**新值的意义由“哪些字段恰好没有被 patch”来定义**。

`clone(old); next.status = ...` 实际在说：old 当前和未来拥有的所有字段，默认都应该被 next 继承，除非某段 patch 代码记得反对。这是一种负面构造：constructor 不声明新值拥有什么，只声明少数例外。

### 支配原则

一个值应当自己说明自己的真相。原型克隆却用负面方式定义真相：每个继承来的字段都是靠“没有 patch 到”被接受的。随着对象演化，新增字段会悄悄流入旧的派生代码，把结构复用变成语义继承。于是 derived value 依赖的是 prototype 未来的整个形状，而不只是 constructor 原本想保留的那些 facts。

### 何时触发

- domain value 通过 clone/spread/deepcopy 后逐字段修改生成；
- source 新增字段会自动流入 derived value，无需任何 compiler/review decision；
- invariants 要在 patch 完后才能重新检查；
- derived value 与 source 语义不同，却继承 source 全部表示；
- fixture/prototype 模式让新 case 不小心继承旧 case 的 irrelevant fields。

### 不应触发

- immutable record update 表示“同一个 semantic value 只改一个属性”，且 constructor invariants 仍安全，可以合理；
- local mutable accumulator 不逃出 constructor scope，可作为实现细节；
- 真正 copy-on-write data structure 不等于 semantic derivation；
- 不要为避免 copy syntax 把简单 immutable update 写成巨型 constructor ceremony。

### 与相邻规则区分

`in-place-mutation` 改现有共享 identity；这里是创造“新值”却让 prototype 决定其内容。

`runtime-checked-builder` 是构造过程可处于非法中间态；clone-and-mutate 可能同时存在，但这里重点是 accidental inheritance。

### 判定程序

想象 source type 明天新增字段 `x`。**derived value 应该自动得到 x，还是应该迫使作者决定 keep/drop/recompute？**

若应该明确决定，而当前 clone 会偷偷继承，本规则触发。

### 例子

- 正例：`next = {...order, status:'paid'}`，但 `PaidOrder` 与原 `Order` 语义不同，未来字段全自动继承。
- 近邻：同一 immutable `UserProfile` 更新 display name，所有其它字段语义上确实保留。
- 反例：`PaidOrder.create(orderId, amount, receipt)` 明确列出 derived concept 需要的 facts。

## 建议

### 现在该做什么

先写清 source 与 derived value 的 semantic relation，然后用 constructor/record transformation 显式表达哪些 facts 保留、哪些重算、哪些丢弃。不要让 source 的完整 future shape 自动成为继承政策。

### 为什么重要

Clone-by-default 把未来字段默认为“应该传播”。这会让新增字段绕过 review：security token、lifecycle marker、ownership metadata、cache state 都可能无声进入一个从未打算拥有它的 derived concept。

显式 construction 则反过来：新字段出现时，compiler 或 constructor 会迫使作者做决定。

### 修复策略

- 区分“同一 value 的 immutable update”与“新 semantic value 的 derivation”；
- 后者列出 preserved facts；
- state-specific data 由 target constructor 拥有；
- local mutation 若用于性能，封闭在 constructor 内；
- source 新增字段时要求 explicit keep/drop/recompute decision。

### 决策分支

- 若 derivation 是 clone 一个 mutable prototype 再 patch，改用基于预期 facts 的 constructor；
- 若 source 与 derived 是同一值类型、所有字段都有意保留，用 constructor-safe 的 record copy，而不是 mutable clone；
- 若 source 出现新字段，要求在 derivation 里显式作出 keep/drop/recompute 决定。

### 常见假修复

- 换更安全的 deep clone library。
- clone 后 `Object.freeze`；accidental inheritance 已经发生。
- 写 comment 列“这些字段应该一样”。
- 封装成 `withChanges()` helper，内部仍全量复制。
- 为所有 immutable record copy 都建笨重 builder，误杀简单同值更新。

### 验证

给 source model 增加一个有语义的新字段。真正 derived constructors 应出现明确决策点，而不是测试仍全绿、字段悄悄传播。

### 完成条件

每个 derived value 的内容都能由 target concept 的 constructor/relation 解释；没有字段仅因为“prototype 当时有它”而进入新值。

### Nudge

新值应该由它**明确拥有的 facts**构造，而不是由 prototype 恰好还剩什么构造。

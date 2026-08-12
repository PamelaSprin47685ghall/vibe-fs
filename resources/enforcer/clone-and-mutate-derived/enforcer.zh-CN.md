# clone-and-mutate-derived — Enforcer 中文版

## 定义
Clone-and-mutate 的核心危险不是 mutation 本身，而是**新值的意义由“哪些字段恰好没有被 patch”来定义**。

`clone(old); next.status = ...` 实际在说：old 当前和未来拥有的所有字段，默认都应该被 next 继承，除非某段 patch 代码记得反对。这是一种负面构造：constructor 不声明新值拥有什么，只声明少数例外。

## 何时触发
- domain value 通过 clone/spread/deepcopy 后逐字段修改生成；
- source 新增字段会自动流入 derived value，无需任何 compiler/review decision；
- invariants 要在 patch 完后才能重新检查；
- derived value 与 source 语义不同，却继承 source 全部表示；
- fixture/prototype 模式让新 case 不小心继承旧 case 的 irrelevant fields。

## 不要误判
- immutable record update 表示“同一个 semantic value 只改一个属性”，且 constructor invariants 仍安全，可以合理；
- local mutable accumulator 不逃出 constructor scope，可作为实现细节；
- 真正 copy-on-write data structure 不等于 semantic derivation；
- 不要为避免 copy syntax 把简单 immutable update 写成巨型 constructor ceremony。

## 刀口
想象 source type 明天新增字段 `x`。**derived value 应该自动得到 x，还是应该迫使作者决定 keep/drop/recompute？**

若应该明确决定，而当前 clone 会偷偷继承，本规则触发。

## 与近邻区分
`in-place-mutation` 改现有共享 identity；这里是创造“新值”却让 prototype 决定其内容。

`runtime-checked-builder` 是构造过程可处于非法中间态；clone-and-mutate 可能同时存在，但这里重点是 accidental inheritance。

## 例子
- 正例：`next = {...order, status:'paid'}`，但 `PaidOrder` 与原 `Order` 语义不同，未来字段全自动继承。
- 近邻：同一 immutable `UserProfile` 更新 display name，所有其它字段语义上确实保留。
- 反例：`PaidOrder.create(orderId, amount, receipt)` 明确列出 derived concept 需要的 facts。

## 提醒
新值应该由它**明确拥有的 facts**构造，而不是由 prototype 恰好还剩什么构造。

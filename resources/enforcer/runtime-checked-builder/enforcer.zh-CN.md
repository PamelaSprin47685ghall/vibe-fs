# runtime-checked-builder — Enforcer

## Definition
当一个 public builder 允许对象长期处于 incomplete / contradictory 状态，任意顺序调用 setter，最后才靠 `build()` / `validate()` 检查 caller 是否“把仪式做对了”，它就是 runtime-checked builder 缺陷。

根因是把**construction protocol 编码成 mutable convention**：API 本来可以要求的事实被推迟成运行时状态，于是非法调用序列也成了正式可能性。

## Governing Principle
构造不是填表流程；构造是在证明“足够事实已经存在，可以让这个值进入世界”。

一个从 invalid 开始、逐步 maybe-valid 的 public builder 会造出一条影子 lifecycle。caller 可以漏步骤、重复步骤、顺序错、failed build 后继续复用、观察互相矛盾的中间组合。最后 `build()` 报错不是 robustness，而是 API 在发现自己主动邀请的错误。

但不要把这条规则写成“runtime validation 都不好”。很多约束天然只能运行时知道：数据库唯一性、实际数字范围、跨对象事实、用户输入。规则攻击的是**可避免的 temporal construction state**，不是 validation 本身。

## Trigger When
以下情形触发：

- required field 通过 optional setter 提供，忘一项直到 `build()` 才失败；
- method order 靠文档说明，而 API 本身不限制；
- half-built / failed builder 可以继续复用；
- contradictory combination 能先组出来，最后才统一拒绝；
- caller 习惯先 `isValid()` 再 `build()`；
- tests 大量覆盖“忘记调 X setter”这种本可由 API 消灭的错误。

## Do Not Trigger When
- staged/phantom-typed builder 让非法操作在类型层不可调用；
- 一个 atomic constructor 接收全部 required data，并对真正 dynamic constraint 返回 typed rejection；
- mutable accumulator 完全私有，不能逃逸，只在最后一次转成真实 domain value；
- parser 因输入本身逐步到达而有真实 incremental state；
- UI draft 本来就允许 incomplete，并且诚实建模成 draft，不冒充 completed domain object。

## Distinguish From
`illegal-state-representable` 管**最终值**仍能矛盾；`phase-flag-accumulation` 管 lifecycle 被 flag 群编码；`clone-and-mutate-derived` 从完成值复制后再变异。

Tie-break：核心是“caller 在 `build` 前能走非法 construction sequence”，用本规则；build 后仍可能 contradictory，再加 `illegal-state-representable`。

## Decision Procedure
把事实分三类：

1. 构造开始时已经知道；
2. 真实 staged work 中才会得到；
3. 天然只能 runtime 判断。

(1) 直接做 constructor input。(2) 若是真实业务 protocol，就用显式 state/type；若只是 setter ceremony，就删掉。(3) 保留 runtime rejection。

最后问一句狠的：**这个 incomplete public builder 自己有什么有用的 domain meaning？** 如果答案只是“等 caller 把 setter 调齐”，它不应该公开存在。

## Examples
- positive：`new OrderBuilder().withItem(x).build()` 因忘 `customer` 才炸，而且 half-built instance 还能继续用。
- positive：十二个 optional setter + 三套 required combination + 200 行 `validate()`，本质是在最后重建合法状态空间。
- near-miss：`Order.create(customer, items)` 因 runtime total/credit rule 返回 `Result<Order, ValidationError>`。
- near-miss：`Builder<NeedsCustomer>` 调 `withCustomer` 后变 `Builder<Ready>`，只有 `Ready` 能 build。
- counterexample：parser 的 incremental state 是问题本身，不是 setter 仪式。

## Nudge
如果一个 incomplete object 唯一的意义只是等 caller 记起剩余步骤，它就不该存在。

**已经知道的事实直接要求；真实阶段认真建模；只有现实迫使你运行时才知道的事，才留给 runtime validation。**

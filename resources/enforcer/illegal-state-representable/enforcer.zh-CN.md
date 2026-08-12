# illegal-state-representable — Enforcer

## Definition
当程序能够构造一个**业务世界里根本不存在**的值，然后靠后续 guard 假装它从未存在时，illegal state 已经进入了表示层。

根因是 **representation space 大于 valid state space**：flag、nullable field、stage marker 或松散 record 的笛卡尔积制造了现实没有的组合，于是 constructor 没做的证明，被迫摊给每一个 consumer。

## Governing Principle
类型不是容器清单；它是在说“这些东西都可能存在”。

如果 `Paid=true` 可以和 `Receipt=None` 同时出现，或者 `status="completed"` 可以和未完成 payload 共存，那么类型不是“更灵活”，而是在虚构世界。

这些虚构状态会持续收租：每个 reader 加 guard，每个 serializer 决定怎么兜底，每个 test 覆盖现实不该有的组合，recovery 最后也学会把垃圾继续搬运，因为“类型允许”。

正确方向不是到处多 `validate()`，而是在最早知道完整 invariant 的 construction boundary 一次证明，让下游有资格信任输入。

## Trigger When
在以下情形触发：

- 多处代码反复写“如果 A，那么 B 一定存在”；
- 多个 flag 表示同一个 lifecycle，而 truth table 中存在无业务意义组合；
- 某些字段之所以 nullable，只因为它们只属于特定状态；
- domain record 有 `validate()`，并要求所有 caller 自觉调用；
- persistence/recovery 能读出 policy 随后立刻认定“不可能”的组合；
- 同一个 contradiction 在多个模块重复防御。

## Do Not Trigger When
- wire DTO 必须暂时容纳不可信输入，但在进入 domain 前经一个 fail-closed constructor 转成闭合类型；
- 所有可表示组合确实都有业务含义；
- 某条约束只能 runtime 判断，但一个 atomic constructor 能返回 typed rejection，且不会泄漏 invalid instance；
- 临时 builder state 完全私有、不能逃逸，也不冒充最终 domain value。

## Distinguish From
`boolean-blindness` 专门处理 boolean 抹掉命名选择；`null-ambiguity` 是多个 absence reason 被压成一个空值；`runtime-checked-builder` 处理构造过程本身的非法阶段。本规则管更广的一刀：**最终 domain value 本身能够表达现实禁止的世界。**

Tie-break：contradiction 若已经进入最终值，用本规则；只有 builder 中途可非法，用 `runtime-checked-builder`；主要问题是 true/false 丢了 domain vocabulary，用 `boolean-blindness`。

## Decision Procedure
先完全不看现有字段，用业务语言列出合法状态。再枚举当前 representation 实际允许的组合。两者的差集，就是缺陷。

然后问：哪个边界第一次拥有足够事实判断合法性？那里就是 invariant owner。让它构造闭合 case、state-specific record，或一次性 validated value。

## Examples
- positive：`{ isPaid: bool; receiptId: string option }` 同时允许“已支付无凭证”和“未支付有凭证”，而所有 caller 都拒绝这两种组合。
- positive：`status + completedAt? + failure?` 能组合出 `open + completedAt + failure`，但 lifecycle 中根本没有这个状态。
- near-miss：HTTP DTO 为了拒绝 malformed input 必须允许 optional field；`Order.parse` 在进入 domain 前把它变成闭合 case。
- counterexample：`PaymentState = Unpaid | Paid of ReceiptId`，receipt 只存在于真正需要它的状态。

## Nudge
不要让每个 reader 都证明输入来自现实。

**让 construction 一次证明。一个“防不可能状态”的 guard，往往正是在告诉你：这个状态本来就不该能被构造。**

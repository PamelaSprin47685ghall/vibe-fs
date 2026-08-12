# non-exhaustive-transition — Enforcer

## Definition
当一个有限 `state × event` 关系里，有些格子的语义不是由 domain 决策给出，而是从 `default`、wildcard、silent ignore、generic error 或 fallthrough 自动继承时，transition 非穷尽。

根因是**transition table 里存在无人负责的 cell**。状态机明明是 closed 的，却有组合从未被明确判为 legal successor、idempotent no-op、typed rejection 或 impossible input；语法上的方便替 domain 做了决定。

## Governing Principle
有限状态机是一张关系表，不是一袋“常用 handler”。

每个 reachable `state × event` 都应该知道自己是什么意思。“有 default”不等于回答了这个问题，只表示若干从未逐一审过的组合因为控制流碰巧共用一个行为。

Wildcard 在演化时尤其危险：加一个新 event，compiler 仍然全绿，新 case 自动继承昨天的 fallback。系统等于在**没人决定语义的情况下接受了新 ontology**。

穷尽的价值也不是逼每格都写不同代码。多个 pair 可以合法共享 `NoOp`，但“它们等价”本身应该是明确判断，而不是 `_` 帮你选出来。

## Trigger When
以下情形触发：

- closed union/enum 的 state/event 用 wildcard/default 处理；
- 未识别组合直接 `return currentState`；
- partial map 缺 key 后统一 fallback；
- 新增 state/event 不会迫使 transition policy 重新审视；
- test 只覆盖 happy path，其余 pair 靠未声明 fallback；
- illegal transition 只 log + ignore，没有 typed rejection 和原因。

## Do Not Trigger When
- input space 本来就是 open-world，并且协议明确规定 future unknown 的稳定行为，例如“保留但绝不执行未知 extension frame”；
- 函数是开放 plugin dispatch，不是 finite domain transition relation；
- wildcard 覆盖的集合已被前置强类型 mechanically 限制成不可能进入；
- 一个命名 domain law 明确把已审过的 closed set 映射到同一结果，而且加新 case 仍会迫使这组集合重审。

## Distinguish From
`catch-all-swallows-future` 是更一般的“catch-all 吞掉 future variant obligation”；`illegal-state-representable` 管 state 内部值本身不合法；`phase-flag-accumulation` 管 lifecycle 被 flag soup 表达。

Tie-break：缺失的是 finite transition relation 的具体 cell 决策，用本规则；只是一般 wildcard 把未来 variant 隐藏掉，用 `catch-all-swallows-future`。

## Decision Procedure
把表写出来。

行 = reachable state；列 = event/input。每一格只能是：

- legal successor；
- 明确 idempotent/no-op law；
- typed rejection；
- mechanically unreachable。

任何只能描述成“掉进 default”的 cell 都是没做完的 domain design。

然后临时增加一个假 event。若它自动继承旧行为而不是制造显式决策义务，exhaustiveness 仍然是假的。

## Examples
- positive：closed lifecycle 中 `switch event { Start -> ...; Stop -> ...; _ -> state }`，`Cancel` 和未来事件全部静默忽略。
- positive：transition dictionary 查不到 key 就统一“保持当前状态”，却没区分 illegal 和 idempotent。
- near-miss：versioned extension protocol 明确规定 unknown vendor frame “保留但永不执行”，这里世界本来就开放。
- counterexample：exhaustive match 对每个 pair 返回 `Next | NoOp reason | IllegalTransition {state; event}`。

## Nudge
Wildcard 不是 domain 决策；它只是未决定 case 变得不可见的地方。

**既然 state 和 event 都有限，就让它们之间的关系也有限、可读、可穷尽。**

# time-source-in-logic — Enforcer

## 定义

Time source 藏在 logic 里的问题，不是“domain 不允许理解现在几点”，而是 current time 成了**没有出现在 visible inputs 里的 policy input**。

函数签名看起来只有 `state, command`，内部却直接 `now()`：同样参数在 12:00 与 12:01 可以产生不同 decision。于是 deterministic replay、incident reconstruction、property reasoning 都缺一块关键事实——当时 policy 到底依据哪个 instant 作答。

“现在”不是 universal constant，而是 observation。

## 支配原则

当下是一个 input，不是 universal constant。内部调用 `now()` 的 decision 会在两个否则完全相同的 invocation 之间改变含义，使 replay 与 causal explanation 无法只从 visible data 得出。把 clock 移到 boundary 恢复 referential clarity：shell 观察时间；policy 推理一个被供应的 temporal fact。

## 何时触发

以下情形触发：

- domain rule 内部直接读 system clock 判断 expiry、eligibility、deadline、window、age、ordering；
- replay 同一 command 时会用 replay 当下时间，而不是 original decision time；
- test 必须 mock global clock 才能控制 core；
- 同一 operation 中不同 layer 各自 `now()`，得到略不同 instant，制造 boundary race；
- event 只记录 decision result，却没保存决定所依据的 temporal fact，导致历史无法解释；
- timezone/precision/default locale 在 deep logic 中被 ambient environment 偷偷决定。

## 不应触发

不要误杀 adapter/orchestration 读 clock。Healthy shape 往往正是 shell 在合适 boundary 观察一次 time，把 `asOf`/`now`/deadline 作为普通 value 传入 deterministic policy。Display formatting 已有 supplied timestamp、且不根据 ambient time 分支，也没问题。纯 logging timestamp 更不是 domain input。

## 与相邻规则区分

与 `time-dependent-test` 区分：那条是 test verdict 被 real clock 污染；本规则是 production policy 的 decision dependency 被隐藏。两者常同时出现，因为不可注入的 ambient time 会自然逼测试去 monkey-patch。

与 `random-source-in-logic` 同理：clock 与 entropy 都是外部 observation。`impure-core` 是更广原则；当真正伤口是 current time 这一种 hidden input，用本规则更精确。

## 判定程序

决定性 thought experiment：把函数签名概念上改成 `decide(state, command, now)`。如果这样一写，原本隐形的业务依赖突然清楚、replay 也有了答案，那 `now` 本来就应该是 input。

只有一种例外需要更细：一个 operation 过程中确实必须多次观察时间（例如长期 lease renewal）。这时可以注入 clock port，而不是只传一个 instant；关键仍然是 clock capability 有明确 owner，而不是 core 随处偷读 global clock。

## 例子

- positive：eligibility code 在 domain 内调用 `Date.now()` 决定 window 是否开放。
- near-miss：HTTP adapter 读一次 clock，把 `asOf` 传进 pure function。
- counterexample：一个 test 睡到 midnight 才断言 expiry——那是 `time-dependent-test`。

## Nudge

> Clock 负责观察时间；policy 负责解释时间。不要让 observation 与 judgment 藏在同一个 `now()` 调用里。
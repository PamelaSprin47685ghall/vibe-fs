# time-source-in-logic — Lesson

## 评价

### 定义

Time source 藏在 logic 里的问题，不是“domain 不允许理解现在几点”，而是 current time 成了**没有出现在 visible inputs 里的 policy input**。

函数签名看起来只有 `state, command`，内部却直接 `now()`：同样参数在 12:00 与 12:01 可以产生不同 decision。于是 deterministic replay、incident reconstruction、property reasoning 都缺一块关键事实——当时 policy 到底依据哪个 instant 作答。

“现在”不是 universal constant，而是 observation。

### 支配原则

当下是一个 input，不是 universal constant。内部调用 `now()` 的 decision 会在两个否则完全相同的 invocation 之间改变含义，使 replay 与 causal explanation 无法只从 visible data 得出。把 clock 移到 boundary 恢复 referential clarity：shell 观察时间；policy 推理一个被供应的 temporal fact。

### 何时触发

以下情形触发：

- domain rule 内部直接读 system clock 判断 expiry、eligibility、deadline、window、age、ordering；
- replay 同一 command 时会用 replay 当下时间，而不是 original decision time；
- test 必须 mock global clock 才能控制 core；
- 同一 operation 中不同 layer 各自 `now()`，得到略不同 instant，制造 boundary race；
- event 只记录 decision result，却没保存决定所依据的 temporal fact，导致历史无法解释；
- timezone/precision/default locale 在 deep logic 中被 ambient environment 偷偷决定。

### 不应触发

不要误杀 adapter/orchestration 读 clock。Healthy shape 往往正是 shell 在合适 boundary 观察一次 time，把 `asOf`/`now`/deadline 作为普通 value 传入 deterministic policy。Display formatting 已有 supplied timestamp、且不根据 ambient time 分支，也没问题。纯 logging timestamp 更不是 domain input。

### 与相邻规则区分

与 `time-dependent-test` 区分：那条是 test verdict 被 real clock 污染；本规则是 production policy 的 decision dependency 被隐藏。两者常同时出现，因为不可注入的 ambient time 会自然逼测试去 monkey-patch。

与 `random-source-in-logic` 同理：clock 与 entropy 都是外部 observation。`impure-core` 是更广原则；当真正伤口是 current time 这一种 hidden input，用本规则更精确。

### 判定程序

决定性 thought experiment：把函数签名概念上改成 `decide(state, command, now)`。如果这样一写，原本隐形的业务依赖突然清楚、replay 也有了答案，那 `now` 本来就应该是 input。

只有一种例外需要更细：一个 operation 过程中确实必须多次观察时间（例如长期 lease renewal）。这时可以注入 clock port，而不是只传一个 instant；关键仍然是 clock capability 有明确 owner，而不是 core 随处偷读 global clock。

### 例子

- positive：eligibility code 在 domain 内调用 `Date.now()` 决定 window 是否开放。
- near-miss：HTTP adapter 读一次 clock，把 `asOf` 传进 pure function。
- counterexample：一个 test 睡到 midnight 才断言 expiry——那是 `time-dependent-test`。

## 建议

### 现在该做什么

把 clock observation 移到 shell，把 temporal judgment 留给 policy。

最常见修法是在 operation owning boundary 读取一次 time，明确 normalise timezone/precision，然后把这个 instant 当普通 input 传入 domain：

```text
asOf = clock.now()
result = decide(state, command, asOf)
```

这样相同 declared inputs 会产生相同 decision；event/replay 也能记录当时真正依据的 temporal fact。

如果一个长期 operation 确实需要多次观察时间，不要为了“pure”硬塞一个初始 instant。注入窄 clock port，让 capability 显式存在，并规定哪些 transition 有资格读取它。显式 effect 比假纯洁更诚实。

### 为什么重要

隐藏的时间让相同的 visible inputs 在现实中不再相同。这会破坏 deterministic tests、event replay、incident reconstruction，以及“为什么这次 deadline/eligibility decision 与上次不同”的解释。

### 修复策略

在 owning boundary 读一次 clock，在那里 normalise zone/precision，把 instant 穿过 pure policy。只有当一个 operation 内多次观察在语义上必要，才用 clock port。

### 决策分支

如果 decision 需要 temporal fact，把它加进函数 inputs，让 core 保持 deterministic。如果 operation 内多次观察在语义上必要，注入 clock port，而不是在 policy 里读 ambient clock。

### 常见假修复

常见假修复：

- 只在 test monkey-patch global `now()`，production architecture 完全不动；
- 一半 call site 传 `asOf`，另一半 deep logic 继续偷读 system clock；
- 把 startup time 存进 hidden singleton，之后所有 domain call 读它，就声称“固定了时间”；
- 每个 layer 各自读一次 now，最后用毫秒差异解释不同 outcome；
- 只传 date string，却把 timezone/precision 继续留给 ambient defaults；
- 为了可测试把整套 Clock service 注入所有函数，连只需要一个 instant 的 pure decision 都背上 capability。

### 验证

验证要证明 replay。固定 state/command/instant，多次运行得到完全相同 core outcome；明确改变 instant，只在 temporal law 的 boundary（expiry 前/到点/后等）改变结果。

事件若需要解释历史，就记录足够的 temporal provenance：是 command observed-at、deadline、lease expiry、还是 business effective date。不要只记“expired=true”，否则以后仍无法回答当时为什么过期。

还要测 timezone/precision normalization 的 owning boundary，防止不同 adapter 给 core 传语义上不一致的 instant。

如果只需要一个 system-clock adapter smoke，单独测它即可，不要让整个 domain suite重新依赖 real clock。

### 完成条件

完成时，每条 time-sensitive decision 都能从 recorded/declared inputs 重放；阅读函数签名即可知道 time 是依赖，而不是靠 grep `Date.now` 才发现。

> 显式时间不是为了让代码更“函数式”，而是为了让过去的 decision 在未来仍有可证明的时间坐标。

### Nudge

> Clock 负责观察时间；policy 负责解释时间。不要让 observation 与 judgment 藏在同一个 `now()` 调用里。

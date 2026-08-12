# impure-core — Enforcer

Impure core 的问题，不是“business code 里不能 I/O”这种洁癖，而是 policy 在作决定时自己向外抓 time、randomness、DB、network、env、file、global state，于是**真实 inputs 比函数签名多**。

表面上 `decide(state, command)`，实际上结果还依赖：现在几点、DB 此刻返回什么、env 怎么设、某 file 是否存在、global cache 里有什么。相同 visible arguments 因此不再代表相同 decision；replay、audit、local reasoning 都必须重新把整个世界叫回来。

以下情形触发：

- domain decision 内部查 DB 才决定 eligibility；
- policy 直接读 clock/RNG/env/global singleton；
- validation 同时发 network request，再根据 response 决定 rule；
- event fold/reducer 读取 filesystem/provider；
- incident 无法从 recorded inputs 重放，因为某个 decision fact 当时只存在外部世界；
- unit test 必须 mock 半个 runtime，才能调用一个本该只是业务判断的函数。

不要误杀 shell/adapter。它们的职责本来就是观察世界、执行 effects。Healthy architecture 不是“整个程序纯”，而是把 observation 与 judgment 分清：shell 读取外部 fact，core 根据 supplied facts 决定，shell 再执行 command/effect。

Logging/metrics 若只观察已完成 decision、不会反向改变结果，也不一定污染 core semantics。真正标准是：外部 effect/fact 是否参与了 business conclusion。

与 `time-source-in-logic` / `random-source-in-logic` 区分：后两条是更具体的 hidden input；若问题只是一种 source，用具体规则更锋利。`mixed-side-effect-boundaries` 则关注一个 imperative owner 同时承担多种 external world 的 failure/lifetime contract；core 即使不复杂，只要 policy 自己抓外界 fact，本规则仍成立。

一个决定性问题：**为了完全解释这次 decision，需要列出哪些 inputs？** 如果其中有函数签名/recorded event 中看不到的 ambient observation，core 正在隐瞒自己的因果前提。

> Pure core 的价值不是数学洁癖，而是让“为什么得出这个结论”只需要看显式事实，而不需要重演当时整个宇宙。
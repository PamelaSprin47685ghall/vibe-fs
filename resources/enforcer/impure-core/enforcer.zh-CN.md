# impure-core — Enforcer 中文版

## 定义
Core 不纯，不是“函数有 side effect”这么机械。真正的病灶是：**policy 在作决定的同时，偷偷向外界索取决定所需的事实**，导致 signature 没有诚实列出它的输入。

如果相同显式参数在不同时间、不同机器、不同环境变量、不同 DB 内容下得到不同 business decision，那么函数的真实输入集合比类型签名更大。隐藏 dependency 让 replay、测试和因果解释一起失真。

## 支配原则
一个 policy 函数最容易获得信任的时刻，是它的结果成为可见输入的数学推论。隐藏的 effect 扩大了输入集合却没有扩大签名：同一次调用，明天、在另一台机器上、或在另一个测试之后，可能意味着不同的事。于是 replay、audit 与局部推理因为同一个原因失效——函数拥有它拒绝命名的依赖。

## 何时触发
- domain rule 内部直接 `now()`、random、读 env、查 DB、HTTP、filesystem；
- policy 读取 mutable singleton / process global 决定业务结果；
- test 必须 monkey-patch ambient world 才能让 core 可控；
- 一次 domain decision 中“观察世界”和“解释事实”混成一个不可拆函数。

## 不应触发
- shell/adapter 的职责本来就是观察外界，然后把事实交给 core；
- logging/metrics 只观察已完成的结果、不会反过来改变决定；
- core 收到明确 port 但 port 本质仍是“去外面查事实”，需要结合语义判断；不是见 interface 就自动纯；
- 有些 policy 的业务语义确实要求多次实时观察，此时可显式注入 clock/query capability，而不是假装输入不存在。

## 与相邻规则区分
`time-source-in-logic`、`random-source-in-logic` 是特定隐藏输入；`mixed-side-effect-boundaries` 是多个 effect law 缠在同一 owner。这里更根本：policy 自己承担了 observation。

## 判定程序
问：**要完整解释这个决定，除了函数参数，我还需要说“当时数据库/时钟/env/网络刚好是什么”吗？**

如果需要，而这些事实没有成为显式输入或明确 effect boundary，core 在隐瞒自己的 premise。

## 例子
- 正例：`isEligible(user)` 内部读当前时间和 feature flag service。
- 近邻：shell 读取 `asOf` 与 `FeaturePolicy` snapshot，再调用 `isEligible(user, asOf, policy)`。
- 反例：domain function 发 metrics 记录已算出的 verdict，但 metrics 成败不影响 verdict。

## Nudge
纯 core 的价值不是宗教洁癖，而是**让决定的理由可以被列举、保存、重放和反驳**。

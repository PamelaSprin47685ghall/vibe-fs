# implicit-convention-magic — Enforcer

Implicit convention 变成 magic，是因为 correctness 依赖 filename、path、annotation、reflection、discovery order、placement 等 ambient ritual，而 call site/type system 都看不见这份 contract。

隐藏 convention 本质上是一种“以缺席为语法的 API”：你不显式 register handler，而是把文件放对目录；不声明 capability，而是起对名字；不连接 route，而是希望 scanner 发现 annotation。做对时什么都不发生，做错时往往也是**什么都不发生**。

这种 silent omission 最危险。Compile 没红、startup 可能没红、feature 只是悄悄消失，于是 architecture 的一部分只能住在人的记忆里。

以下情形触发：

- filename suffix 决定 handler/route 是否参与；
- directory placement 决定 runtime capability；
- reflection/annotation scanning 漏一个标记就静默缺功能；
- registration/discovery order 决定 behavior，但没有显式 model；
- 新 contributor 只有“知道规矩”才能让 component 生效；
- convention violation 最早只能从 runtime absence 发现。

不要误杀所有 convention。Convention 可以是很好的 ergonomic sugar，前提是背后有显式 checked model：build/startup 会验证 completeness，错误有 owner、有 precise message。目录仅用于 human navigation，也完全没问题。

与 `implicit-control-flow` 区分：那里隐藏的是 **when / happens-before**；本规则隐藏的是 **who participates / how configured**。与 `missing-architecture-gate` 区分：后者是已声明 boundary 没机械 enforcement；本规则是 contract 本身主要以 ambient convention 存在。

判定问题：一个新 participant 如何加入？如果真正步骤是“起一个符合 regex 的名字、放某目录，然后祈祷 scanner 看见”，而没有任何 explicit registry/schema 能证明它被纳入，magic 已经拥有 correctness。

> Convention 可以省语法，但不能省掉错误信号。看不见的配置如果还能静默失效，就是 architecture folklore。
# missing-architecture-gate — Enforcer 中文版

## 定义

关键 architecture rule 若机器可以廉价、确定地判断，却长期只靠 reviewer 记住，就是 missing architecture gate。问题不是“CI 规则越多越好”，而是**本可机械拒绝的 forbidden state 被委托给未来注意力**。

## 支配原则

没有 enforcement 的 invariant 是对未来注意力的请求。Architecture 尤其脆弱，因为违规在局部很方便：import 一个 internal module 常常让当前任务更容易，同时不可见地恶化全局 graph。静态 gate 把负担从记忆移到机制，让 forbidden state 在被引入的那一刻就昂贵，而不是等到日后 redesign 才昂贵。

## 何时触发

- forbidden import/layer edge 每次靠人工 review 抓；
- ownership/generated-file rule 有精确 structural predicate，却没有 standard check；
- 同一违规反复出现，原因只是局部写法很方便；
- architecture statement 已稳定，但 repository 对违反保持沉默。

## 不应触发

- 规则本质是高语义判断，静态扫描会大量 false positive；
- 一次性 review 意见还没成为 standing invariant；
- build/type system 已经机械禁止；
- 纯风格偏好无架构后果。

## 与相邻规则区分

`missing-invariant-documentation` 缺少规则的持久陈述；`false-gate` 有一个实际不会失败的 check。Tie-break：如果 invariant 已知且可机械判定、却交给纪律，用本规则；如果缺的是措辞，用 `missing-invariant-documentation`；如果 check 存在但无效，用 `false-gate`。

## 判定程序

能否写出一个便宜、deterministic、low-noise 的 predicate 来识别违反？能，而 merge pipeline 不执行它，就是把机器能做的工作留给记忆。

## 例子

- positive：reviewers 反复抓跨包 `internal/` imports，但 CI 没有 import-graph check。
- near-miss：边界是“不要把 domain meaning 泄漏进 adapters”，没有廉价静态 check 能判定。
- counterexample：既有 dependency-cruiser rule 已在 CI 运行的同一命令里让 forbidden edges 失败。

## Nudge

Architecture 最脆弱的地方恰好是违规通常“局部更方便”。能让机器拒绝的边界，不要要求所有未来人永远自律。
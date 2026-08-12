# god-module — Enforcer

## 定义
God module 不是“大文件”。它是一个 module 因为 centralize 很方便，而吞下了**多个彼此独立的 sovereignty**——policy、state、effect、resource 或 domain responsibility，它们本来可以因为不同 reason 独立变化。

Size 可能把问题照出来。真正判罪的是 ownership。

## 支配原则
只有当一个 invariant 真正需要 joint ownership 时，code 才应该 colocate。不能因为“大家都来这里比较方便”就共用一个 sovereign。

一个从同一 grammar 生成的 900 行 parser 可以完全 coherent。一个只有 120 行的 `Service`，如果同时决定 authorization、persistence policy、retry behavior、cache invalidation、billing rule、telemetry semantics 与 deployment toggle，它已经是 god module，即使一屏能看完。

病灶是 sovereignty collapse：独立的 reasons-to-change 被迫经过一个 owner，于是 unrelated decisions 被迫共享 state、tests、dependencies 与 lifecycle。

## 何时触发
当一个 module 拥有多个并不需要共同 invariant 的 responsibilities 时触发。典型迹象：

- unrelated policy decisions 因为“central coordinator 在这里”而全在同一 module 决定；
- module 同时 import 多种 infrastructure resources，又拥有各自的 domain policy；
- 不同 team/feature 经常修改不同区域，却从不触碰同一个 invariant；
- test 一个 responsibility 必须先构造一大堆 unrelated fixtures；
- independent resources 的 mutable state/lifecycle 被一起存放；
- unrelated domains 的 errors 在一个 giant switch 中统一 normalize/handle；
- 一个 `manager/service/runtime/context` object 逐渐成为几乎所有 capability 的入口；
- extract 某个 responsibility 困难，主要因为 central module 让任何东西都能访问任何东西；
- 同一个 module 同时扮 scheduler、repository、policy engine、cache owner、protocol adapter、event publisher。

## 不应触发
- Module 很大，只因为一个 coherent algebra/protocol/state machine 真有很多 cases。
- Generated/declarative tables 很长，但只有一个 owner 与一个 reason-to-change。
- Composition root 知道很多 dependencies，但不拥有它们的 policy；“构造系统”本身是独立 responsibility。
- Facade 暴露 coherent subsystem 的多个 operation，而 internal policy 仍有 owners。
- Transaction boundary 为 atomicity 真正协调多个 effects；atomicity 本身就是 invariant。
- 少量 closely related responsibilities 必须一起变化，才能保持同一 contract 成立。

## 与相邻规则区分
`generic-helper-bucket` 是 ownerless odds-and-ends 的累积；god module 通常反而**权力太多**，不仅 helper 多。

`incidental-complexity-dominates` 更广，也可能来自 layers 太多而不是 centralization 太强。`boundary-collapse` 抓 boundary 没保护 distinction；这里是一个 boundary/owner 把多个 independent domains 全吞了。

## 判定程序
不要先数 functions，先列出 module 的 decisions 与 state。

对每一项问：

- 它保护哪个 invariant？
- 什么 event/requirement 会让它改变？
- 它拥有哪个 resource/effect lifecycle？
- 哪些其他 items 必须因为**同一个 domain reason**与它一起改变？

只把答案相同的东西聚在一起。

如果剩下多个独立 clusters 只是因为 convenience 共住一个 module，无论 line count 多小，它都已经 god-like。

## 例子
- positive：`AppRuntime` 用一个 mutable object 同时拥有 session state、auth policy、retry strategy、Git operations、PTY processes、cache、billing limits、review logic。
- positive：`UserService` 同时验证 business policy、执行 SQL、发 email、写 audit event、管理 cache TTL、选择 HTTP status。
- positive：150 行 module 里有四个 unrelated mutable dictionaries，各自生命周期/consumer 完全不同。
- near-miss：900 行 generated parser 只实现一个 grammar，并由一个 source regenerate。
- near-miss：composition root 构造很多 resources，但所有 decision 立即 delegate 给 owners。
- counterexample：transaction coordinator 同时碰 storage 与 event publication，因为 atomic commit/order 正是它唯一拥有的 invariant。

## Nudge
大不是罪。

罪在于让互不相关的真相，都向同一个 sovereign 宣誓效忠。

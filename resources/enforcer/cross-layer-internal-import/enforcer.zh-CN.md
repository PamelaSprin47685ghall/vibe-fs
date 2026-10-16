# cross-layer-internal-import — Enforcer

## 定义

Cross-layer internal import 是 boundary collapse 最容易被 source code 直接看见的一种：一个 layer 把另一个 layer **没有承诺为 public contract 的实现成员**当成正式 API。

问题不在 import syntax，而在 dependency 的含义。Owner 本来说“这些 internals 我可以独立改”，foreign layer 一 import，就悄悄取消了这份独立性。Private structure 变成 de facto API，却没有版本、contract review、semantic owner，也不会在 architecture diagram 里被承认。

典型泄漏：

- application import infrastructure 的 internal storage record；
- higher layer 直接引用 generated runtime detail，而不是 published facade；
- domain 知道 Host/provider SDK 的 private object shape；
- 一个 package 从另一个 package 的 `internal/`、`impl/`、generated file path 深处拿 helper；
- test support 作为 production dependency 反向进入真实层；
- foreign code 根据 private union case/field 做 policy branch。

## 支配原则

Encapsulation 是关于“什么可以独立改变”的承诺。一条 internal import 静默取消这份承诺：private structure 变成没有 review、versioning、ownership 的 de facto API。Dependency graph 于是低估了真实架构，因为决定谁知道什么的不是 declared design，而是 source visibility。

## 何时触发

不要把“public”理解成语言修饰符。一个 `export`/`public` symbol 可能只是技术可见，并不代表 semantic owner 承诺它是跨层 contract。反过来，某个 generated public entry 如果明确就是 owner 发布的兼容 surface，即使文件位于 generated path，也可以合法依赖。

## 不应触发

- 导入的 surface 是明确 public、语义稳定、且被 owner 当作这些 layer 间受支持 contract 拥有；
- import 停留在同一 module 的 private implementation 内，从不跨 declared layer boundary；
- 同一 module 拥有的 white-box test 把 internals 当作该 owner contract 的一部分检查，而不是作为 foreign layer dependency；
- generated code 只通过 owner 发布为 contract 的 generated public entry 消费，即使文件位于 generated path。

## 与相邻规则区分

与 `boundary-collapse` 区分：后者是整个 context isolation 的更广问题；本规则专打**dependency edge 本身越过 public contract**。与 `context-model-leak` 区分：一个 master model 即使通过“正式 public”入口共享，也可能语义上仍泄漏；本规则只判断 internal/public ownership。

## 判定程序

一个很实用的问题：**Owner 能不能在不发 breaking-change notice 的情况下删除/重命名这个 imported member？** 如果理论上能、实际上 foreign layer 会炸，那你已经把 internal 变成了未经治理的 contract。

还要注意 generated/runtime internals。它们往往最容易被“反正能 import”利用，但 compiler output name、DU representation、private SDK shape 恰恰是最不该成为手写业务依赖的东西。

## 例子

- positive：application service import 一个从未发布为 contract 的 persistence 模块 table-layout helper。
- near-miss：同一 service 依赖 persistence owner 声明为 public 且稳定的 repository port。
- counterexample：把需要的事实搬进 declared public contract，或把 behavior 搬到已经拥有 internal knowledge 的 layer。

## Nudge

> 可见性只说明代码能不能碰到；contract 才说明代码有没有资格依赖。不要把文件系统暴露误认为架构授权。
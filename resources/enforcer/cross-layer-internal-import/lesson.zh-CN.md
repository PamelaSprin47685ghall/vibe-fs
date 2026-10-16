# cross-layer-internal-import — Lesson

## 评价

### 定义

Cross-layer internal import 是 boundary collapse 最容易被 source code 直接看见的一种：一个 layer 把另一个 layer **没有承诺为 public contract 的实现成员**当成正式 API。

问题不在 import syntax，而在 dependency 的含义。Owner 本来说“这些 internals 我可以独立改”，foreign layer 一 import，就悄悄取消了这份独立性。Private structure 变成 de facto API，却没有版本、contract review、semantic owner，也不会在 architecture diagram 里被承认。

典型泄漏：

- application import infrastructure 的 internal storage record；
- higher layer 直接引用 generated runtime detail，而不是 published facade；
- domain 知道 Host/provider SDK 的 private object shape；
- 一个 package 从另一个 package 的 `internal/`、`impl/`、generated file path 深处拿 helper；
- test support 作为 production dependency 反向进入真实层；
- foreign code 根据 private union case/field 做 policy branch。

### 支配原则

Encapsulation 是关于“什么可以独立改变”的承诺。一条 internal import 静默取消这份承诺：private structure 变成没有 review、versioning、ownership 的 de facto API。Dependency graph 于是低估了真实架构，因为决定谁知道什么的不是 declared design，而是 source visibility。

### 何时触发

不要把“public”理解成语言修饰符。一个 `export`/`public` symbol 可能只是技术可见，并不代表 semantic owner 承诺它是跨层 contract。反过来，某个 generated public entry 如果明确就是 owner 发布的兼容 surface，即使文件位于 generated path，也可以合法依赖。

### 不应触发

- 导入的 surface 是明确 public、语义稳定、且被 owner 当作这些 layer 间受支持 contract 拥有；
- import 停留在同一 module 的 private implementation 内，从不跨 declared layer boundary；
- 同一 module 拥有的 white-box test 把 internals 当作该 owner contract 的一部分检查，而不是作为 foreign layer dependency；
- generated code 只通过 owner 发布为 contract 的 generated public entry 消费，即使文件位于 generated path。

### 与相邻规则区分

与 `boundary-collapse` 区分：后者是整个 context isolation 的更广问题；本规则专打**dependency edge 本身越过 public contract**。与 `context-model-leak` 区分：一个 master model 即使通过“正式 public”入口共享，也可能语义上仍泄漏；本规则只判断 internal/public ownership。

### 判定程序

一个很实用的问题：**Owner 能不能在不发 breaking-change notice 的情况下删除/重命名这个 imported member？** 如果理论上能、实际上 foreign layer 会炸，那你已经把 internal 变成了未经治理的 contract。

还要注意 generated/runtime internals。它们往往最容易被“反正能 import”利用，但 compiler output name、DU representation、private SDK shape 恰恰是最不该成为手写业务依赖的东西。

### 例子

- positive：application service import 一个从未发布为 contract 的 persistence 模块 table-layout helper。
- near-miss：同一 service 依赖 persistence owner 声明为 public 且稳定的 repository port。
- counterexample：把需要的事实搬进 declared public contract，或把 behavior 搬到已经拥有 internal knowledge 的 layer。

## 建议

### 现在该做什么

把 dependency 拉回 owner 正式发布的 contract。

先别急着“换 import path”。问 foreign layer 真正需要的是什么：query、command、stable value、event、capability、adapter result，还是只为了方便拿了一个 internal helper？让 owner 发布**最小 semantic surface**，然后删掉对 private representation 的依赖。

### 为什么重要

Internal imports 制造架构文档不记录的义务。Provider 看起来可以自由 refactor，其实不能；consumer 看起来只依赖 interface，其实知道 storage layout、helper structure 或 lifecycle detail。这是没有诚实 contract 的耦合。

### 修复策略

常见 repair：

- internal storage row → owner query/result type；
- generated Fable/runtime internals → 稳定 facade/export；
- private helper → 要么搬到真正 owner，要么把需要的能力正式做成 public contract；
- SDK/provider object shape → adapter decode 成 application/domain type；
- foreign union case inspection → owner 返回 caller 真正需要的 closed outcome；
- production import test support → 把共同逻辑移到真正 production owner，test support 只消费它。

不要通过 re-export 私有成员来“修复”。如果 `PublicFacade` 只是 `export * from ../internal/*`，foreign knowledge 一点没减少，只是 import path 漂亮了。

也不要为了消除一条 internal import 造一层 forwarding-only translator；如果新层不改变 knowledge/representation/authority，会落入 `translator-layer-bloat`。

### 决策分支

- 如果 consumer 需要 provider 已拥有的稳定 fact，发布承载该 fact 的最小 public contract，删除 internal import。
- 如果 consumer 需要 implementation detail 来执行 policy，ownership 可能放错了；把 policy 搬到 owning layer，而不是把 internals 向外泄漏。
- 如果 import 是同层 private wiring，留下；本规则不管 intra-module structure。

### 常见假修复

常见假修复：

- 给 internal symbol 加 `public` / export，就说 contract 已建立；
- 复制 private type 到 consumer，然后两边靠手工同步 shape；
- 关闭 architecture lint，让 import “合法”；
- 通过 reflection/dynamic import 绕过静态 dependency，概念上的越界仍然存在；
- test facade 直接暴露 compiler-emitted internals，导致 compiler upgrade 变成业务 breaking change；
- 把所有 shared internals 塞进 `common` package，原 ownership 问题只是换了地址。

### 验证

验证应做 owner-side refactor：保持 declared contract 完全不变，重命名 internal module、换 storage representation、升级 compiler-generated shape、移动 helper。Foreign layer 应继续编译/运行。

再做 dependency scan：跨 layer 的 imports 只应指向正式 supported entry，禁止 deep path/private generated detail。机械 gate 可以保护这条**代码边界**，因为它检查的是 executable dependency，不是 free-form prose。

如果 foreign layer 真正需要某个 internal fact 才能正确工作，别把它叫 internal；重新设计 contract 并明确 owner。这比长期偷偷 import 更诚实。

### 完成条件

完成时，source dependency graph 与 architecture 声称的 boundary 一致；private change 不再通过 hidden import 产生远距离破坏。

> 内部细节一旦有人必须依赖，就已经不是“细节”；要么收回依赖，要么正式承担 contract 责任。

### Nudge

> 可见性只说明代码能不能碰到；contract 才说明代码有没有资格依赖。不要把文件系统暴露误认为架构授权。

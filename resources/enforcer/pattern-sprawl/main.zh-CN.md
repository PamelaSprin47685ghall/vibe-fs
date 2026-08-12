# pattern-sprawl — Main

## 现在该做什么
先命名这个 pattern 真正要提供的 semantic capability，再把 machinery collapse 到 host language 中能够完整保留该 capability 的最简单 construct。

不要为了显得聪明而删 indirection。只有当 direct form 更清楚、且不丢真实 extension/lifecycle/durability/authority boundary 时，才删。

## 为什么重要
Pattern sprawl 会让代码看起来“architecture 很有意图”，却隐藏很多 layer 实际没有做 semantic work。

Visitor、element、strategy、context、command、handler、factory、provider、builder、director、mediator——这些词对应真实 responsibility 时很有用；只是在复刻 language feature 时，maintainer 必须先把 pattern 反编译回 underlying idea，才能开始 reasoning。

结果是 abstraction latency：一个简单 closed choice 要跨几个文件、几次 dynamic call 才看明白。Refactor 也越来越难，因为 scaffolding 自己开始拥有 tests/consumers，最后反过来为自己的存在辩护。

## 修复策略
逐类处理：

- **closed variants：**repository 拥有完整 case set 时，优先 algebraic data / enum + exhaustive match；
- **stateless strategies：**没有 independent identity/lifecycle 时，优先 first-class functions / small modules；
- **construction：**优先 immutable constructor / smart constructor / type；只有 staged/complex construction 真实存在时保留 builder；
- **factory：**selection local/static 时直接 constructor；runtime discovery/boundary substitution 真实存在时保留 factory/registry；
- **command：**ephemeral local action 用 function/call；有 durable identity、queue/replay/audit/undo 时保留 command object；
- **visitor：**closed/owned hierarchy 用 direct match/traversal；external stable hierarchy + independently varying operations 时保留 visitor；
- **mediator/event：**direct causality 用 direct dependency；真实 temporal/distributed decoupling 才用 messaging。

删除只为维护 obsolete pattern choreography 而存在的 interface/class/registration/test。

## 决策分支
- **Pattern 真买 open runtime extension：**保留并 test extension contract。
- **Pattern 真买 durability/serialization/queueing：**保留 first-class message semantics。
- **Language 有 closed/exhaustive form：**case set 确实 owned/closed 时 collapse inheritance/visitor ceremony。
- **唯一理由是 mocking：**注入真实 effect/capability boundary，不要给每个 pure class 造 abstraction。
- **未来 implementations 只是想象：**别提前付 abstraction cost；independent variation 真的出现再 extract。
- **Pattern vocabulary 已是 public/external contract：**改变它可能是 compatibility 任务，不是 local cleanup。

## 常见假修复
- 用一个流行新 pattern 替换旧 pattern，moving parts 一个没少。
- Classes 改 functions，却保留已经失去作用的 registry/factory/mediator。
- 因“functions 更简单”删掉真实 runtime extension point。Simplicity 不能抹掉真实 capability。
- 造 generic combinator framework，让“simplified”版本比原来更抽象。
- 为减少 types 把 closed states 塌成 strings/dictionaries。Direct 不等于 untyped。
- 每个 interface 只有一个 implementation、独立 substitution 已消失，仍“为了 consistency”永久保留。

## 验证
比较前后 semantic surface：

- 所有 valid variants 仍能表达；
- invalid states 没变得更容易创建；
- required runtime extension/durability 仍保留；
- control flow 更 explicit，而不是只是搬家；
- reader 理解 domain law 前需要学习的 concepts 更少；
- tests 保护 behavior/contract，不再保护 removed pattern choreography。

Invariant：

> 每一层 surviving indirection，都买到了 direct host-language form 无法同样清楚提供的 capability。

## 完成条件
Code 先命名 problem，再命名 pattern。

Reader 不需要先在脑中反编译 architecture diagram，才能理解 semantic law。

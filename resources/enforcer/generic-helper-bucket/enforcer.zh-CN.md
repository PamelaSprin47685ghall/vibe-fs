# generic-helper-bucket — Enforcer

## 定义
Generic helper bucket 是一种 module：它唯一的 organizing principle 是**没有 ownership**。

`utils`、`helpers`、`common`、`misc`、`core` 这些名字本身不自动有罪。真正触发规则的是：你问“为什么这段 code 放这里”，答案是“因为别处都不太合适”。于是 bucket 变成 architecture junk drawer——任何 orphan 都能塞，因为名字没有 exclusion rule。

## 支配原则
一个好 module 必须有能力说 **no**。

好的 module 由 concept、boundary、invariant 或 capability 组织。这个 organizing principle 让 maintainer 能预测 code 应该放哪里、哪些 dependency 合法。

Generic bucket 没有这种约束力。先来 string formatting，再来 path logic，再来 retry policy、JSON helper、database glue、domain-specific predicate。因为所有 orphan 都“符合 common”，fan-in 越来越大，semantic cohesion 越来越小。

真正 defect 不是命名审美，而是 ownership decision 被无限延期，最后拿一个 location 冒充 answer。

## 何时触发
当 generic module 因为没有 semantic owner 而累积 unrelated responsibilities/dependencies 时触发。常见迹象：

- 文件里的 functions 如果诚实名名，明显应分别属于不同 domain/boundary；
- 每加一个 helper，就给 bucket 引入一个新的 unrelated dependency；
- 很多 modules 都依赖 bucket，一个 helper change 导致大范围 coupling/rebuild，尽管语义无关；
- helper 反向调用 higher-level domain modules，形成 cycle/inverted dependency；
- bucket tests 是毫无共同 invariant 的杂物清单；
- maintainer 经常问“这东西放哪”，默认答案永远是 “utils”；
- supposedly reusable helper 实际编码一个 product/domain convention，却被叫 generic；
- rushed change、migration、AI-generated patch 最爱把暂时不好归属的东西塞进这里。

## 不应触发
- Module 表达一个真正窄且跨 domain 通用的 technical concept，例如 UTF-8 byte operation、stable hashing primitive、pure collection combinator、well-defined platform shim。
- `common` package 本身就是 deliberate versioned public product，有 explicit scope/ownership。
- Small local helper file 紧贴一个 owner，只装它的 implementation details。
- 名字很差，但内容实际只有一个 clear invariant/dependency boundary；这时 rename 可能已经足够。
- Shared extraction 会制造两个 otherwise independent domains 的 false coupling，因此故意保留少量 duplication。

## 与相邻规则区分
`god-module` 往往已经拥有多个 unrelated sovereignty，包括大量 policy/effect。Generic helper bucket 可以更早、更机械；核心病灶是 ownerless accumulation。

`dependency-bloat` 主要看不必要 external packages。Helper bucket 即使没有新 package，也能制造严重 internal dependency bloat。

`duplicated-control-flow` 可能诱发 shared extraction，但不要为了消重复造 bucket；extracted logic 必须先有一个真实 concept 可以拥有它。

## 判定程序
对每个 exported helper，完成这句话：

> 这个 operation 属于 ___，因为如果没有它，___ 的 invariant/contract 就不完整。

如果答案指向不同 domains/boundaries，bucket 就不是 coherent owner。

再问：什么 rule 能阻止下一条 unrelated helper 被加进来？如果没有 semantic exclusion rule，这个 module 从结构上就会继续 sprawl。

## 例子
- positive：`utils.ts` 同时放 currency rounding、HTTP retry、SQL escaping、feature-flag parsing、slug generation、account authorization。
- positive：`common.fs` 同时 import domain types 与 infrastructure SDKs，结果几乎每层都依赖它，它又反向依赖多层。
- positive：每次 incident 后 `helpers.js` 都长一批“temporary” recovery function，因为它们没有别的家。
- near-miss：`Utf8.fs` 只做 byte/string conversion，没有 domain knowledge，被多个 adapter 使用。
- counterexample：两个 domains 故意保留相似 local formatting logic，因为抽 common abstraction 会制造 false coupling。

## Nudge
Junk drawer 很方便，因为每个 orphan 都放得进去。

Architecture 开始于 code 有一个真正**属于它的地方**，而不是随便有地方能塞。

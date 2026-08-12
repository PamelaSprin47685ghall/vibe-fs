# incidental-complexity-dominates — Enforcer

## 定义
当一个本来简单的 domain fact，需要穿过比它自身更难理解的 machinery 才能被理解或修改时，incidental complexity 已经反客为主。

问题不是“文件多”或“代码长”，而是**semantic displacement**：wrapper、adapter、flag、configuration、lifecycle glue、serializer、registry、compatibility path、orchestration、framework ceremony 变成工程师真正需要学习的“业务”，原本的 domain rule 反而只剩脚注。

## 支配原则
Essential complexity 是现实不允许你删掉的部分。Accidental complexity 是 representation 自己发明出来的部分。

Payment system 可能真的需要 idempotence、durable state、authorization 与 partial-failure handling。但这并不自动意味着它需要六次 DTO translation、三个用来重新拼同一 state 的 lifecycle flag、两套并行 config surface，或一个只负责寻找“类型系统本来已经能直接命名的代码”的 registry。

病真正开始于：implementation 发明出来的 ontology，比问题本身的 ontology 还丰富。

## 何时触发
当 maintainer 的主要 reasoning budget 花在 solution-imposed machinery，而不是 domain distinction 上时触发。常见迹象：

- 一个 domain action 穿过多层 wrapper，但这些层没有增加 authority、persistence、isolation 或 meaningful contract；
- 同一个 fact 在单一 trust boundary 内被反复转换成几乎一样的 shape；
- lifecycle/status flags 只是重新构造 durable truth 已经知道的事实；
- configuration 主要为了参数化内部 indirection，而没有独立 consumer；
- 一个很小 behavior change 要同步修改很多 plumbing layer，却没有对应 semantic change；
- control flow 只能通过 registration、callback、factory、generated binding、side table 才找得到；
- compatibility/migration/“temporary” path 已经没有任何 named external consumer，仍继续存在；
- test 花在 framework setup 上的认知成本远高于它真正要区分的 behavior。

## 不应触发
- Complexity 保护真实 boundary：process isolation、authority、persistence、external protocol、independent deployment、failure containment，或 collapse 后会丢失的其他 consequence。
- Domain 本身真的复杂；很多 states/files 可能正是最简单诚实的表示。
- Boundary 两侧确实拥有不同 model，translation 本身就是 semantic work。
- External platform 强迫存在某些 ceremony，但这些 ceremony 被局部封装在窄 adapter，而没有扩散进 core。
- Code 只是 verbose，但 conceptual path 非常直接。字多本身不是 accidental complexity。

## 与相邻规则区分
`framework-tax` 是常见子类：framework lifecycle/configuration 成了主 ontology。`translator-layer-bloat` 专门抓 repeated shape conversion。`facade-hides-mess` 是用漂亮入口盖住复杂度，却没有减少它。`god-module` 则把互不相关的 sovereignty 塞进一个 owner。

当核心观察更广——“solution 自己的 machinery 已经比 problem 更难理解”——用本规则。

## 判定程序
先用普通技术语言描述 domain operation，然后列出 correctness 真正需要的最小 facts：owners、states、effects、durable facts、failure boundaries。

再沿 implementation 逐层问每个 layer/state/translation：

> 如果删掉这个 mechanism，会消失哪一个真实 distinction？

如果答案是“没有，另一层已经知道同一件事”“framework pattern 要求这样”“以后也许用得上”，就是 accidental complexity。

## 例子
- positive：修改一个 user preference 要经过 Controller DTO → Service DTO → Command DTO → Domain DTO → Persistence DTO，字段完全相同，也没有 boundary-specific validation。
- positive：持久化 `started/completed/published` 三个 booleans，而一个 durable state/event 已能 deterministic 推导三者。
- positive：改一条 validation rule 需要同时改 registry metadata、factory、adapter、facade、mapper、handler 与 duplicate schema，却没有独立 owners。
- near-miss：external wire DTO 只在 ingress 转一次 strong domain type，因为 wire contract 与 domain model 确实不同。
- counterexample：distributed workflow 拥有多个 explicit states，因为 crash recovery 真的必须区分它们。

## Nudge
如果 machinery 比 invariant 更容易被人记住，machinery 已经变成了产品。

让 representation 重新被真实问题支配。

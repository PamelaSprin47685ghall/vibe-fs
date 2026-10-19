# incidental-complexity-dominates — Lesson

## 评价

### 定义

当一个本来简单的 domain fact，需要穿过比它自身更难理解的 machinery 才能被理解或修改时，incidental complexity 已经反客为主。

问题不是“文件多”或“代码长”，而是**semantic displacement**：wrapper、adapter、flag、configuration、lifecycle glue、serializer、registry、compatibility path、orchestration、framework ceremony 变成工程师真正需要学习的“业务”，原本的 domain rule 反而只剩脚注。

### 支配原则

Essential complexity 是现实不允许你删掉的部分。Accidental complexity 是 representation 自己发明出来的部分。

Payment system 可能真的需要 idempotence、durable state、authorization 与 partial-failure handling。但这并不自动意味着它需要六次 DTO translation、三个用来重新拼同一 state 的 lifecycle flag、两套并行 config surface，或一个只负责寻找“类型系统本来已经能直接命名的代码”的 registry。

病真正开始于：implementation 发明出来的 ontology，比问题本身的 ontology 还丰富。

### 何时触发

当 maintainer 的主要 reasoning budget 花在 solution-imposed machinery，而不是 domain distinction 上时触发。常见迹象：

- 一个 domain action 穿过多层 wrapper，但这些层没有增加 authority、persistence、isolation 或 meaningful contract；
- 同一个 fact 在单一 trust boundary 内被反复转换成几乎一样的 shape；
- lifecycle/status flags 只是重新构造 durable truth 已经知道的事实；
- configuration 主要为了参数化内部 indirection，而没有独立 consumer；
- 一个很小 behavior change 要同步修改很多 plumbing layer，却没有对应 semantic change；
- control flow 只能通过 registration、callback、factory、generated binding、side table 才找得到；
- compatibility/migration/“temporary” path 已经没有任何 named external consumer，仍继续存在；
- test 花在 framework setup 上的认知成本远高于它真正要区分的 behavior。

### 不应触发

- Complexity 保护真实 boundary：process isolation、authority、persistence、external protocol、independent deployment、failure containment，或 collapse 后会丢失的其他 consequence。
- Domain 本身真的复杂；很多 states/files 可能正是最简单诚实的表示。
- Boundary 两侧确实拥有不同 model，translation 本身就是 semantic work。
- External platform 强迫存在某些 ceremony，但这些 ceremony 被局部封装在窄 adapter，而没有扩散进 core。
- Code 只是 verbose，但 conceptual path 非常直接。字多本身不是 accidental complexity。

### 与相邻规则区分

`framework-tax` 是常见子类：framework lifecycle/configuration 成了主 ontology。`translator-layer-bloat` 专门抓 repeated shape conversion。`facade-hides-mess` 是用漂亮入口盖住复杂度，却没有减少它。`god-module` 则把互不相关的 sovereignty 塞进一个 owner。

当核心观察更广——“solution 自己的 machinery 已经比 problem 更难理解”——用本规则。

### 判定程序

先用普通技术语言描述 domain operation，然后列出 correctness 真正需要的最小 facts：owners、states、effects、durable facts、failure boundaries。

再沿 implementation 逐层问每个 layer/state/translation：

> 如果删掉这个 mechanism，会消失哪一个真实 distinction？

如果答案是“没有，另一层已经知道同一件事”“framework pattern 要求这样”“以后也许用得上”，就是 accidental complexity。

### 例子

- positive：修改一个 user preference 要经过 Controller DTO → Service DTO → Command DTO → Domain DTO → Persistence DTO，字段完全相同，也没有 boundary-specific validation。
- positive：持久化 `started/completed/published` 三个 booleans，而一个 durable state/event 已能 deterministic 推导三者。
- positive：改一条 validation rule 需要同时改 registry metadata、factory、adapter、facade、mapper、handler 与 duplicate schema，却没有独立 owners。
- near-miss：external wire DTO 只在 ingress 转一次 strong domain type，因为 wire contract 与 domain model 确实不同。
- counterexample：distributed workflow 拥有多个 explicit states，因为 crash recovery 真的必须区分它们。

## 建议

### 现在该做什么

让 domain operation 重新变得可见。

找出系统真正必须保留的少数 distinctions——ownership、state、authority、persistence、failure、external contract——然后 collapse 那些没有保护其中任何一项的 machinery。不要用“再包一层 abstraction”来解决 accidental complexity。

### 为什么重要

Incidental complexity 会对每一次未来 change 收利息。

Redundant layer 不是只付一次成本。每个 maintainer 都要学它；tests 要 mock 它；refactor 要迁它；schema change 要同步它；incident 要 debug 它；最后大家还会因为“不确定它是不是 secretly important”而不敢删它。

最终组织会失去一种关键能力：分辨“问题本身要求的东西”与“我们之前设计出来的东西”。到了那一步，architecture 开始自我证明：系统之所以复杂，是因为 architecture 很复杂；architecture 之所以不能简化，是因为整个系统已经围着它长歪了。

### 修复策略

从 semantic inventory 开始，不要从移动文件开始：

1. 命名 domain action 与 externally visible promise；
2. 命名真实 owners 与 durable facts；
3. 命名确实需要 isolation 的 effects/failure boundaries；
4. 标出 path 上每一个 translation、wrapper、flag、registry、adapter、facade、lifecycle object；
5. 对每个 mechanism 说出它唯一保护的 invariant/boundary；
6. merge/delete 那些答案重复、历史遗留或纯 ceremony 的机制；
7. 让 tests 保护 surviving semantic boundaries，而不是旧 plumbing。

在一个 ownership boundary 内优先只用一个 representation。只在真实 ingress/egress 做一次 translation。能够从 durable truth deterministic 导出的 state 不要另设 writer。没有 real dynamic discovery requirement 时，用语言直接引用替代 registration maze。

目标不是 fewer files，而是 fewer independent concepts：让 maintainer 回答“发生什么、为什么”时，需要同时握在脑子里的概念更少。

### 决策分支

- **两层都在做同一个 decision：**选一个 semantic owner，另一层降为 mechanical adapter 或删除。
- **同一 boundary 内有两个近似 representation：**除非各自承载 distinct invariant，否则 collapse。
- **Stored state 可由更 authoritative durable fact 推导：**删除 duplicate writer，改为 derive。
- **Framework boilerplate 无法避免：**把它关在窄 edge，让 domain code 不说 framework ontology。
- **某层保护 real external contract / failure boundary：**保留，并明确命名它拥有的 distinction。
- **复杂度确实来自 domain：**改好 name/type/test，但不要为了 ceremony 少而压扁真实 states。

### 常见假修复

- 在原 layers 外加一个 facade，然后宣布 architecture 简化。
- 把大文件拆成小文件，却完整保留 distributed ownership 与 call graph。
- 自动 generate boilerplate。Generated accidental complexity 仍有 semantic/debugging cost。
- 为了“统一模式”引入 generic framework，让原本 direct code 更间接。
- 再发明一个 canonical DTO，然后要求所有旧 DTO 都先翻译到它。
- 为了“更少 types”删除真实 domain distinctions。丢真相换来的 simplicity 是 corruption，不是 design。
- 用任意 line/file count 作为拆分理由。Size 可以提出问题，不能回答 ownership。

### 验证

选一个代表性 change，对比修复前后的 reasoning path。

在**不丢真实 invariant**的前提下，修复后应需要更少 independent concepts、更少同步 edits。Tests 仍能反驳同样的 public/domain promises；recovery、authority、external boundary 在重要处仍然 explicit。

一个很实用的检查：让 maintainer 不使用 framework/plumbing nouns 解释这条 operation。如果这个解释现在可以直接映射到 code owner 与 state，方向就对了。

Invariant：

> Essential distinctions 保持 explicit；solution 自己发明的 distinctions 不再主宰 mental model。

### 完成条件

Implementation 再次主要是在表达它真正解决的问题。

删掉一层时，你能清楚说明消失了哪一份 semantic burden，而不只是“代码被搬到别处”。

### Nudge

如果 machinery 比 invariant 更容易被人记住，machinery 已经变成了产品。

让 representation 重新被真实问题支配。

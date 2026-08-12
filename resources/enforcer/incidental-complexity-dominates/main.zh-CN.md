# incidental-complexity-dominates — Main

## 现在该做什么
让 domain operation 重新变得可见。

找出系统真正必须保留的少数 distinctions——ownership、state、authority、persistence、failure、external contract——然后 collapse 那些没有保护其中任何一项的 machinery。不要用“再包一层 abstraction”来解决 accidental complexity。

## 为什么重要
Incidental complexity 会对每一次未来 change 收利息。

Redundant layer 不是只付一次成本。每个 maintainer 都要学它；tests 要 mock 它；refactor 要迁它；schema change 要同步它；incident 要 debug 它；最后大家还会因为“不确定它是不是 secretly important”而不敢删它。

最终组织会失去一种关键能力：分辨“问题本身要求的东西”与“我们之前设计出来的东西”。到了那一步，architecture 开始自我证明：系统之所以复杂，是因为 architecture 很复杂；architecture 之所以不能简化，是因为整个系统已经围着它长歪了。

## 修复策略
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

## 决策分支
- **两层都在做同一个 decision：**选一个 semantic owner，另一层降为 mechanical adapter 或删除。
- **同一 boundary 内有两个近似 representation：**除非各自承载 distinct invariant，否则 collapse。
- **Stored state 可由更 authoritative durable fact 推导：**删除 duplicate writer，改为 derive。
- **Framework boilerplate 无法避免：**把它关在窄 edge，让 domain code 不说 framework ontology。
- **某层保护 real external contract / failure boundary：**保留，并明确命名它拥有的 distinction。
- **复杂度确实来自 domain：**改好 name/type/test，但不要为了 ceremony 少而压扁真实 states。

## 常见假修复
- 在原 layers 外加一个 facade，然后宣布 architecture 简化。
- 把大文件拆成小文件，却完整保留 distributed ownership 与 call graph。
- 自动 generate boilerplate。Generated accidental complexity 仍有 semantic/debugging cost。
- 为了“统一模式”引入 generic framework，让原本 direct code 更间接。
- 再发明一个 canonical DTO，然后要求所有旧 DTO 都先翻译到它。
- 为了“更少 types”删除真实 domain distinctions。丢真相换来的 simplicity 是 corruption，不是 design。
- 用任意 line/file count 作为拆分理由。Size 可以提出问题，不能回答 ownership。

## 验证
选一个代表性 change，对比修复前后的 reasoning path。

在**不丢真实 invariant**的前提下，修复后应需要更少 independent concepts、更少同步 edits。Tests 仍能反驳同样的 public/domain promises；recovery、authority、external boundary 在重要处仍然 explicit。

一个很实用的检查：让 maintainer 不使用 framework/plumbing nouns 解释这条 operation。如果这个解释现在可以直接映射到 code owner 与 state，方向就对了。

Invariant：

> Essential distinctions 保持 explicit；solution 自己发明的 distinctions 不再主宰 mental model。

## 完成条件
Implementation 再次主要是在表达它真正解决的问题。

删掉一层时，你能清楚说明消失了哪一份 semantic burden，而不只是“代码被搬到别处”。

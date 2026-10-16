# boolean-blindness — Lesson

## 评价

### 定义

当 `true/false` 被拿来编码一个**有名字的 domain choice**，而不是真正的 binary proposition，导致 vocabulary 恰恰在 caller 做决定的地方消失时，就是 boolean blindness。

多个 flags 合起来表示一个 conceptual state 时更糟。两个 booleans 会制造 4 个 representable combinations；五个会制造 32 个。如果 domain 实际只有 3 个 meaningful modes，剩下 29 个并不是“灵活性”，而是 representation 凭空创造的 fictional states。

问题不是 boolean 存在。`isEmpty`、`hasPermission`、`wasCached` 之类 predicate 本来就应该是 bool。真正 defect 是让 bits 承担比 yes/no 更丰富的 vocabulary。

### 支配原则

Call site 应该直接说出它正在做的选择。

`open(true, false)` 迫使 reader 从 parameter order、editor hint 或 memory 找回 meaning。`open(ReadWriteExisting)` 则把 domain decision 留在 expression 本身。

更重要的是，flag product 会扩大 state space。一旦 illegal combinations 可以被构造，每个 consumer 就必须 defensive check，或暗中相信 constructor 永远守纪律。这不是 flexibility，而是 modeling failure。

所以 boolean blindness 同时是 readability defect，也经常是 correctness defect：它一边擦掉 names，一边制造 possibility。

### 何时触发

当 booleans 编码 mode、phase、policy alternative、permission、result kind 或 mutually constrained state，而这些 meaning 在 domain 中本来有名字时触发。常见迹象：

- caller 直接传 literal `true/false`，必须靠 parameter hint/comment 才知道意义；
- 多个 flags 需要满足 “exactly one”“at most one”“if A then not B” 等规则；
- 新 mode 通过“再加一个 boolean”实现，而不是新增 named case；
- 同一个 flag 的含义依赖 sibling flags；
- persisted record 有 `isRunning/isDone/isFailed/isCancelled` 一串 lifecycle flags；
- feature/policy code 对 boolean tuple branch，而 tuple 实际只对应少数 named modes；
- read/write/admin 等 capability 被分散成 booleans，但只有特定组合合法；
- API 暴露 bool，而 action/mode enum 本可以让 call site self-describing。

### 不应触发

- Value 是真正 predicate，只有两个 semantic outcomes、没有 hidden third state，例如 `isEmpty`、`contains`、`isAuthorized` observation。
- Return boolean 直接回答一个名字已经在 call site 可见的 yes/no question。
- 多个 boolean facts 真正 independent，而且 Cartesian product 全部有 contract meaning。
- Wire/storage 用 bits，但 domain boundary 会立刻构造 named cases/capabilities。
- Boolean 只用于 domain choice 已经完成之后的 representation optimization，不再泄漏回 policy vocabulary。

### 与相邻规则区分

`illegal-state-representable` 是更广的 state-space defect。Booleans 本身正在擦除 named alternatives 时用 `boolean-blindness`；任意 nullable fields/discriminants 组成 impossible product 时用更广规则。

`primitive-obsession` 看 primitives 一般性的 semantic identity；boolean 特别容易在 call site 抹掉 vocabulary、又指数级放大组合，所以本规则更锋利。

仅仅因为 literal 难读而抱怨 “magic boolean” 太浅。规则应当因为 named domain choice / constrained state 被压成 bits 而触发。

### 判定程序

先完全不看 flags，写出真正 semantic alternatives。

对 flag cluster 列完整 truth table，并给每个 combination 标记：

- meaningful named state；
- genuinely independent combination；
- impossible/undefined state。

如果多行实际对应 domain names，或有 rows 根本没有合法 meaning，这个 boolean product 就是错误模型。

对 single boolean parameter 问：“我能不能把 `true/false` 换成两个能够表达真实 policy/mode 的名字？”如果能，优先 named choice。如果它只是回答 function 已经命名清楚的 predicate，保留 bool。

### 例子

- positive：`openFile(path, true, false)` 表示 write=true/create=false；reader 必须看 signature 才懂。
- positive：`{ isRunning, isCompleted, isFailed }` 可以构造 `true,true,true`，而 lifecycle 明明只允许一个 state。
- positive：`send(message, true)` 中 `true` 实际就是 `RequireAcknowledgement`，domain 已经有名字。
- positive：新增 “dry run” 时又加 `isDryRun` 放在 `isWrite` 旁边，开始制造 contradictory combinations。
- near-miss：`collection.isEmpty(): bool` 直接返回 binary observation，没有 vocabulary 被擦掉。
- near-miss：`{ isEncrypted, isCompressed }` 如果四种组合都真实合法，就是 independent facts。
- counterexample：`FileOpenMode = Read | WriteExisting | WriteCreate`，caller 明确选择 named case。

## 建议

### 现在该做什么

把承载 named choice 的 booleans 替换成真正列出这些 choices 的 type。

Flag cluster 则直接建模**合法 state space**，不要继续保留一个巨大 Cartesian product，再要求每个 consumer 记住哪些组合是虚构的。

真正 predicate 继续用 bool。目标是 semantic precision，不是禁用 boolean。

### 为什么重要

Boolean-heavy API 让 meaning 编码很便宜，恢复却很昂贵。

Writer 刚看完 signature，当然知道 `true` 是什么。Reader 几个月后看到 `configure(false, true, false)`，只能去别处重建 vocabulary。IDE 可以显示 parameter name，但 program 仍然允许 literals 被调换、contradictory combinations 被构造。

Flag cluster 更糟。每增加一个 boolean，representable state space 就翻倍，无论 domain 有没有翻倍。Invalid combinations 随后进入 persistence、tests、branching、migration、incident diagnosis，系统开始为用户永远不可能合法产生的 worlds 写 defensive code。

Named sum/enum/capability set 会让 valid alternatives 可见，也让未来变化有明确位置。

### 修复策略

从 domain alternatives 出发，不从现有 flags 出发：

1. 枚举 meaningful modes/states/actions；
2. 判断它们 mutually exclusive、independent，还是 hierarchical；
3. mutually exclusive 用 sum/enum；
4. state-specific data 跟着对应 case；
5. genuinely independent combinations 才用 capability/set model；
6. external wire/storage 必须用 boolean 时，在 boundary translate；
7. 删除 old boolean overload，防止 caller 绕过 named model；
8. 使用 exhaustive matching，让新增 alternative 产生 visible obligation。

Single parameter 如果承载 policy choice，`true/false` 会隐藏 intent，即使只有两个 case，也值得用 named two-case type。

### 决策分支

- **Mutually exclusive lifecycle states：**一个 closed state type，不要多条 `isX`。
- **Two-way policy alternative：**call-site meaning 重要时用 two-case enum/union。
- **Independent capabilities：**如果每个组合都 meaningful，可以用 set/bitset，但 capability 必须有名字。
- **Simple observation/predicate：**保留 `bool`；`isEmpty` 不需要强行 `Empty | NonEmpty`，除非状态后来承载 data/behavior。
- **External protocol 就是 boolean：**ingress decode 成 named domain choice，egress encode 回去。
- **Storage 已存在历史 contradictory flags：**先定义 migration/validation policy，不要 silent reinterpret。

### 常见假修复

- 只把 `flag` rename 成 `isSpecial`，caller 仍到处 literal。
- Comment 写明哪些 combination illegal，type 继续接受。
- 每个新 mode 再加一个 boolean，继续指数爆炸。
- 把 boolean 换成 free-form strings `"read"/"write"`，从 boolean blindness 变 stringly typing。
- “为了 convenience”保留 legacy boolean overload，让新 code 随时绕过 repair。
- 把 genuinely independent binary facts 塞进 giant enum，枚举所有组合。Independence 真实时应使用 named capabilities，而不是人为耦合。

### 验证

同时证明 readability 与 state-space closure：

- 搜索 policy/mode 位置 unexplained boolean literals；
- 尝试构造过去 contradictory flag combinations；
- 新 case 加入时 exhaustive match 应强制 review；
- wire/storage adapter 保持 external compatibility，但不让 bool 泄漏 inward；
- genuinely predicates 仍保持简单，没有 ceremony wrapper。

Invariant：

> Program 能表示的 policy/state choices，正好等于 domain 真正命名的 choices。

### 完成条件

Call site 不靠 editor hints 也能说清自己在做什么；type 不再承认那些唯一 semantics 是“理论上不该发生”的组合。

### Nudge

Boolean 应当回答一个问题。

它不应该逼 reader 记住 type 拒绝命名的 vocabulary，更不应该因为 bits 很便宜，就凭空创造现实不存在的世界。

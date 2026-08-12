# boolean-blindness — Enforcer

## 定义
当 `true/false` 被拿来编码一个**有名字的 domain choice**，而不是真正的 binary proposition，导致 vocabulary 恰恰在 caller 做决定的地方消失时，就是 boolean blindness。

多个 flags 合起来表示一个 conceptual state 时更糟。两个 booleans 会制造 4 个 representable combinations；五个会制造 32 个。如果 domain 实际只有 3 个 meaningful modes，剩下 29 个并不是“灵活性”，而是 representation 凭空创造的 fictional states。

问题不是 boolean 存在。`isEmpty`、`hasPermission`、`wasCached` 之类 predicate 本来就应该是 bool。真正 defect 是让 bits 承担比 yes/no 更丰富的 vocabulary。

## 支配原则
Call site 应该直接说出它正在做的选择。

`open(true, false)` 迫使 reader 从 parameter order、editor hint 或 memory 找回 meaning。`open(ReadWriteExisting)` 则把 domain decision 留在 expression 本身。

更重要的是，flag product 会扩大 state space。一旦 illegal combinations 可以被构造，每个 consumer 就必须 defensive check，或暗中相信 constructor 永远守纪律。这不是 flexibility，而是 modeling failure。

所以 boolean blindness 同时是 readability defect，也经常是 correctness defect：它一边擦掉 names，一边制造 possibility。

## 何时触发
当 booleans 编码 mode、phase、policy alternative、permission、result kind 或 mutually constrained state，而这些 meaning 在 domain 中本来有名字时触发。常见迹象：

- caller 直接传 literal `true/false`，必须靠 parameter hint/comment 才知道意义；
- 多个 flags 需要满足 “exactly one”“at most one”“if A then not B” 等规则；
- 新 mode 通过“再加一个 boolean”实现，而不是新增 named case；
- 同一个 flag 的含义依赖 sibling flags；
- persisted record 有 `isRunning/isDone/isFailed/isCancelled` 一串 lifecycle flags；
- feature/policy code 对 boolean tuple branch，而 tuple 实际只对应少数 named modes；
- read/write/admin 等 capability 被分散成 booleans，但只有特定组合合法；
- API 暴露 bool，而 action/mode enum 本可以让 call site self-describing。

## 不应触发
- Value 是真正 predicate，只有两个 semantic outcomes、没有 hidden third state，例如 `isEmpty`、`contains`、`isAuthorized` observation。
- Return boolean 直接回答一个名字已经在 call site 可见的 yes/no question。
- 多个 boolean facts 真正 independent，而且 Cartesian product 全部有 contract meaning。
- Wire/storage 用 bits，但 domain boundary 会立刻构造 named cases/capabilities。
- Boolean 只用于 domain choice 已经完成之后的 representation optimization，不再泄漏回 policy vocabulary。

## 与相邻规则区分
`illegal-state-representable` 是更广的 state-space defect。Booleans 本身正在擦除 named alternatives 时用 `boolean-blindness`；任意 nullable fields/discriminants 组成 impossible product 时用更广规则。

`primitive-obsession` 看 primitives 一般性的 semantic identity；boolean 特别容易在 call site 抹掉 vocabulary、又指数级放大组合，所以本规则更锋利。

仅仅因为 literal 难读而抱怨 “magic boolean” 太浅。规则应当因为 named domain choice / constrained state 被压成 bits 而触发。

## 判定程序
先完全不看 flags，写出真正 semantic alternatives。

对 flag cluster 列完整 truth table，并给每个 combination 标记：

- meaningful named state；
- genuinely independent combination；
- impossible/undefined state。

如果多行实际对应 domain names，或有 rows 根本没有合法 meaning，这个 boolean product 就是错误模型。

对 single boolean parameter 问：“我能不能把 `true/false` 换成两个能够表达真实 policy/mode 的名字？”如果能，优先 named choice。如果它只是回答 function 已经命名清楚的 predicate，保留 bool。

## 例子
- positive：`openFile(path, true, false)` 表示 write=true/create=false；reader 必须看 signature 才懂。
- positive：`{ isRunning, isCompleted, isFailed }` 可以构造 `true,true,true`，而 lifecycle 明明只允许一个 state。
- positive：`send(message, true)` 中 `true` 实际就是 `RequireAcknowledgement`，domain 已经有名字。
- positive：新增 “dry run” 时又加 `isDryRun` 放在 `isWrite` 旁边，开始制造 contradictory combinations。
- near-miss：`collection.isEmpty(): bool` 直接返回 binary observation，没有 vocabulary 被擦掉。
- near-miss：`{ isEncrypted, isCompressed }` 如果四种组合都真实合法，就是 independent facts。
- counterexample：`FileOpenMode = Read | WriteExisting | WriteCreate`，caller 明确选择 named case。

## Nudge
Boolean 应当回答一个问题。

它不应该逼 reader 记住 type 拒绝命名的 vocabulary，更不应该因为 bits 很便宜，就凭空创造现实不存在的世界。

# misleading-name — Lesson

## 评价

### 定义

Misleading name 是 identifier 向读者承诺了实现没有提供的 guarantee、owner、scope 或 domain meaning。名字不是装饰；读者靠它跳过重复阅读实现，所以错误名字会把错误 premise 缓存进每个 call site。

### 支配原则

名字是人类推理中的可执行假设。读者用它跳过重读实现；这正是假名字比含糊名字更危险的原因。每次相遇都会把一个错误前提导入下游推理，成本随复用累积。因此名字必须描述 contract，而不是代码的抱负或历史。

### 何时触发

- `commitDurable` 实际只写 memory；
- `atomicSave` 没有 atomicity；
- `uniqueId` 只“通常不重复”；
- `authorizedUser` 尚未授权；
- `final/complete/safe/verified` 比真实 evidence 更强；
- 名字暗示 global scope，实际只 session-local，或反之。

### 不应触发

- 泛化名字可能弱，但未必说谎；
- qualifier 已诚实缩小 guarantee，如 `InMemoryCache`；
- 单纯有多个 synonym 属 `domain-language-drift`；
- 缩写难解码属 `abbreviation-anxiety`。

### 与相邻规则区分

`domain-language-drift` 管的是系统内不一致的词汇表；`abbreviation-anxiety` 管的是解码成本。本规则是特定名字里的语义谎言。Tie-break：如果同一概念有多个名字，用 `domain-language-drift`；如果一个名字声明了假 contract，用本规则。

### 判定程序

写出一个合理读者仅凭名字会推断的最强 contract，再与实现逐项对比。名字比实现强一档，就是 semantic lie。

### 例子

- positive：`commitDurable` 只写 process-local map，从不 fsync。
- near-miss：`tryReserve` 让尝试显式；失败本来就是名字 contract 的一部分。
- counterexample：`appendToJournal` 确实 append 到 journal，且 tests 证明这个 durability claim。

## 建议

### 现在该做什么

让 name contract 与 real contract 对齐：若强 guarantee 本来就是目标，增强实现；若现实现才是目标，rename 到真实语义。同步 public API、tests、docs、events，别保留会继续传播谎言的 alias。

### 为什么重要

False name 会把 caveat 成本乘以所有 call sites。每个读者都必须知道“虽然叫 durable 但其实不 durable”，这说明名字已经成为 anti-documentation。

### 修复策略

用平实语言命名真实的 domain fact、owner 与 guarantee。同步更新 public types、tests、docs 与 call sites，让词汇收敛，而不是保留继续传播谎言的 misleading aliases。

### 决策分支

- 若更强的名字才是 intended contract，增强实现直到声明为真。
- 若现实现才是 intended contract，rename 让名字匹配那个更弱的 guarantee。

### 常见假修复

- 加 comment “despite the name…”。
- 前缀 `Real/Actual/Safe`，实现仍无对应 guarantee。
- 只改内部名，provider/public surface 继续说旧谎。
- 为“兼容”保留 misleading alias，却没有真实 consumer。

### 验证

让不熟实现的人只看名称与类型，写出他们认为存在的 guarantees；应与 tests/implementation 实际证明的内容一致。

### 完成条件

名字是可靠的 compressed contract，不需要团队记忆一套“名字虽这么叫但别当真”的 caveats。

### Nudge

名字是给维护者使用的缓存。错误名字最危险的地方，是它让人**合理地不去检查实现**，然后从一个假 premise 继续推理。

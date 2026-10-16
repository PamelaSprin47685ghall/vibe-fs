# stale-documentation — Lesson

## 评价

### 定义

Stale documentation 发生在一个自称 authoritative 的 spec/schema/example/diagram 与 implementation 同时声称描述“当前 contract”，却已经给出不同答案。

这不是 clerical mismatch，而是 epistemic fork：未来工程师可能完全忠实地照文档实现，并因此重新引入代码早已废弃的行为。

### 支配原则

Authoritative documentation 与 code 是同一份 agreement 的两种 representation。如果它们能独立分叉，未来设计决策可能相对于错误的 contract 版本在逻辑上正确。因此 authoritative 文档必须与它定义的行为原子地改变。

### 何时触发

- CLI/API/schema/lifecycle 已变，how/spec 仍写旧 contract；
- current diagram 表示旧 ownership；
- examples 仍教 retired field/tool/flow；
- code 与 docs 都被团队称为“真源”；
- behavior change 没有同步更新其 owning documentation。

### 不应触发

- 明确标为 historical 的旧说明；
- private implementation change 不影响 documented contract；
- generated docs 与 source-of-truth 在同一 delivery 自动更新；
- theatrical comments 不是 owning spec，优先归 `comment-theater`。

### 与相邻规则区分

`comment-theater` 管用 prose 替代 structure；`misleading-name` 管 identifiers；`missing-invariant-documentation` 管从未写下的 invariant。本规则是两个都声称描述当前 contract 的 surface 之间的分歧。Tie-break：如果 authoritative docs 与 behavior 分叉，这个案子归本规则。

### 判定程序

假装看不到 implementation，只读维护中的文档。一个 competent reader 会预测出与当前 tests/runtime 不同的行为吗？会，就是 stale authority。

### 例子

- positive：命令的 flags 在 code 里变了，`docs/how` 仍把旧 CLI 文档化为 current。
- near-miss：dated `history.md` 描述旧 API，并明确标注为 historical 而非 current。
- counterexample：schema、examples 与 how-docs 与 behavior change 在同一 delivery 更新。

## 建议

### 现在该做什么

找出 changed contract 的所有 authoritative representations，在同一 delivery 更新或删除旧语义。Historical material 明确标历史；current docs 不保留两个竞争版本。

### 为什么重要

Stale authoritative docs 会让正确的人做出错误修改：他不是粗心，而是相信了项目自己发布的 contract。同步因此属于 correctness，不是最后有空再做的文案工作。

### 修复策略

把 changed concept 追到它的 owning documentation，更新语义而不是只更新 examples，删除过时描述而不是在 current docs 里留下历史备选。

### 决策分支

- 若 owning spec/schema/how-doc 会预测旧 behavior，在本 delivery 里更新或移除它。
- 若文本明确是 historical，保留它，并确保 current docs 是 authority。
- 若 comments 而非 owning docs 是 theatrical，那是 `comment-theater`——不要把它当本规则的主要修复。

### 常见假修复

- 加一句“docs may be outdated”。
- 只更新 changelog，不改 how/spec/schema。
- examples 换新，但 invariant 正文仍旧。
- 再写一份新文档，不删除/降级旧 authority。
- 说“源码才是真的”，同时继续发布一份自称 authoritative 的旧 spec。

### 验证

不看 source，只按 current docs 操作/实现，所得 contract 应与 behavioral tests/runtime 一致。

### 完成条件

所有维护中的 authoritative representations 对当前 contract 给出同一答案；旧答案只在明确历史上下文中存在。

### Nudge

一个 contract 不能同时有两个 current versions。历史可以保留旧故事，维护中的 authoritative surface 必须只讲现在。

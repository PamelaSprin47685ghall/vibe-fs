# stale-documentation — Enforcer 中文版

## 定义

Stale documentation 发生在一个自称 authoritative 的 spec/schema/example/diagram 与 implementation 同时声称描述“当前 contract”，却已经给出不同答案。

这不是 clerical mismatch，而是 epistemic fork：未来工程师可能完全忠实地照文档实现，并因此重新引入代码早已废弃的行为。

## 支配原则

Authoritative documentation 与 code 是同一份 agreement 的两种 representation。如果它们能独立分叉，未来设计决策可能相对于错误的 contract 版本在逻辑上正确。因此 authoritative 文档必须与它定义的行为原子地改变。

## 何时触发

- CLI/API/schema/lifecycle 已变，how/spec 仍写旧 contract；
- current diagram 表示旧 ownership；
- examples 仍教 retired field/tool/flow；
- code 与 docs 都被团队称为“真源”；
- behavior change 没有同步更新其 owning documentation。

## 不应触发

- 明确标为 historical 的旧说明；
- private implementation change 不影响 documented contract；
- generated docs 与 source-of-truth 在同一 delivery 自动更新；
- theatrical comments 不是 owning spec，优先归 `comment-theater`。

## 与相邻规则区分

`comment-theater` 管用 prose 替代 structure；`misleading-name` 管 identifiers；`missing-invariant-documentation` 管从未写下的 invariant。本规则是两个都声称描述当前 contract 的 surface 之间的分歧。Tie-break：如果 authoritative docs 与 behavior 分叉，这个案子归本规则。

## 判定程序

假装看不到 implementation，只读维护中的文档。一个 competent reader 会预测出与当前 tests/runtime 不同的行为吗？会，就是 stale authority。

## 例子

- positive：命令的 flags 在 code 里变了，`docs/how` 仍把旧 CLI 文档化为 current。
- near-miss：dated `history.md` 描述旧 API，并明确标注为 historical 而非 current。
- counterexample：schema、examples 与 how-docs 与 behavior change 在同一 delivery 更新。

## Nudge

一个 contract 不能同时有两个 current versions。历史可以保留旧故事，维护中的 authoritative surface 必须只讲现在。
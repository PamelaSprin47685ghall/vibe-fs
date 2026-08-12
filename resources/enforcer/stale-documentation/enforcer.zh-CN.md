# stale-documentation — Enforcer 中文版

## 定义
Stale documentation 发生在一个自称 authoritative 的 spec/schema/example/diagram 与 implementation 同时声称描述“当前 contract”，却已经给出不同答案。

这不是 clerical mismatch，而是 epistemic fork：未来工程师可能完全忠实地照文档实现，并因此重新引入代码早已废弃的行为。

## 何时触发
- CLI/API/schema/lifecycle 已变，how/spec 仍写旧 contract；
- current diagram 表示旧 ownership；
- examples 仍教 retired field/tool/flow；
- code 与 docs 都被团队称为“真源”；
- behavior change 没有同步更新其 owning documentation。

## 不要误判
- 明确标为 historical 的旧说明；
- private implementation change 不影响 documented contract；
- generated docs 与 source-of-truth 在同一 delivery 自动更新；
- theatrical comments 不是 owning spec，优先归 `comment-theater`。

## 刀口
假装看不到 implementation，只读维护中的文档。**一个 competent reader 会预测出与当前 tests/runtime 不同的行为吗？** 会，就是 stale authority。

## 提醒
一个 contract 不能同时有两个 current versions。历史可以保留旧故事，维护中的 authoritative surface 必须只讲现在。

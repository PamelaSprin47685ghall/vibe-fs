# stale-documentation — Main 中文版

## 现在该做什么

找出 changed contract 的所有 authoritative representations，在同一 delivery 更新或删除旧语义。Historical material 明确标历史；current docs 不保留两个竞争版本。

## 为什么重要

Stale authoritative docs 会让正确的人做出错误修改：他不是粗心，而是相信了项目自己发布的 contract。同步因此属于 correctness，不是最后有空再做的文案工作。

## 修复策略

把 changed concept 追到它的 owning documentation，更新语义而不是只更新 examples，删除过时描述而不是在 current docs 里留下历史备选。

## 决策分支

- 若 owning spec/schema/how-doc 会预测旧 behavior，在本 delivery 里更新或移除它。
- 若文本明确是 historical，保留它，并确保 current docs 是 authority。
- 若 comments 而非 owning docs 是 theatrical，那是 `comment-theater`——不要把它当本规则的主要修复。

## 常见假修复

- 加一句“docs may be outdated”。
- 只更新 changelog，不改 how/spec/schema。
- examples 换新，但 invariant 正文仍旧。
- 再写一份新文档，不删除/降级旧 authority。
- 说“源码才是真的”，同时继续发布一份自称 authoritative 的旧 spec。

## 验证

不看 source，只按 current docs 操作/实现，所得 contract 应与 behavioral tests/runtime 一致。

## 完成条件

所有维护中的 authoritative representations 对当前 contract 给出同一答案；旧答案只在明确历史上下文中存在。
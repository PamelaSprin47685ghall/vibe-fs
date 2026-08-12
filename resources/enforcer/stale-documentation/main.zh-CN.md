# stale-documentation — Main 中文版

## 现在该做什么
找出 changed contract 的所有 authoritative representations，在同一 delivery 更新或删除旧语义。Historical material 明确标历史；current docs 不保留两个竞争版本。

## 为什么这很重要
Stale authoritative docs 会让正确的人做出错误修改：他不是粗心，而是相信了项目自己发布的 contract。同步因此属于 correctness，不是最后有空再做的文案工作。

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

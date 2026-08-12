# context-model-leak — Main 中文版

## 现在该做什么
从每个 bounded context 的问题与 invariant 重新定义 local model，只通过明确 boundary contract 传递真正共享的 facts。不要从现有 master object 的字段列表出发拆 DTO。

## 为什么这很重要
Universal model 会制造“semantic coupling disguised as reuse”。一个字段为了 Billing 加入后，Auth/Session/UI 全部开始知道它；很快 nullable、context flags、conditional validation 堆积起来，因为类型已经无法说明哪个解释现在有效。

## 修复策略
- 列出每个 context 要回答的问题；
- 为每个 context 建最小 model；
- 共享真正语义稳定的 value objects/IDs；
- crossing facts 通过 explicit contract 翻译；
- 删除在某 context 中永远“not applicable”的 foreign fields；
- persistence schema 不直接决定 domain model 边界。

## 常见假修复
- 把 master model 复制到每个 package，但字段仍一模一样、同步演化。
- 加 `contextType` 决定哪些字段有效。
- 给不适用字段全部加 `Option/null`。
- 用 `authEmail/billingEmail/...` 把多个概念继续塞同一个对象。
- 为每个 context 再包一层 view，却底下仍暴露完整 master object。

## 验证
一个 context 的核心逻辑应能只依赖自己的 model 与显式 crossing contracts 编译/测试。另一个 context 新增字段，不应让本 context 莫名发生 source change。

## 完成条件
每个 model 有一个 semantic owner 和一个 reason to change；跨 context 传递的是明确 facts，不是一个无所不知的 master object。

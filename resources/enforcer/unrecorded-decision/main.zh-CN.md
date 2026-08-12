# unrecorded-decision — Main 中文版

## 现在该做什么
在项目真正做 architecture decision 的地方记录：context/constraints、chosen path、credible alternatives、为什么拒绝、consequences、什么新证据会触发 revisit。链接 governing contracts，不复制整份 specification。

## 为什么这很重要
没有 rationale 的代码只是一种“ unexplained equilibrium”。未来人看到它，很可能为了简化而恢复曾经失败的设计，然后重新支付同一轮分析甚至事故成本。

## 常见假修复
- 贴 chat transcript，不提炼 decision。
- 只描述最终实现，不记录 alternatives 与 constraints。
- 把 contract 全复制进 ADR，制造第二 truth source。
- 每个小决定都写长 ADR，导致真正重要记录被噪声淹没。

## 验证
未来 reader 应能回答：这个 decision 在哪些 assumptions 下成立？当什么 evidence 出现时应重新考虑？当初哪些 credible alternatives 因什么被拒绝？

## 完成条件
架构不仅保存形状，也保存足够的反事实知识，避免未来把历史重新当成未知问题。

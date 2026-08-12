# unrecorded-lesson — Main 中文版

## 现在该做什么
把 reusable discovery 放进未来人自然会查的 artifact：regression test、runbook、RuleBook、contract doc、troubleshooting guide。记录 causal lesson，不要只 dump 调试过程。

## 为什么这很重要
未外部化的经验会让团队重复支付搜索成本：同样的 provider surprise、错误 hypothesis、诊断顺序一遍遍被重新发现。人的经验增长了，system knowledge 却没有增长。

## 修复策略
至少记录四件事：symptom、underlying fact、如何验证、它要求/禁止什么行动。能由 test/gate 保存的知识优先 executable preservation。

## 常见假修复
- 保存整段 session/log，没人知道结论在哪。
- 只写“注意 retry”。
- 放在个人 note 或一次性 chat。
- 同一 lesson 同时复制进五份文档，未来再产生 drift。

## 验证
一个没参与原事故的人能从 affected concept 找到 artifact，快速避开已知死路或直接验证 quirk。

## 完成条件
项目的 durable knowledge 因这次经验而增加；下次同类问题从今天的结论开始，而不是从昨天的无知开始。

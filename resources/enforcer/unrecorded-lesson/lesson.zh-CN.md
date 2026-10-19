# unrecorded-lesson — Lesson

## 评价

### 定义

一次 debug/incident/integration 调查若发现了可复用事实，却只让当事人“长了经验”，项目本身没有增加 durable memory，就是 unrecorded lesson。

真正的工程资本不是“我们这次终于知道了”，而是下一个没参加这次会话的人能否在犯同样错误之前遇到这份知识。

### 支配原则

经验只有被外化之后才成为工程资本。一次 incident 可能揭示 provider quirk、ordering 约束、失败的 hypothesis、诊断捷径或 recovery law，这些单靠源码并不显而易见。根因是经验停留在对话里：组织在生物意义上学会了，结构上却没有，于是人员更替或上下文丢失会把系统的有效记忆清零。

### 何时触发

- 花数小时发现 provider quirk，结束后只留 chat；
- 已证明某 hypothesis 是死路，却没有 runbook/test/rule 保存；
- recovery/ordering/diagnostic shortcut 可重复使用，却只存在个人笔记；
- 同类 incident 每次从零开始排查。

### 不应触发

- 真正一次性、无复用价值的细节；
- lesson 已被 regression test/runbook/rule/contract 保存；
- deliberate architecture rationale 属 `unrecorded-decision`；
- 已有 lesson 明明存在却没用，属于 `repeated-known-mistake`。

### 与相邻规则区分

`unrecorded-decision` 是为一次有意选择保存 rationale；`repeated-known-mistake` 是没能复用已有的记忆。Tie-break：若新获得的经验没有转化为 durable 的项目记忆，用本规则；若既有 lesson 明明可用却被忽略，用 `repeated-known-mistake`。

### 判定程序

问：**未来另一个工程师遇到相似 symptom，这次发现能不能显著缩小他的搜索空间？** 能，而项目里找不到它，组织只在生物记忆里学习了。

### 例子

- 正例：花数小时发现 vendor 的 409 是最终一致，会话结束却没有 runbook 或 test；
- 近邻：同样的 quirk 被编码成 regression test 和一段 runbook 条目；
- 反例：选了 Kafka 而不用 Rabbit 却从不写原因，那是 `unrecorded-decision`。

## 建议

### 现在该做什么

把 reusable discovery 放进未来人自然会查的 artifact：regression test、runbook、RuleBook、contract doc、troubleshooting guide。记录 causal lesson，不要只 dump 调试过程。

### 为什么重要

未外部化的经验会让团队重复支付搜索成本：同样的 provider surprise、错误 hypothesis、诊断顺序一遍遍被重新发现。人的经验增长了，system knowledge 却没有增长。

### 修复策略

至少记录四件事：symptom、underlying fact、如何验证、它要求/禁止什么行动。能由 test/gate 保存的知识优先 executable preservation。

### 决策分支

- 若未来工程师遇到同样 symptom 能因此缩小搜索空间，写一个他们真正会查的最小 durable artifact；
- 若事实已经在 test、runbook 或 rule 里，指向它，而不是再造第二份副本。

### 常见假修复

- 保存整段 session/log，没人知道结论在哪。
- 只写“注意 retry”。
- 放在个人 note 或一次性 chat。
- 同一 lesson 同时复制进五份文档，未来再产生 drift。

### 验证

一个没参与原事故的人能从 affected concept 找到 artifact，快速避开已知死路或直接验证 quirk。

### 完成条件

项目的 durable knowledge 因这次经验而增加；下次同类问题从今天的结论开始，而不是从昨天的无知开始。

### Nudge

会话结束就消失的 discovery 是租来的知识。真正学会，意味着 repository/knowledge system 也发生了变化。

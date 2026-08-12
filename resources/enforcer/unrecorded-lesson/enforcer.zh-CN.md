# unrecorded-lesson — Enforcer 中文版

## 定义
一次 debug/incident/integration 调查若发现了可复用事实，却只让当事人“长了经验”，项目本身没有增加 durable memory，就是 unrecorded lesson。

真正的工程资本不是“我们这次终于知道了”，而是下一个没参加这次会话的人能否在犯同样错误之前遇到这份知识。

## 何时触发
- 花数小时发现 provider quirk，结束后只留 chat；
- 已证明某 hypothesis 是死路，却没有 runbook/test/rule 保存；
- recovery/ordering/diagnostic shortcut 可重复使用，却只存在个人笔记；
- 同类 incident 每次从零开始排查。

## 不要误判
- 真正一次性、无复用价值的细节；
- lesson 已被 regression test/runbook/rule/contract 保存；
- deliberate architecture rationale 属 `unrecorded-decision`；
- 已有 lesson 明明存在却没用，属于 `repeated-known-mistake`。

## 刀口
问：**未来另一个工程师遇到相似 symptom，这次发现能不能显著缩小他的搜索空间？** 能，而项目里找不到它，组织只在生物记忆里学习了。

## 提醒
会话结束就消失的 discovery 是租来的知识。真正学会，意味着 repository/knowledge system 也发生了变化。

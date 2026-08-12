# program-counter-state — Enforcer

Program-counter state 的问题，是把“**代码下一步该从哪里继续**”存成了 shared/durable domain fact。

`currentStep`, `nextAction`, `resumeAt`, `phase=callValidateThenSave` 这类字段往往不描述现实世界，只描述某个 implementation 的 instruction pointer。它们一旦进入 durable state，重构控制流就会变成 data migration；recovery 也开始承担“跳回旧函数中间继续跑”的任务。

真正 domain state 回答的是“世界现在是什么”：订单已批准、lease 已过期、job 已接受。Program counter 回答的是“解释器现在走到哪”。这两个问题不能因为都叫 `status/phase` 就混成一件事。

以下情形触发：

- DB 保存 `next_step = sendEmailThenFinalize`；
- crash recovery 根据函数名/step number 跳回 imperative workflow 中点；
- step enum 每次 refactor orchestration 都要 migration；
- field 唯一用途是决定“下一段代码跑什么”，外部 domain observer 根本不关心；
- durable state 同时保存 real facts 与一堆 implementation cursor/generation/flags，只为恢复 control flow；
- workflow engine 将每个代码语句阶段都变成业务 status。

不要误杀真正 workflow state。`Draft | Submitted | Approved` 如果产品、用户、其他 actor 都有语义依赖，它就是 domain fact，即使 control flow 也根据它分支。关键问题是：**换一种实现 sequencing 后，这个状态仍然值得存在吗？**

也不要误杀 local in-flight continuation。函数内部 loop index、local async state machine、process memory cursor，只要不被当 authoritative shared truth，就只是实现。

与 `phase-flag-accumulation` 区分：后者是 lifecycle 真实存在但 flags 组合建模很差；本规则则是 lifecycle 可能根本不属于 domain，只是 interpreter position 被持久化。与 `implicit-control-flow` 相反：一个把 sequencing 藏得太深，一个把 sequencing 提升得过高。

> Durable state 应描述世界，不应描述昨天那版代码执行到第几行。
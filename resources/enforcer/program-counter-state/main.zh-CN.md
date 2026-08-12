# program-counter-state — Main

把真实 workflow fact 与 interpreter position 分开。

对每个 persisted `phase/step/nextAction` 问：如果内部实现完全改成另一种 control structure，外部 domain 是否仍然需要知道这个值？

- 若需要，它可能是真 domain state：重新命名成业务概念，定义合法 transition 与拥有的数据。
- 若不需要，就把 sequencing 收回 in-flight structured control，durable store 只保留能够从世界事实重新决定下一步的 information。

Recovery 也应从 durable facts **重新推导接下来该做什么**，而不是恢复旧 instruction pointer。这样 refactor workflow 不再要求把所有历史 `step=7` 映射到新代码第几步。

常见假修复：

- `currentStep` 改名 `status`，语义完全不变；
- step enum 越做越细，让每个 await 前后都有一个 durable phase；
- DB 直接存 function/handler name；
- 为恢复中间过程，再加入 `subStep`, `resumeToken`, `lastAction`，逐渐把 interpreter 全存下来；
- 用 workflow engine 把每个 implementation continuation 变成 external schema；
- 删除 step field，但改从 temp filename / filesystem residue 猜 resume point，转成 `recovery-by-filesystem-state`。

若 operation 本身就是 long-running durable workflow，当然需要持久化 progress，但应持久化**有业务/协议意义的 milestone 与事实**：`PaymentAuthorized`, `ShipmentRequested`, `ApprovalPending`，而不是“下一次调用 handleStep4”。下一步应由当前 facts + policy 推导。

验证最有力的是 refactor test：概念上把 sequencing 从 A→B→C 改成 batch、不同 helper、不同 function boundaries，只要 domain-visible lifecycle 没变，durable schema/history 不应需要 migration。

Crash/restart 也要测：从每个 durable milestone 重启，系统根据事实重新进入正确行为，而不是依赖 process 当时 local continuation 的残影。

如果某个 technical cursor 真是外部 protocol 必需（例如 stream offset），把它作为该 protocol 的明确 fact 管理，不要混进业务 phase 伪装成“当前步骤”。

完成时 stored state 可以回答“世界已经发生了什么/还欠什么”，而不是“旧程序下一行准备执行什么”。

> Recovery 最稳的方式不是保存解释器，而是保存足够真实的事实，让新的解释器重新知道该做什么。
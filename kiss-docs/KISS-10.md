# Nudge 删除

---

## 一、不是领域

Todo while · Review while · 空输出在 ContinueWork · **无**独立调度器。

旧 Nudge 名称把四种不同知识揉成一个“提醒平台”：

|旧表面|真正事实|结束条件|
|---|---|---|
|Todo reminder|仍有未完成 Todo|TodoChanged 使 Unfinished=false|
|Review reminder|Review.Required=true|Passed 或明确 ReviewExhausted|
|Empty output nudge|本次发送没有有效结果|同模型重试、换模型或 FallbackExhausted|
|Idle next step|主流程当前可执行的下一个动作|对应结构化程序动作开始|

它们不共享 owner、lease、文案、失败策略或持久化事实。抽成 Nudge 只增加交叉状态，不减少领域复杂度。

---

## 二、删第二条路径 [NORMATIVE]

[FORBIDDEN] idleProposals / List.tryPick / PromptKind.Nudge。  
Purpose 仅：ContinueTodo | RetryTurn | SwitchModel | ReviewChanges | RunChild | ReturnToParent。

禁止把 `idleProposals` 改名为 `continuationCandidates`、`nextActions` 或 `reminders` 后继续保留。只要另有一个按快照扫描、排序、抢占 prompt 的循环，就仍然是第二条控制路径。主流程的顺序必须直接写在 `finishTodo`、`passReview`、`ContinueWork` 与 Child/Review Flow 中。

---

## 三、仅保留

PromptProtocol + PromptKey + Host MessageId 相关 + Pending 派生（KISS-Driver）。

这里保留的是**本 Runtime** 外部协议事实，不是跨 Runtime 全局 prompt 锁。另一个 OpenCode Runtime 可能同时向同一 Session 发 prompt；当前 Runtime 在生命周期内不实时看见它，重启时才把双方事实纳入 Chronological Replay。存储层不删除、不伪造、不把它判成 fork。

---

## 四、删除清单

NudgeState/Snapshot/Lease/Claim/Outcome/Owner/Classifier/Runtime*/Effect*…

同时删除围绕这些名字产生的转发壳、事件 writer、模型解析器、skip token、dispatch claim、跨功能快照 source。若某个实现仍需要去重，迁移到 PromptProtocol；若需要循环，写成 while 或尾递归；若需要终态，返回领域 DU。

---

## 五、映射

|旧|新|
|---|---|
|TODO/REVIEW while|主程序|
|FALLBACK|KISS-09 尾递归|
|IDLE|删除|
|DEDUP/LEASE|PromptKey/Protocol|

## 六、验收

迁移完成后，仓库应满足：

- 搜索不到 `PromptKind.Nudge`、`NudgeLease`、`NudgeDispatchClaim`；
- Todo/Review 主循环可以单独阅读，不需要进入 Nudge 模块；
- 空输出路径只在 ContinueWork/Fallback 内处理；
- 同一 Runtime 的 Prompt 去重仍有正式测试；
- 同一 Session 的另一个 Runtime 事实不会被当前运行期偷偷读取；
- 重启测试证明所有可见 Runtime 日志仍能按时间归并。

---

*KISS-10 终。*

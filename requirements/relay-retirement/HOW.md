# relay-retirement — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs(.fsi)`：exact tool binding、freeze-before-check 与 atomic retirement transaction。
- `src/Wanxiangshu/Mission/Relay/Surface.fs(.fsi)`：`retireContinue` 接受 Continue outcome 与显式 retirement 快照（允许与 assessment 快照不同，取退休时当前并前向携带），`retireAccepted` 接受 Accepted + certificate id 与显式快照（须等于 assessment 快照）；`retirement()` 暴露闭合 outcome、快照、修订、ProviderRunId 与 ToolCallId；`blockCleanup` 记录 RetirementCleanupBlocked，清障后可重试 Accepted。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：normal-terminal 观测按 phase 选择 `runtime/manager-assess`、`runtime/manager-work`、`runtime/manager-finish` 三份 nudge 文档之一经 manager guard gate 发送，gate admission 唯一去重。
- `src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs(.fsi)` 与 `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs(.fsi)`：退休请求拦截与 durable context cut；`RetiredAttemptStopped` 清空旧请求并成对执行 `suppressProviderStep + releasePhysicalExecution`，防止已退休 Manager 占住父任务借出的 model-capacity token。任一 outcome 在 active 且已准入的新迭代出现前中断旧请求，只有 Continue 经 `ManagerLoopGate`（`manager-loop:`）自动激活。显式证书失效后 Change ContinueLoop 可派发使用 LatestRetirement cut 的普通新迭代。
- `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs`：HumanRoot 在旧 attempt interrupt 完成后开启或恢复当前迭代，再派发规范 assessment continuation；AgentOwnerRoot 由 Change 独占派发。
- `src/Wanxiangshu/Composition/Durable/Fold.fs`：Continue 退休保留 active LogicalRun，Accepted 退休关闭当前迭代的 HumanRoot Manager authority（证书失效后允许普通新迭代）。
- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：`ManagerLoopGate`、`RetirementOutcome`、`ProjectionCut`、`RetirementSummary`。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `delegation`
- `managed-chat-execution`
- `provider-attempt-recovery`

## 验证

| 命题 | executable proof |
|---|---|
| RETIRE-001 | `requirements/relay-retirement/tests/retirement-admission.test.mjs::WHAT[RETIRE-001] suicide retires without any quality progress or test gate` |
| RETIRE-002 | `requirements/relay-retirement/tests/retirement-admission.test.mjs::WHAT[RETIRE-002] dirty work quality state and conflicts never block suicide` |
| RETIRE-003 | `requirements/relay-retirement/tests/retirement-admission.test.mjs::WHAT[RETIRE-003] live recursive resources are the only business blockers` |
| RETIRE-004 | `requirements/relay-retirement/tests/retirement-admission.test.mjs::WHAT[RETIRE-004] freeze fence rejects retirement races without crossing the next iteration boundary` |
| RETIRE-007 | `requirements/relay-retirement/tests/retirement-transaction.test.mjs::WHAT[RETIRE-007] Continue retirement commits a closed Continue outcome with cut binding`；`requirements/relay-retirement/tests/retirement-transaction.test.mjs::WHAT[RETIRE-007] Accepted retirement commits a closed Accepted outcome with certificate binding`；`requirements/relay-retirement/tests/retirement-transaction.test.mjs::WHAT[RETIRE-007] Accepted with a stale different snapshot fails`；`requirements/relay-retirement/tests/retirement-transaction.test.mjs::WHAT[RETIRE-007] blocked perfect iteration retries Accepted after blockers clear` |
| RETIRE-008 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[RETIRE-008] wire cut drops the retired tail and the internal loop wake until the next real user turn` |

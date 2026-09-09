# relay-retirement — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs(.fsi)`：exact tool binding、freeze-before-check 与 atomic retirement transaction。
- `src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs(.fsi)`：通过 Relay `Fold.view` 的 typed `RoadView` 读取 active incumbency、assessment 与 certificate binding，不读取编译后的 Map／union 布局。退休 fence 保存 `IncumbencyId`；同任期重复冻结不重新取得 admission，新 active 任期清除旧 fence，无 active 任期时保留已冻结状态。assessment 存在性取 `AcceptedAssessmentTransport.IsSome`，有效证书仍须同时匹配 active incumbency、snapshot 与 authority revision。
- `src/Wanxiangshu/Mission/Relay/Surface.fs(.fsi)`：`retireContinue` 接受 Continue outcome 与显式 retirement 快照（允许与 assessment 快照不同，取退休时当前并前向携带），`retireAccepted` 接受 Accepted + certificate id 与显式快照（须等于 assessment 快照）；`retirement()` 暴露闭合 outcome、快照、修订、ProviderRunId 与 ToolCallId；`blockCleanup` 记录 RetirementCleanupBlocked，清障后可重试 Accepted。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：normal-terminal 观测从 active incumbent、accepted assessment transport 与 exact bound certificate 选择 `runtime/manager-assess`、`runtime/manager-work`、`runtime/manager-finish`；同一 owner CE 在 physical stop 前冻结 exact retirement/authority continuation context，再串行执行 stop → 开启迭代 → `ManagerLoopGate` enqueue。进入 opening 前重读 durable context；并发 exact observation 以 RetirementId 派生相同 IncumbencyId 并由 Relay fold 幂等收敛，gate admission 保证一个 physical prompt。显式证书失效后的 Change ContinueLoop 调用同一 owner；transport receipt 与 physical acceptance 仍由各自事实区分。
- `src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs(.fsi)` 与 `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs(.fsi)`：退休请求拦截与 durable context cut；`RetiredAttemptStopped` 向 Manager owner 提供成对的 `suppressProviderStep + releasePhysicalExecution + InterruptAttempt` capability，owner 返回后清空旧请求，防止已退休 Manager 占住父任务借出的 model-capacity token。transform 不直接解释 Continue，也不直接开启或发送下一迭代。
- `src/Wanxiangshu/Composition/Durable/Fold.fs`：Continue 与 Accepted 都清除 Relay active incumbency；Continue 保留承载 Road 的 LogicalRun authority并由 owner 显式开启下一迭代，Accepted 关闭 HumanRoot Manager authority（证书失效后允许普通新迭代）。
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

# relay-retirement — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs(.fsi)`：exact tool binding、freeze-before-check 与 atomic retirement transaction。
- `src/Wanxiangshu/Mission/Manager/Workflow.fs(.fsi)`：normal-terminal causal-frontier decision。
- `src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs(.fsi)`：退休请求拦截与 durable context cut。
- `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs`：旧 attempt interrupt 完成后派发正式 successor continuation。
- `src/Wanxiangshu/Composition/Durable/Fold.fs`：区分单任退休与 HumanRoot Manager Road 完成。

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
| RETIRE-004 | `requirements/relay-retirement/tests/retirement-admission.test.mjs::WHAT[RETIRE-004] freeze fence rejects retirement races without crossing the successor incumbency boundary` |
| RETIRE-005 | `requirements/relay-retirement/tests/nudge.test.mjs::WHAT[RETIRE-005] each fresh frontier schedules at most one nudge without a protocol retry ceiling` |
| RETIRE-006 | `requirements/relay-retirement/tests/nudge.test.mjs::WHAT[RETIRE-006] provider failure and external terminal never schedule an exit nudge` |
| RETIRE-007 | `requirements/relay-retirement/tests/retirement-transaction.test.mjs::WHAT[RETIRE-007] retirement commits retired baton cut and successor request as one fold-visible state transition` |
| RETIRE-008 | `requirements/relay-retirement/tests/no-session-abort.test.mjs::WHAT[RETIRE-008] retirement never issues a session-scoped physical abort`；`requirements/relay-retirement/tests/no-session-abort.test.mjs::WHAT[RETIRE-008] retired continuations are interrupted in the transform hook by gate identity` |

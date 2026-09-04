# relay-retirement — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Retirement/ResourceClosure.fs(.fsi)`：递归资源 blocker projection。
- `src/Wanxiangshu/Mission/Relay/Retirement/Admission.fs(.fsi)`：freeze-before-check 与 atomic retirement transaction。
- `src/Wanxiangshu/Mission/Relay/Retirement/Nudge.fs(.fsi)`：normal-terminal causal-frontier decision。
- `src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs(.fsi)`：exact tool binding 的薄 adapter。

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
| RETIRE-004 | `requirements/relay-retirement/tests/retirement-admission.test.mjs::WHAT[RETIRE-004] freeze fence rejects resource creation racing after retirement begins` |
| RETIRE-005 | `requirements/relay-retirement/tests/nudge.test.mjs::WHAT[RETIRE-005] each fresh frontier schedules at most one nudge without a protocol retry ceiling` |
| RETIRE-006 | `requirements/relay-retirement/tests/nudge.test.mjs::WHAT[RETIRE-006] provider failure and external terminal never schedule an exit nudge` |
| RETIRE-007 | `requirements/relay-retirement/tests/retirement-transaction.test.mjs::WHAT[RETIRE-007] retirement commits retired baton cut and successor request as one fold-visible state transition` |


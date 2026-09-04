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
| RETIRE-001/002/003/004 | `requirements/relay-retirement/tests/retirement-admission.test.mjs` |
| RETIRE-005/006 | `requirements/relay-retirement/tests/nudge.test.mjs` |
| RETIRE-007 | `requirements/relay-retirement/tests/retirement-transaction.test.mjs` |


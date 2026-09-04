# host-provider-failure-ownership — HOW

## 生产落点

- `src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs(.fsi)`：enabled 时固定 retry=0。
- `src/Wanxiangshu/Execution/Failure/Model.fs(.fsi)` / `Policy.fs(.fsi)`：closed Host failure claim/presentation decision。
- `src/Wanxiangshu/OpenCode/Host/ProviderFailurePresentation.fs(.fsi)`：Host 边界 typed adapter。
- `scripts/checks/opencode-host-failure-ownership.mjs`：1.18.18 provenance/shape drift gate。

## 依赖关系

DEPENDS ON:
- `execution-failure-policy`
- `provider-attempt-recovery`
- `host-boundary`

## 验证

| 命题 | executable proof |
|---|---|
| HOSTFAIL-001/002 | `requirements/host-provider-failure-ownership/tests/retry-zero.test.mjs` |
| HOSTFAIL-003/004/006 | `requirements/host-provider-failure-ownership/tests/presentation.test.mjs` |
| HOSTFAIL-005 | `requirements/provider-attempt-recovery/tests/retry-owner.test.mjs` |
| HOSTFAIL-007 | `requirements/host-provider-failure-ownership/tests/version-drift.test.mjs` |


# host-provider-failure-ownership — HOW

## 生产落点

- `src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs(.fsi)`：enabled 时固定 retry=0。
- `src/Wanxiangshu/Execution/Failure/Model.fs(.fsi)` / `Policy.fs(.fsi)`：closed Host failure claim/presentation decision。
- `src/Wanxiangshu/OpenCode/Host/ProviderFailurePresentation.fs(.fsi)`：Host 边界 typed adapter。
- `scripts/checks/opencode-host-failure-ownership.mjs`：1.18.29 provenance/shape drift gate。

## 依赖关系

DEPENDS ON:
- `execution-failure-policy`
- `provider-attempt-recovery`
- `host-boundary`

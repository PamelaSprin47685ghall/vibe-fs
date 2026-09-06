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

## 验证

| 命题 | executable proof |
|---|---|
| HOSTFAIL-001 | `requirements/host-provider-failure-ownership/tests/retry-zero.test.mjs::WHAT[HOSTFAIL-001] managed config forces chatMaxRetries to zero and has no environment override` |
| HOSTFAIL-002 | `requirements/host-provider-failure-ownership/tests/retry-zero.test.mjs::WHAT[HOSTFAIL-002] Host retry zero is a literal ownership rule rather than a positive retry budget` |
| HOSTFAIL-003 | `requirements/host-provider-failure-ownership/tests/presentation.test.mjs::WHAT[HOSTFAIL-003] recovery resolution produces zero Wanxiangshu final presentation`；`requirements/host-provider-failure-ownership/tests/presentation.test.mjs::WHAT[HOSTFAIL-003] recovery resolution has no suppression facade` |
| HOSTFAIL-004 | `requirements/host-provider-failure-ownership/tests/presentation.test.mjs::WHAT[HOSTFAIL-004] non-terminal resolution leaves presentation to the Host` |
| HOSTFAIL-005 | `requirements/host-provider-failure-ownership/tests/presentation.test.mjs::WHAT[HOSTFAIL-005] policy owner recovers with zero Host retry` |
| HOSTFAIL-006 | `requirements/host-provider-failure-ownership/tests/presentation.test.mjs::WHAT[HOSTFAIL-006] exhaustion uses one typed final Wanxiangshu presentation`；`requirements/host-provider-failure-ownership/tests/presentation.test.mjs::WHAT[HOSTFAIL-006] terminal resolution is never retrying` |
| HOSTFAIL-007 | `requirements/host-provider-failure-ownership/tests/version-drift.test.mjs::WHAT[HOSTFAIL-007] OpenCode compatibility baseline is pinned to 1.18.29` |

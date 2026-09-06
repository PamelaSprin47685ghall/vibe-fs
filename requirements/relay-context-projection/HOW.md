# relay-context-projection — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：拥有 cut 类型。`ProjectionCut = { ProviderRunId; ToolCallId }` 定位 cut frontier；`RetirementOutcome` 决定 projection 去向：Continue 进入下一轮 projection，Accepted 关闭当前迭代（证书失效后允许普通新迭代）。
- `src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs(.fsi)`：拥有 cut。统一 Manager review-first context，并以 `RelayProjectionDisposition` 区分普通请求、当前迭代与已中断旧 attempt；退休后新 active 迭代就位后一律按 LatestRetirement cut 投影（含 Accepted 失效后重开）：保留 typed authority 消息与当前迭代消息，移除全部前任消息与首个内部 loop wake continuation；当前迭代跳过后置 XWire/Companion 历史投影，不注入旧 lifecycle WorkRecord。
- `src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs(.fsi)` 与 `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs`：任一 retirement 后、尚无 active 且已准入的新迭代时，中断旧 attempt，清空其 provider request，并释放该请求的 exact provider-step admission；Continue 经 `ManagerLoopGate`（`manager-loop:`）自动激活下一迭代，Accepted 只终止旧 attempt、永不自动激活。证书失效后的普通新迭代由 Change ContinueLoop 派发并同样使用 LatestRetirement cut；wake 消息不进入 provider context。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `provider-projection`
- `host-boundary`

## 验证

| 命题 | executable proof |
|---|---|
| PROJ-001 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-001] audit projection retains every physical message across the cut` |
| PROJ-002 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-002] projection cut covers the suicide request and result parts` |
| PROJ-003 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-003] next iteration context contains exact authority and existing current messages` |
| PROJ-004 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-004] retired finish and internal wake project to a clean authority start` |
| PROJ-005 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-005] next iteration shows the current-iteration tail after a clean authority start` |
| PROJ-006 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-006] projection is deterministic and bounded` |
| PROJ-007 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-007] Accepted retirement reopened after invalidation cuts to authority plus current tail` |
| PROJ-008 | `requirements/relay-context-projection/tests/loop-projection.test.mjs::WHAT[PROJ-008] projection cut preserves only typed authority from the retired iteration` |

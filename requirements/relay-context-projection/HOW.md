# relay-context-projection — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Retirement/Baton.fs(.fsi)`：canonical BatonBuilder。
- `src/Wanxiangshu/Mission/Relay/Projection.fs(.fsi)`：active phase、cut、certificate 与 successor context projection。
- `src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs(.fsi)`：统一 Manager review-first context。
- `src/Wanxiangshu/OpenCode/Host/HostMessageProjection.fs(.fsi)`：消费 typed ProjectionCut，不读文本。

## 依赖关系

DEPENDS ON:
- `relay-incumbency`
- `provider-projection`
- `host-boundary`

## 验证

| 命题 | executable proof |
|---|---|
| PROJ-001 | `requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-001] audit projection retains every physical message across the cut` |
| PROJ-002 | `requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-002] projection cut covers the suicide request and result parts`；`requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-002] wire cut drops the retired tail until the next user turn` |
| PROJ-003 | `requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-003] provider context is rebuilt from root authority current baton and post-cut epoch` |
| PROJ-004 | `requirements/relay-context-projection/tests/baton.test.mjs::WHAT[PROJ-004] first baton is ExistingWorld without invented predecessor facts` |
| PROJ-005 | `requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-005] successor provider view keeps one continuous session narrative` |
| PROJ-006 | `requirements/relay-context-projection/tests/baton.test.mjs::WHAT[PROJ-006] baton canonicalization is deterministic bounded and strips secret-like fields` |
| PROJ-007 | `requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-007] rebuilt successor context carries no retired raw history field` |
| PROJ-008 | `requirements/relay-context-projection/tests/projection-cut.test.mjs::WHAT[PROJ-008] projection cut preserves only typed authority messages from the predecessor epoch` |


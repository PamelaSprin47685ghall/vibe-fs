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
| PROJ-001/002/003/005/007 | `requirements/relay-context-projection/tests/projection-cut.test.mjs` |
| PROJ-004/006 | `requirements/relay-context-projection/tests/baton.test.mjs` |


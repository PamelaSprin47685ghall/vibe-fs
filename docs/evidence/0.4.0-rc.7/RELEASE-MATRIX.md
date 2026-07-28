# 0.4.0-rc.7 Release Matrix

| Gate | Result |
|---|---|
| clean `npm ci` | pass |
| tests-next | 281 passed |
| manager-tools | pass |
| gate-testkit | 29 passed |
| CANARY_REPEAT=3 (18 canaries) | pass |
| pack + empty-dir install/import | pass |
| provider-visible A/A/B/B | pass (`provider-aabb-trace.txt`) |

## Production changes vs rc.6

- Debounced PluginFallbackRetry after SessionIdle settle
- Non-retryable session.error → durable failure → EffectiveModel continue
- Wire models proven: test-model → test-model → test-model-b → test-model-b

## Still blocking final 0.4.0

- RC observation period
- Final cut + second clean gate on version `0.4.0`
- Private delivery default

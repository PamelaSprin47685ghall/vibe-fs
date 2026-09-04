# relay-incumbency — HOW

## 生产落点

- `src/Wanxiangshu/Mission/Relay/Contract.fs(.fsi)`：Road/Incumbency/phase/port vocabulary。
- `src/Wanxiangshu/Mission/Relay/Facts.fs(.fsi)`：单 append transaction envelope。
- `src/Wanxiangshu/Mission/Relay/Fold.fs(.fsi)`：纯 fold，拒绝双 active、retired resurrection、第二 assessment 与 cut 前 successor。
- `src/Wanxiangshu/Mission/Relay/Workflow.fs(.fsi)`：唯一任期推进 owner。

## 依赖关系

DEPENDS ON:
- `participant-identity`
- `durable-events`
- `interaction-authority`

## 验证

| 命题 | executable proof |
|---|---|
| RELAY-001/005/006 | `requirements/relay-incumbency/tests/fold.test.mjs` |
| RELAY-002/003/004 | `requirements/relay-incumbency/tests/fold.test.mjs` |
| RELAY-007 | `requirements/relay-retirement/tests/nudge.test.mjs` |
| RELAY-008 | `requirements/relay-assessment/tests/certificate.test.mjs` |


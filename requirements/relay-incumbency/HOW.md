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
| RELAY-001 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-001] one open road admits at most one active incumbent` |
| RELAY-002 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-002] first incumbency opens on the same AuditPending state machine` |
| RELAY-003 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-003] successor incumbency starts AuditPending after committed retirement` |
| RELAY-004 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-004] low-score assessor takes work ownership in place without a new incumbency` |
| RELAY-005 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-005] retired incumbent cannot be activated again`；`requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-005] retired incumbent cannot receive an authority update`；`requirements/relay-incumbency/tests/retired-host-routing.test.mjs::WHAT[RELAY-005] stale retired provider runs stay absorbed across successor activation` |
| RELAY-006 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-006] successor requires committed retirement baton and cut` |
| RELAY-007 | `requirements/relay-retirement/tests/nudge.test.mjs::WHAT[RELAY-007] silent normal terminal schedules an exit nudge instead of ending the incumbency`；`requirements/relay-incumbency/tests/retired-host-routing.test.mjs::WHAT[RELAY-007] manager tool-call intermediate observations wait for idle before ordinary repair` |
| RELAY-008 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-008] authority update invalidates a perfect certificate without restoring work ownership`；`requirements/relay-assessment/tests/certificate.test.mjs::WHAT[RELAY-008] certificate invalidation is explicit and never reactivates its assessor` |
| RELAY-009 | `requirements/relay-incumbency/tests/fold.test.mjs::WHAT[RELAY-009] active authority update advances revision and snapshot exactly once`；`requirements/relay-incumbency/tests/authority-update-host.test.mjs::WHAT[RELAY-009] same-road charge uses exact physical authority admission before durable revision`；`requirements/relay-incumbency/tests/authority-update-host.test.mjs::WHAT[RELAY-009] commission forwards exact caller run and tool identities to authority update` |

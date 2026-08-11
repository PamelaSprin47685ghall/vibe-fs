# Strength — Proof

Strength 不是“能跑 Replica”即完成；每个 durable/authority boundary 必须有永久自动化证据。

## Domain / property

代表入口：`tests/unit/strength/authority-policy.test.mjs`、`frame-projection.test.mjs`、`predictor-rollout.test.mjs`、`lifecycle-recovery.test.mjs`。

| 义务 | Clause |
|---|---|
| Decision 纯且确定；ineligible/unknown cost/non-deep/fallback/reviewer/attached → K0 | STRENGTH-002、010 |
| K 只能 0/1/2，按 provider request 计；K2Margin > K1Margin 且有 evidence floor | STRENGTH-003、010 |
| control assignment restart-stable、与 predictor score 无关 | STRENGTH-010 |
| readonly set 恰为 Read/Glob/Grep；其它 role/request 组合 fail closed | STRENGTH-004、PROMPT-008 |
| bundle 只接收完整 allowed exchanges；digest 去 wire id 稳定；synthetic id 确定性 | STRENGTH-005 |
| Replica mirror 重定位 owner ToolCallId 后 semantic projection 等价；owner id 不跨 Session；media/orphan fail K0 | STRENGTH-009 |
| same Decision + same digest 幂等；same Decision + different digest 冲突 | STRENGTH-005、009 |
| Candidate wrong-target 不能 render；Promoted replay 幂等 | STRENGTH-006..009 |
| cost/byte/delay/risk 增大不会提高对应 value；Replica events 不成为 primary label | STRENGTH-010 |

## Projection

代表入口：`tests/unit/strength/projection-algebra.test.mjs`、`projection-adapter.test.mjs`。

必须覆盖：`UseStrengthMirror` 与普通 Work base selection 冲突；Strength insertion canonical order 与注册顺序无关；多个 Promoted 的 `BeforeMessageIndex` 均以原始 base 为绝对锚；Candidate 只插 target run；Promoted 插在 target assistant 之前；Strength frames 在 pair marker 之前；Candidate 不走 early replay；Promoted 不能反射到 Replica mirror；同 anchor 不同 payload → ProjectionConflict。

## EventStore / fold

代表入口：`tests/unit/strength/store.test.mjs`、`durability-port.test.mjs`、`commit-promotion.test.mjs`。

必须覆盖：Prepared same digest+payload_refs 幂等；same Decision different digest/refs 拒绝；Promoted without Prepared、wrong run、wrong digest 拒绝；Promoted 重复幂等；Traced before Promoted 拒绝；XTrace range 单调；live projection 与 restart fold 相同；Strength material 只通过 committed EventStore `payload_refs` closure 存在，不写 Journal NDJSON/RuntimePath blob；Host/Application 只依赖 `StrengthDurabilityPort`，unified-store gate 禁止 AgentJournal + EventStore dual-write。

CommitUnknown 必须有受控端口测试：Prepared/Promoted 分别验证“重读证明存在→继续、证明不存在→按合同 K0、仍未知→fail closed”。

## Integration

代表入口：`tests/unit/strength/replica-transform.test.mjs`、`runtime.test.mjs`、`tests/integration/strength/lifecycle.test.mjs`。

至少覆盖：K1 readonly batch→primary consume→Promotion→restart/continuation 仍见 frame；K2 两个 request batch与单 request 并发多 tool；达到 K 后下一 provider request 物理 abort；Replica text-out；伪造 write/execute/network 被 execution gate 拒绝；Replica provider failure不动 owner FallbackCursor；AttemptAborted/SessionDeleted 级联取消 owner Replica；promotion crash recovery；Companion 只在 Promotion 后 ingestion；Traced 被 `IngestedThroughSequence` 覆盖后 raw replay 才退休；Candidate 永不进入 XTrace/LWR/PrefixSnapshot。

## Host canary

Host/OpenCode 版本门禁必须机械验证：

1. Root Work transform 等待 InternalLeaf StrengthReplica 不死锁。
2. 达 K 后 K+1 provider request 物理不外发。
3. transform 能唯一绑定 TargetProviderRun；不能绑定时 K0。
4. provider schema 恰为 read/glob/grep。
5. execution gate 对 write/edit/executor/fork/join/network fail closed。
6. Replica 不产生 permission ask。
7. deep owner 与 fast Replica role system prompt 语义一致，无 Strength 身份提示。
8. owner→Replica message semantic projection 等价；Replica model/tools/profile 不被 owner 覆盖。
9. 同 Decision replay synthetic ids/bytes 相同。
10. 未 Promoted Candidate 不进 XTrace/Companion/LWR。
11. Promotion 后 crash/restart，下一 request 仍有等价历史。
12. Strength tool-result anchor 后仍有 PairProgrammingThought marker。
13. ReviewSeal 覆盖最终 bytes，Reviewer 恒 K0。
14. StrengthReplica 不创建 Companion/SyncDelegate/嵌套 Replica，不进入 fork/list/join surface，且不存在 `SatelliteKind.Replica`。
15. Host/OpenCode version fingerprint 变化时 canary 重新成立；任一关键项失败使新 decision K0。

## Statistical / rollout

`tests/unit/strength/predictor-rollout.test.mjs` 证明 predictor/control/value 的确定性；`host-policy.test.mjs` 证明 treatment canary 与当前 OpenCode/plugin 版本指纹绑定且 process fuse 不可被普通 session cleanup 清零。

默认 rollout = Shadow；仓库**不宣称**已有正收益 cohort。K1 treatment 只有显式成本、exact Host canary fingerprint、deterministic control 与足够 predictor evidence 同时成立才可能启用；没有外部稳定 cohort 证据时保持 K0/Shadow。K2 必须独立通过更高 margin/evidence 与稳定窗口，不继承 K1 的任何结论。

## 仓库门禁

修改完成后执行：

```text
node scripts/checks/spec.mjs
npm run lint
npm run build
npm test
npm run test:integration
```

涉及 Host canary / package boundary 时继续执行对应 integration/e2e/package 正式入口。禁止临时 probe 代替永久测试；任何发现的回归必须落入仓库测试。

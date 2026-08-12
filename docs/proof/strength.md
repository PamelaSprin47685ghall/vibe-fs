# Strength — Proof

Strength 不是“能跑 Replica”即完成；每个 durable/authority boundary 必须有永久自动化证据。

## §31 最终不变量

| # | 不变量 | Clause | 机械证明 |
|---|---|---|---|
| 1 | Strength disabled / K0 → 普通 Work provider-visible bytes 与控制流无变化 | STRENGTH-001 | `host-policy.test.mjs`、`host-canary-k0.test.mjs` |
| 2 | StrengthReplica = InternalLeaf × Attached(StrengthReplica)；无 Companion / SyncDelegate / 嵌套 Replica；禁止 `SatelliteKind.Replica` | STRENGTH-004/014 | `host-canary-k0.test.mjs`、`runtime.test.mjs` |
| 3 | 没有 Replica Role / fast-replica / deep-replica Agent | STRENGTH-004/015 | `authority-policy.test.mjs`、`student-teacher-absence` ratchet |
| 4 | Replica = `fast-<owner-role>`；**继承** owner SessionPersona / SessionProviderLanguage；只换 ExecutionBinding；system prompt 仍由 owner CanonicalRole 决定；不换人、不换世界语 | STRENGTH-004/015、FALLBACK-014、AGENT-028/029 | `host-canary-k0.test.mjs` same-role prompt + Persona |
| 5 | schema 与 execution gate 恰为 `read/glob/grep` | STRENGTH-004、PROMPT-008 | `authority-policy.test.mjs`、`runtime.test.mjs`、`host-canary-k0.test.mjs` |
| 6 | K 单位是 provider request；K ∈ {0,1,2} | STRENGTH-003 | `replica-transform.test.mjs`、`batch-collector.test.mjs` |
| 7 | Candidate 消费前不进 XTrace / Companion / LWR / PrefixSnapshot | STRENGTH-006/008 | `lifecycle-recovery.test.mjs`、`tests/integration/strength/lifecycle.test.mjs` |
| 8 | Candidate 只对绑定 TargetProviderRun 可见 | STRENGTH-006 | `projection-algebra.test.mjs`、`commit-promotion.test.mjs` |
| 9 | 只有该 run 的真实 provider output 才能 Promotion | STRENGTH-007 | `lifecycle-recovery.test.mjs`、`turn-evidence.test.mjs` |
| 10 | Promoted 在 crash/restart/continuation 后不从语义历史消失 | STRENGTH-007/008 | `tests/integration/strength/lifecycle.test.mjs` |
| 11 | Promoted 最终进入 owner XTrace，并可被 Companion coverage 消化 | STRENGTH-008 | `lifecycle-recovery.test.mjs` traced + `needsRawReplay` |
| 12 | 跨 Session 只比较 Semantic projection；wire id 确定性 localize；机制 provenance 不进模型字节；Strength/Fallback 不改变 Agent self-identity | STRENGTH-009/012、FALLBACK-014、ARCH-016 Gate D | `frame-projection.test.mjs`、`projection-adapter.test.mjs`、`invisibility.test.mjs`；`tests/unit/invariants/prompt-stability.test.mjs` |
| 13 | Replica 失败不推进 owner FallbackCursor，不触发 InteractionRepair | STRENGTH-004/019、FALLBACK-004 | `authority-policy.test.mjs`；`attempt-plan.test.mjs` |
| 14 | Review / Finality / Attached / InternalLeaf 第一版恒 K0 | STRENGTH-002/019 | `host-canary-k0.test.mjs` |
| 15 | PairProgrammingThought 覆盖 Strength tool-result anchor；ReviewSeal 仍最后 | STRENGTH-009 | `projection-algebra.test.mjs`；Host 顺序见 `how/host.md` |
| 16 | 普通 pre-commit 失败 fail-open K0；durable / consumed-history 歧义 fail-closed | STRENGTH-006/007/011 | `commit-promotion.test.mjs`、`durability-port.test.mjs` |
| 17 | Predictor 不把 Replica intervention 当 primary counterfactual label | STRENGTH-010 | `predictor-rollout.test.mjs` |
| 18 | control assignment restart-stable，不由 predictor score 选择 | STRENGTH-010 | `predictor-rollout.test.mjs` |
| 19 | lifecycle 状态只从 EventStore 事实与 projection 推导；无 Stage/Phase | STRENGTH-013/017 | `store.test.mjs`、`lifecycle-recovery.test.mjs` |
| 20 | Host canary 失败 → 新 decision K0；已 Promoted 仍可恢复 | STRENGTH-011 | `host-policy.test.mjs` fuse + fingerprint；`lifecycle.test.mjs` replay |
| 21 | 大 material 只经 EventStore `payload_refs`；无 Journal NDJSON / RuntimePath blob / 独立 storage ref | STRENGTH-006/017 | `store.test.mjs`、`durability-port.test.mjs`、`unified-store-gate` |

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

代表入口：`tests/unit/strength/projection-algebra.test.mjs`、`projection-adapter.test.mjs`、`invisibility.test.mjs`。

必须覆盖：`UseStrengthMirror` 与普通 Work base selection 冲突；Strength insertion canonical order 与注册顺序无关；多个 Promoted 的 `BeforeMessageIndex` 均以原始 base 为绝对锚；Candidate 只插 target run；Promoted 插在 target assistant 之前；Strength frames 在 pair marker 之前；Candidate 不走 early replay；Promoted 不能反射到 Replica mirror；同 anchor 不同 payload → ProjectionConflict。

## EventStore / fold

代表入口：`tests/unit/strength/store.test.mjs`、`durability-port.test.mjs`、`commit-promotion.test.mjs`。

必须覆盖：Prepared same digest+payload_refs 幂等；same Decision different digest/refs 拒绝；Promoted without Prepared、wrong run、wrong digest 拒绝；Promoted 重复幂等；Traced before Promoted 拒绝；XTrace range 单调；live projection 与 restart fold 相同；Strength material 只通过 committed EventStore `payload_refs` closure 存在，不写 Journal NDJSON/RuntimePath blob；Host/Application 只依赖 `StrengthDurabilityPort`，unified-store gate 禁止 AgentJournal + EventStore dual-write。

CommitUnknown 必须有受控端口测试：Prepared/Promoted 分别验证“重读证明存在→继续、证明不存在→按合同 K0、仍未知→fail closed”。

## Integration

代表入口：`tests/unit/strength/replica-transform.test.mjs`、`runtime.test.mjs`、`tests/integration/strength/lifecycle.test.mjs`。

至少覆盖：K1 readonly batch→primary consume→Promotion→restart/continuation 仍见 frame；K2 两个 request batch与单 request 并发多 tool；达到 K 后下一 provider request 物理 abort；Replica text-out；伪造 write/execute/network 被 execution gate 拒绝；Replica provider failure不动 owner FallbackCursor；AttemptAborted/SessionDeleted 级联取消 owner Replica；promotion crash recovery；Companion 只在 Promotion 后 ingestion；Traced 被 `IngestedThroughSequence` 覆盖后 raw replay 才退休；Candidate 永不进入 XTrace/LWR/PrefixSnapshot。

## Host canary

Host/OpenCode 版本门禁必须机械验证。默认生产路径 canary 不健康 → 新 decision K0（STRENGTH-011）。

真实 Host + mock LLM 的 request-budget / nested-session dry-run：`tests/e2e/entry.test.mjs` long-stroke `strength-canary-*`（`WANXIANGSHU_STRENGTH_MODE=dry-run`，K2 恰好两轮 Replica request，第 3 轮物理不外发；不注入 primary，故 `StrengthCandidatePrepared=0`）。

其余项由 unit / integration 覆盖；permission popup、ReviewSeal 最终 bytes、OpenCode 版本升级重跑仍绑定 `WANXIANGSHU_STRENGTH_HOST_CANARY` fingerprint，通用 `true/pass` 无效。

1. Root Work transform 等待 InternalLeaf StrengthReplica 不死锁。
2. 达 K 后 K+1 provider request 物理不外发。
3. transform 能唯一绑定 TargetProviderRun；不能绑定时 K0。
4. provider schema 恰为 read/glob/grep。
5. execution gate 对 write/edit/run/fork/horizon/join/network fail closed。
6. Replica 不产生 permission ask。
7. deep owner 与 fast Replica role system prompt 语义一致；Persona / language 继承 owner；无 Strength 身份提示。
8. owner→Replica message semantic projection 等价；Replica model/tools/profile 不被 owner 覆盖。
9. 同 Decision replay synthetic ids/bytes 相同。
10. 未 Promoted Candidate 不进 XTrace/Companion/LWR。
11. Promotion 后 crash/restart，下一 request 仍有等价历史。
12. Strength tool-result anchor 后仍有 PairProgrammingThought marker。
13. ReviewSeal 覆盖最终 bytes，Reviewer 恒 K0。
14. StrengthReplica 不创建 Companion/SyncDelegate/嵌套 Replica，不进入 fork/horizon/join surface，且不存在 `SatelliteKind.Replica`。
15. Host/OpenCode version fingerprint 变化时 canary 重新成立；任一关键项失败使新 decision K0。
16. Strength/Fallback 推进后 owner system prompt 字节与 SessionPersona 不变（Gate D）。

## Statistical / rollout

`tests/unit/strength/predictor-rollout.test.mjs` 证明 predictor/control/value 的确定性；`host-policy.test.mjs` 证明 treatment canary 与当前 OpenCode/plugin 版本指纹绑定且 process fuse 不可被普通 session cleanup 清零。

默认 rollout = Shadow（STRENGTH-010）。仓库**不宣称**已有正收益 cohort，也不把架构闭环当成 treatment 启用。K1 treatment 只有显式成本、exact Host canary fingerprint、deterministic control 与足够 predictor evidence 同时成立才可能启用；没有外部稳定 cohort 证据时保持 K0/Shadow。质量熔断与 canary 失败同样只禁止**新** speculation；已 Promoted 历史继续 replay。K2 必须独立通过更高 margin/evidence 与稳定窗口，不继承 K1 的任何结论。

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

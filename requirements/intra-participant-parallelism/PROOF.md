# PROOF：intra-participant-parallelism

下表每条 assertion 归本包唯一 ownership。测试先写并冻结，再写 production；本次 Active execution 不先运行 RED。落点锚点为 test 标题（`tests/<file>.mjs::<test title>`），均真实存在。

| WHAT | 落点 | 证明内容 |
|---|---|---|
| INTRA-PARTICIPANT-PARALLELISM-001 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-001] lanes carry no provider-visible identity or handle and keep the same logical participant` | lane/group 不产生 public participant identity；owner id 独立于 physical lane ids；`Surface` 输出为 JS-native plain data，不含 lane session id |
| INTRA-PARTICIPANT-PARALLELISM-002 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-002] canonical parser normalizes only newline shape and preserves lane text` | newline normalization、N≥2、empty-line refusal、space preservation |
| INTRA-PARTICIPANT-PARALLELISM-003 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-003] admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input`；`tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-003] sibling creation is a distinct Host capability from managed-child creation` | fresh sessions；每 lane parent == old caller parent；prompt 含 canonical LWR + exact lane input；不用 Host fork |
| INTRA-PARTICIPANT-PARALLELISM-004 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-004] partial create or start failure rolls back every created lane and never interrupts old caller` | 任一 create/send fail → rollback created lanes、old caller不 abort |
| INTRA-PARTICIPANT-PARALLELISM-005 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] old caller silent-interrupts only after every lane started`；`tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] failed silent interrupt rolls back lanes and old caller stays out of active set`；`tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-005] FissionRuntime preserves silent interrupt across multiple checks and is cleared only by clearOwner/clearSilentInterrupt` | 全 lane admitted 后才 silent interrupt；silent abort 不 terminal/cascade |
| INTRA-PARTICIPANT-PARALLELISM-006 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-006] pre-fission completion broadcasts to every lane exactly once with idempotent delivery` | pre-fission completion target = every lane exactly once，重复 delivery 幂等 |
| INTRA-PARTICIPANT-PARALLELISM-007 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-007] post-fission completion has exactly one affinity target: the initiating lane` | post-fission affinity 只指 initiating lane |
| INTRA-PARTICIPANT-PARALLELISM-008 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-008] keyed work bundle is idempotent and rejects conflicting records for one lane` | keyed bundle union 幂等；same key/different ref fail closed；顺序不影响 keys |
| INTRA-PARTICIPANT-PARALLELISM-009 | `tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] convergence requires all lane records and all completion deliveries`；`tests/fission-domain.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] ring successor wraps and forwards past already-closed lanes to the next live present`；`tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] observeLaneTurn and OrdinaryTurnWorkflow absorb Fission-replaced owner turns without sending continuations`；`tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-009] Host convergence performs ring takeover before reporting the old logical owner` | complete set 才可进入 takeover；closed successor 按 ring 机械跳过；最终 present 消费完整 handoff 后以真实 ordinary final prose 回填 old logical owner completion cell；旧 owner turn 继续静默吸收 |
| INTRA-PARTICIPANT-PARALLELISM-010 | `tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors` | Fission durable fact/projection/recovery anchor 存在；禁止 session-fork guessing path |
| INTRA-PARTICIPANT-PARALLELISM-011 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-011] second admission while active is rejected as AlreadyFissioned until release` | same owner second active admission → AlreadyFissioned |
| INTRA-PARTICIPANT-PARALLELISM-012 | `tests/fission-source-ratchet.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility comes from ToolPermission.Fission for current office vocabulary` | role matrix entitlement 与 registry gate 同一 `ToolPermission.Fission` source；fast/deep 不分叉 |
| INTRA-PARTICIPANT-PARALLELISM-013 | `tests/fission-runtime.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-013] user-facing root caller is rejected before fission reserves or creates anything` | physical parent absent 的 user-facing/root caller fail closed；只允许 parent lookup，未 reserve、未读 LWR、未 create lane、未 interrupt |

## Focused acceptance

```text
node --test requirements/intra-participant-parallelism/tests/*.test.mjs
```

本次不以 full repository suite 作为此 GAP 的关闭条件；全仓 gate 由 verification-system 的正常 release 流程承担。

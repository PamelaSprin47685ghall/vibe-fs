# PROOF：intra-participant-parallelism

下表每条 assertion 归本包唯一 ownership。测试先写并冻结，再写 production；本次 Active execution 不先运行 RED。

| WHAT | 落点 | 证明内容 |
|---|---|---|
| INTRA-PARTICIPANT-PARALLELISM-001 | `tests/fission-domain.test.mjs` | lane/group 不产生 public participant identity；owner id 独立于 physical lane ids |
| INTRA-PARTICIPANT-PARALLELISM-002 | `tests/fission-domain.test.mjs` | newline normalization、N≥2、empty-line refusal、space preservation |
| INTRA-PARTICIPANT-PARALLELISM-003 | `tests/fission-runtime.test.mjs` | fresh sessions；每 lane parent == old caller parent；prompt 含 canonical LWR + exact lane input；不用 Host fork |
| INTRA-PARTICIPANT-PARALLELISM-004 | `tests/fission-runtime.test.mjs` | 任一 create/send fail → rollback created lanes、old caller不 abort |
| INTRA-PARTICIPANT-PARALLELISM-005 | `tests/fission-runtime.test.mjs` | 全 lane admitted 后才 silent interrupt；silent abort 不 terminal/cascade |
| INTRA-PARTICIPANT-PARALLELISM-006 | `tests/fission-domain.test.mjs`, `tests/fission-runtime.test.mjs` | pre-fission completion target = every lane exactly once，重复 delivery 幂等 |
| INTRA-PARTICIPANT-PARALLELISM-007 | `tests/fission-domain.test.mjs` | post-fission affinity 只指 initiating lane |
| INTRA-PARTICIPANT-PARALLELISM-008 | `tests/fission-domain.test.mjs` | keyed bundle union 幂等；same key/different ref fail closed；顺序不影响 keys |
| INTRA-PARTICIPANT-PARALLELISM-009 | `tests/fission-domain.test.mjs`, `tests/fission-runtime.test.mjs` | complete set 才可 converge；logical owner terminal at most once |
| INTRA-PARTICIPANT-PARALLELISM-010 | `tests/fission-source-ratchet.test.mjs` | Fission durable fact/projection/recovery anchor 存在；禁止 session-fork guessing path |
| INTRA-PARTICIPANT-PARALLELISM-011 | `tests/fission-runtime.test.mjs` | same owner second active admission → AlreadyFissioned |
| INTRA-PARTICIPANT-PARALLELISM-012 | `tests/fission-source-ratchet.test.mjs` | role matrix entitlement 与 registry gate 同一 `ToolPermission.Fission` source；fast/deep 不分叉 |

## Focused acceptance

```text
node --test requirements/intra-participant-parallelism/tests/*.test.mjs
```

本次不以 full repository suite 作为此 GAP 的关闭条件；全仓 gate 由 verification-system 的正常 release 流程承担。

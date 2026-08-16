# intra-participant-parallelism

## 一句话 WHY

同一个 participant 需要在不分裂 identity / authority / responsibility 的前提下拥有多个并发 present，并在任意完成顺序、失败和恢复下无损地收敛回一次普通完成。

## WHAT 概览

- Fission 只增加 execution presents，不增加 logical participant（IPP-001）。
- `fission(prompts)` 以 canonical line parser 定义 lane set；N≥2，空 lane fail closed（IPP-002）。
- V1 只允许已有 physical parent 的 subsession Fission；user-facing/root caller fail closed。物理替换使用 fresh sibling Host sessions：`parent(lane)=parent(old caller)`；启动材料是 caller canonical LWR + exact lane input，不使用 Host session fork（IPP-003/013）。
- admission all-or-none；全部 lanes 成功建立后 old caller 才 silent interrupt（IPP-004/005）。
- fission 前已 outstanding 的 subagent/PTY completion 属于 logical owner，exactly-once-per-lane 广播；fission 后新发起的 completion 绑定 initiating lane（IPP-006/007）。
- lane work 用 keyed bundle / deterministic forwarding 收敛；parent 只收到一次 terminal completion（IPP-008/009）。
- active group、lane identity、delivery/convergence facts 可 durable replay；restart 不猜 lane（IPP-010）。
- V1 禁 nested fission；office entitlement 与 runtime/schema role gate 同源；subsession origin 另由 admission 强制（IPP-011/012/013）。

## DEPENDS ON

`participant-identity`, `session-ontology`, `managed-session-lifecycle`, `office-capability`, `capability-enforcement`, `participant-horizon`, `work-record`, `process-execution`, `durable-events`, `crash-reconciliation`.

## 边界（DOES NOT OWN）

Role/Persona/Binding 本体（`participant-identity`）；role consequence catalog（`office-capability`）；session 通用 create/cancel/retire（`managed-session-lifecycle`）；LWR 内容格式（`work-record`）；PTY 本体（`process-execution`）；通用 EventStore/fold substrate（`durable-events`）；通用 crash reconciliation（`crash-reconciliation`）；provider representation（`provider-projection`）。

## 阅读顺序

1. `WHY.md`
2. `WHAT.md`
3. `HOW.md`
4. `PROOF.md`

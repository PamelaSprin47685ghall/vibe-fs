# Ontology audit ledger

本文件记录当前设计仍需继续反向覆盖的地方。这里的 `ORPHAN` / `OVERLAP` / `GARBAGE` 是设计状态，不是产品 verdict。

## Resolved discoveries from the initial 36-package workset

### Added as independent packages

1. `provider-language`
   - WHY 与 identity/horizon/projection 都不同：一个 life 的自然语言世界必须稳定，而 protocol identity 不翻译。
2. `effect-accounting`
   - Requested / outcome-unknown / Accepted 是跨 Prompt、Git publish、repository transaction 的共同副作用语义，不应藏在 EventStore。
3. `action-affordance`
   - 调用瞬间的 semantic act contract 与长期 Role/Library cognition 可独立重写。
4. `output-distillation`
   - 控制 process 与诚实压缩大输出不是一个 WHY。
5. `capability-enforcement`
   - office authority 是“有资格造成什么”；schema/runtime gate 同源是“系统怎样确保看见的能力与执行能力一致”。
6. `external-investigation`
   - public web/external facts 的 provenance/disagreement/visual evidence 与 local repository investigation 不同。

### Split

- `participant-guidance` → `cognitive-environment` + `action-affordance`。
- `review-protocol` → `review-judgement` + `review-assurance`。
- `recovery` → `provider-attempt-recovery` + `crash-reconciliation`。

### Renamed away from implementation/component names

- `sphinx` → `epistemic-reasoning`。Sphinx/F#/MCP/A*/MCTS/Bayes 都是现行 implementation/proof evidence，不是 package identity。

## ORPHAN / deferred candidates

### `intra-participant-parallelism` / Fission — DEFERRED

仓库已经有世界观命题“一种 identity 可有 several presents”，Manager 也暴露 `fission` surface；但当前 `FissionTool.fs` 明确是 MVP，合法请求恒以 capacity consequence 拒绝，multi-lane runtime 尚未存在。

目前不立永久 package：

- 若未来真正实现“同一 participant、同一 authority、多个 independent presents、最终再收敛”的产品行为，它有独立 WHY，应该成为 `intra-participant-parallelism`。
- 当前只把 Fission 的 action-surface truth 视为 `action-affordance` / current product surface evidence；“future lane engine”不得提前写成 Requirement。

### Pair-programming guideline / NEEDHELP — WATCH

当前 HOST-013、Pair Hint 与 NEEDHELP 横跨长期 craft、runtime delivery、assistance continuation：

- craft meaning 暂归 `cognitive-environment`；
- action/office boundary 暂归 `action-affordance` / `office-capability`；
- wire injection 暂归 `provider-projection` + `prefix-stability`；
- consultation 暂归 `delegation`；
- authority continuity 归 `interaction-authority`。

若全仓 reverse coverage 发现一条无法被这些 guarantees 组合解释的独立 WHY，再拆 `collaboration-guidance`，现在不先造包。

## Absorbed former topics (not standalone packages)

### Companion / Blogger topology

当前 `每个 Work Session 恰好一个 leaf Companion Y` 很重要，但更像现行 shape：

- Work/InternalLeaf + Attached ontology → `session-ontology`；
- managed Y lifecycle → `managed-session-lifecycle`；
- canonical work-history capture → `semantic-trace`；
- Y-based summary/compression → `context-compression`；
- Blogger diagnosis → `behavior-diagnosis`；
- delivery coverage → `guidance-delivery`。

Independent-change test：未来可用 deterministic/in-process summarizer 替代 physical Blogger leaf，而上述 product guarantees 均可保持。因此暂不设 `companion` package。

### Synthetic TOML

TOML comment/field、literal/basic escaping、value tree 是 representation HOW。其重要 WHAT 分散为：

- instruction/data 不互相冒充 → `participant-horizon` / `provider-projection`；
- machine identifiers 不翻译 → `provider-language`；
- representation 不反解 authority → `interaction-authority` + `provider-projection`。

不设 `synthetic-toml` package。

### Agent catalog / MCP integration

- Role/Persona/Binding → `participant-identity`。
- office consequence → `office-capability`。
- schema/gate parity → `capability-enforcement`。
- Browser/Sphinx/Semble 分别由 `external-investigation` / `epistemic-reasoning` / `repository-investigation` 的 WHAT 解释；具体 MCP/client/config 属 Host adapter HOW。

不设 `agent-catalog` / `mcp` package。

## Known OVERLAP in current docs/tests

> Phase A（Clause-by-Clause 全仓反向覆盖）已完：`docs/what/` 全部 25 文件（含 shape/how 同前缀条款）逐 proposition 判 owner，累计 ~418 条款。结论：0 新包、0 ORPHAN、0 OVERLAP、0 dependency delta；全部 NEEDS-SPLIT 均被现有 45 包分解吸收；GARBAGE/HOW 集中确认（legacy absence、exact catalog、tuning 常数、wire 机制）。完整 ledger 见 `COVERAGE.md`。

### `docs/what/prompt.md`

至少混合：

- interaction authority；
- dispatch semantics；
- participant identity stability；
- cognitive environment；
- provider language；
- action affordance；
- Todo/Finality runtime guidance。

未来不得整体迁成一个 package。

Clause 级结果见 `COVERAGE.md`：PROMPT-001..007/009/010/011/015..021 单 owner；
PROMPT-008/013/014 标 NEEDS-SPLIT 但已被现有包分解吸收（interaction-authority / capability-enforcement / prefix-stability / host-boundary / obligation-ledger / participant-horizon / finality / participant-identity / provider-language）；
PROMPT-012 全条判 GARBAGE（Student/Teacher migration absence）。本轮无新包、无 ORPHAN。

### `docs/what/agent.md`

至少混合：

- identity/catalog；
- office capability；
- capability enforcement；
- delegation topology；
- repository/external investigation；
- epistemic reasoning integration；
- warm-start optimization。

Clause 级结果见 `COVERAGE.md`：AGENT-002（22 catalog）/AGENT-004（legacy names）/AGENT-020..022（Student absence）判 GARBAGE；
15 条 NEEDS-SPLIT（AGENT-001/009/011..016/023..026/030..032）均被现有包分解；
AGENT-031（NEEDHELP）仍维持 WATCH，未发现独立 WHY，不立 `collaboration-guidance`。

### `docs/what/host.md`

至少混合：Host capability assumptions、session ontology/lifecycle、ProviderLanguage、pair guideline projection、Magic Todo membrane、provider/tool physical identity、compaction/reanchor、NEEDHELP assistance。

Clause 级结果见 `COVERAGE.md`：HOST-001..003/005/007..009/011/012/016/022..026 单 owner；
11 条 NEEDS-SPLIT（HOST-004/006/010/013/015/017..021/027）均被现有包分解；
HOST-014（Student/Teacher absence）+ HOST-008 历史字段判 GARBAGE。NEEDHELP（HOST-027）维持 WATCH，不立新包。

### `docs/what/companion.md`

至少混合：XTrace / WorkRecord / coverage / Opening / compression。

Clause 级结果见 `COVERAGE.md`：12 单 owner（topology→`session-ontology`、frame/squash→`context-compression`、WorkRecord→`work-record`、prefix→`prefix-stability`）；
COMPANION-003/007/008 标 NEEDS-SPLIT 但均被现有包分解。无一条需要独立 `companion` package（HANDOFF §11.1 维持）。

### `docs/what/execution.md`

至少混合：delegation（fork/commission/inspect/sync）、process-execution（PTY/run）、output-distillation（Distiller）、work-record（LWR）、participant-horizon（leak 禁令）、managed-session-lifecycle（handle/child）。

Clause 级结果见 `COVERAGE.md`：23 单 owner；8 条 NEEDS-SPLIT（EXEC-004/005/014/016/017/026/028/031）均被现有包分解；EXEC-027（Student absence）+ 已删算法面判 GARBAGE。无独立 `sync-delegate` package。

### `docs/what/architecture.md`

- Horizon → `participant-horizon`。
- Office Capability → `office-capability`。
- Static gates → 各 semantic owner 的 proof + `verification-system`，不能继续由 Architecture 统治所有事实。

### `tests/unit/prompt/authority.test.mjs`

至少应裂成：

- `interaction-authority` oracle；
- `dispatch-protocol` oracle；
- 旧 agent-name absence 若仅是 migration ratchet → delete/compatibility proof，而非未来 package proof。

### `tests/unit/resources/prompt-semantic-depth.test.mjs` + `scripts/checks/semantic-anchors.mjs`

当前一个 catalog 同时证明：

- `cognitive-environment`；
- `office-capability` 的 mirrored projection；
- `action-affordance`；
- `epistemic-reasoning` / review craft 等 domain cognition。

未来 semantic anchor 可共享机械 checker，但每条 semantic oracle 必须有唯一 package owner。

### `tests/unit/verify/language-parity-gate.test.mjs`

当前混合 language parity、Role cognition、tool affordance、Office capability projection。未来：

- locale leaf/placeholder/identifier parity → `provider-language`；
- Role semantic meaning → respective semantic owner；
- tool action contract → `action-affordance`；
- Office consequence projection parity → `office-capability`。

### `projection-algebra.test.mjs`

algebra/order/conflict/deterministic rendering → `provider-projection`；具体 Review/Repair/Companion/Strength intent 的“为什么存在”仍由各 semantic package owner 证明。

## GARBAGE / compatibility / HOW candidates

以下目前不得直接升级为永久 WHAT：

- “必须恰好 22 个 agent”及具体 `fast-*`/`deep-*` machine names。
- Student/Teacher/Meditator/Executor 等 legacy absence ratchet；最终 clean world 只需定义合法新 vocabulary，不应永远背负旧名单。
- exact Persona display names（Integrator/Director/...），除非产品明确把命名本身当 public contract。
- OpenCode hook 名、callback 参数 shape、F# module/file path。
- MCP server 当前 repo URL/ref、`uvx` command、test fixture env vars。
- Semble `MaxKeywords=8` / `TopK=4` / `64 KiB` 等现行 tuning numbers，除非证明数值本身影响产品 guarantee。
- Prompt recovery `TailWindow=50` / `Budget=3` 这类可调 HOW；未来 WHAT 只要求 bounded + no blind resend。
- Context `200 KiB` 若只是 input safety bound，而非用户可观察产品承诺。
- current `ProjectionSnapshot` 字段集合、ProjectionIntent case 名。
- Synthetic TOML 具体语法与 quote strategy。
- migration double-renderer absence / historical canary scaffolding。
- `SuppressTransportOnly` 某个 Change 的 deferred wiring history。
- current Fission MVP “capacity refusal”作为长期未来 parallelism ontology。
- `resources/prompts` / catalog.json 等已删除路径的永久 absence tests；迁移完成后应由现行 resource closure 正面定义取代。

## Boundary questions still open

1. `managed-session-lifecycle → crash-reconciliation` 的依赖方向是否需要改成两个更窄 guarantees，避免 lifecycle-specific restore 与 generic crash epistemology互相引用。
2. `distribution` 与 `provider-language` 的 resource closure 是否只需 dependency edge，还是应有更一般的 `runtime-resource-integrity` package。当前先不拆。
3. Fission 实现真正出现后重新做 independent-change test，不允许因为已经有 `fission` tool name 就倒推 package 必然存在。
4. PROMPT-006 execution-binding 解析律（managed 冻结 / user-facing 追真实用户 / ExplicitExecutionOverride 单次 / fail-closed）当前归 `participant-identity`，但其 send 海关机制与 `dispatch-protocol`、`provider-attempt-recovery` 共用。语义 owner 唯一故暂不立包；若未来 dispatch 层需独立重写 binding 海关而不动 identity，重做 independent-change test。

## Recently resolved questions

- Phase A reverse coverage 全仓完成（见 `COVERAGE.md`）：45 包不需增删并；Fission/NEEDHELP/runtime-resource-integrity 三个 WATCH 项维持原 verdict。

- `semantic-trace` **不**依赖 `participant-horizon`：canonical trace 位于 horizon 之前，horizon 只治理后续 provider-visible projection/record delivery。
- bounded canonical LWR 已抽成独立 `work-record` package；`semantic-trace` 拥有原始历史，`context-compression` 拥有可替代 coverage，`work-record` 拥有 bounded cross-boundary statement。

## Phase B — WHY reverse audit（已完）

对 45 包逐包回 `docs/why/*.md`（25 文件）+ `changes/completed/`（41 份）做单-WHY / double-WHY / 假边界 / DOES-NOT-OWN-hardness 四问。完整 verdict 表见 `COVERAGE.md` Phase B 节。

### 结果

- **0 double-WHY、0 假边界、0 拆/并/增/删。** 45 包全部通过单 WHY + independent-change 测试。
- 假边界反例确认：`sphinx→epistemic-reasoning`、`companion`、`synthetic-toml`、`agent-catalog`、`mcp`、`sync-delegate` 均维持不立包（why-doc 无独立 WHY）。

### 本轮修复（2 处边界缺陷）

1. **OVERLAP（已修）**：`repository-programming` × `capability-enforcement` 曾同时 claim「capability → surface → runtime gate 四层同构」。裁决：同构/同源律唯一归 `capability-enforcement`（其 OWNS 已有「schema 与 runtime gate 读同一 capability truth」）；`repository-programming` 只应用它到 js-* 编程面，新增 `capability-enforcement` hard edge（17-repository.md / 20-capability-external.md）。依据：js-tools.md why「四层同构（capability → base-class method → description → example → runtime gate）保证模型看到的与可执行的完全一致」是通用同源律，不是编程面专有事实。
2. **假依赖（已删）**：`finality → managed-session-lifecycle`。life completion 触发的 dedicated reviewer session 退休由 `managed-session-lifecycle` 的 owner-closure 消费，是下游 effect，不是 finality 定义前提（15-mission-review.md）。

### 转入 Phase E 的弱依赖候选

- `structured-workflow → causal-wait`（CE builder implementation coupling）。
- `time-capability → causal-wait`（条件依赖「当等待需要 deadline 时」，非 hard）。
- `guidance-delivery → provider-projection`（渲染是下游机制）。
- `finality → participant-horizon`（horizon-respecting 薄依赖）。

### 非问题确认

- 「failure-domain separation」（tool red / semantic REVISE / infra fatal）是三态各归 `capability-enforcement` / `review-judgement` / `host-boundary`+`crash-reconciliation` 的硬边界纪律，不构成第四个独立 WHY；`review-assurance` 的「infra failure 不伪装 REVISE」是其 review-side 本地负边界。
- `INDEX.md` 依赖骨架是示意；精确 hard edge 以各 boundary card `DEPENDS ON` 为准（已加注），Phase E 统一重画。

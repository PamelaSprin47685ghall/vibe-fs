# participant-identity — 实现模型与约束（非 normative）

## 实现模型

身份轴的 canonical 事实全部落在 Domain/Kernel 层，发送路径只消费：

| 事实 | 实现 | 说明 |
|------|------|------|
| `Role` DU + `AgentTier` | `src/Wanxiangshu/Kernel/Roles.fs` | 10 个 office 身份 + Fast/Deep 两档 |
| 名称/peer/分组公式 | `src/Wanxiangshu/Domain/ManagedAgentCatalog.fs` | `nameOf`/`peerNameOf`/`managerForkableRoles`/`bookkeeperNames`；peer 是公式不是表 |
| Persona 解析 | `src/Wanxiangshu/Session/PersonaCatalog.fs` | `persona role tier`（Role × initial tier）；`bookkeeperPersona`；`inheritFrom` |
| Persona 冻结 | `PersonaCatalog.SessionPersona` | `bindOnce`：同值幂等、异值拒绝；`clearAllForTests`（测试钩子） |
| agent 名解析 | `src/Wanxiangshu/Domain/PromptAuthority.fs` `parseAgentNameTyped` | `fast-ROLE`/`deep-ROLE` → `{Name; Role; Tier; PeerName}`；legacy/未知/畸形三分拒绝 |
| prompt identity | `PromptAuthority.systemPromptIdFor` | 只依赖 `CanonicalRole`（tier 不参与，PID-005） |
| profile 组装 | `PromptAuthority.buildAttemptExecutionProfile` + `Domain/AttemptPlanner.fs` | `EffectiveAgent` 随 fallback cursor 动；`SystemPromptId`/Persona 不动 |
| binding 解析律 | `src/Wanxiangshu/OpenCode/Host/Sessions.fs`、`HostSignalBootstrap.fs`、`ChatParamsHook.fs` | `BindingIntent` = Preserve / ExplicitExecutionOverride；managed frozen / user-facing 追最近真实 EffectiveAgent；SendPrompt 只保留 agent 且 `Model=None`，物理 `chat.message` 以 `(SessionId, PhysicalUserMessageId)` 取得 current execution lease；`chat.params` 验证 chat.message 已记录的 exact binding，messages transform 再用 trailing physical id 复核（PROMPT-006） |
| 角色标签持久化 | `src/Wanxiangshu/Session/AgentRoleIdentity.fs` | `roleName` 委托 `ManagedAgentCatalog.roleLabel`，避免 DU 拼写改名破坏 durable string |

关键不变量：任何路径都**不得**在 `buildAttemptExecutionProfile` 之外另造身份字段；tier/EffectiveAgent
只影响 `EffectiveAgent` 一个字段（FALLBACK-004），Persona / SystemPromptId / ToolCapabilitySet 不随其变。

## 边界与弃权

### 不归本包（引用其它包）

- office 的 entitled consequence、权限矩阵内容 → `office-capability`、`capability-enforcement`。
- session 的 execution class（Work/InternalLeaf）× attachment（Attached/…）→ `session-ontology`
  （DEPENDS ON）。
- fallback/Strength 的预算与算法 → `provider-attempt-recovery` / `speculative-investigation`。
- system prompt 字节稳定性、prefix epoch → `prefix-stability`。

### GARBAGE / HOW 裁决（不进入 WHAT）

| 内容 | 裁决 | 理由 |
|------|------|------|
| AGENT-002「恰好 22 名」 | HOW（runtime contract） | COVERAGE：exact catalog + machine names = implementation vocabulary；managed catalog 仍精确覆盖 20 Role×tier + 2 Bookkeeper。旧“每名必须带非空 model 串”已由 `execution-model-routing` 的 MJS scheduler/lease 合同取代；model 不再是 agent inventory 字段。 |
| AGENT-004 非法旧名清单（orchestrator/meditator/student/…） | GARBAGE（migration ratchet） | legacy reject = 迁移证明；`catalog.test.mjs` 的 `AGENT_004_*` 断言保留作 ratchet，新世界基线稳定后可删 |
| Persona display 名（Integrator/Director/Coordinator/…） | HOW | AGENT-028 表是当前命名；除非命名成为 public contract，否则不构成 WHAT |
| `SessionPersona` process-local `Dictionary` | HOW | Phase 16 实现；durable journal fact 是未来演进，不改变「bind-once」语义 |
| `AgentRoleIdentity.ofRole/ofManaged/toRole` 恒等函数 | HOW | 只作 Host-wire 解析入口的显式类型通道 |

## 历史（考古摘要）

- 历史 change（universal）：Student/Teacher 删除后目录重排、Bookkeeper pair 条件化——
  session 部分归 `session-ontology`，身份轴（Role/Persona/Binding 分离）由此沉淀。
- 历史 why/agent 条款「备选与被拒」：Role=Persona=Binding 合一被拒（Bookkeeper 病理实例）；
  双层权限、内部 Agent 隐去、Persona 一次绑定各有独立失败模式记录。
- 历史 what/prompt PROMPT-006/014：binding 解析律与 Persona 冻结立法。

## 验证与测试落点

每条 WHAT 命题恰好一行落点。类型：`MOVE` = 本包 tests/ 下文件（物理移入）；`REUSE` = 留原处，
记精确断言锚点与 cutover 拆分计划。

### 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|----------|
| PID-001 | `tests/catalog.test.mjs` `WHAT[PID-001] catalog_has_exactly_ten_canonical_roles_and_two_tiers` + `WHAT[PID-001] required_names_are_exactly_ten_roles_times_two_tiers` | MOVE | `node --test requirements/participant-identity/tests/catalog.test.mjs` |
| PID-002 | `tests/catalog.test.mjs`（`WHAT[PID-002] all_legacy_bare_names_are_rejected`、`WHAT[PID-002] rejection_prose_is_version_agnostic`，legacy 合一名拒绝 = 三轴分离 ratchet）+ `tests/session-persona.test.mjs` `WHAT[PID-002] persona_matrix_is_an_independent_axis_from_role_and_binding` | MOVE | `node --test requirements/participant-identity/tests/{catalog,session-persona}.test.mjs` |
| PID-003 | `tests/session-persona.test.mjs` `WHAT[PID-003] SessionPersona_binds_once_same_value_idempotent_different_value_rejected` + `tests/persona-binding.test.mjs` `WHAT[PID-003] persona_binds_once_and_never_rewrites` | MOVE | `node --test requirements/participant-identity/tests/{session-persona,persona-binding}.test.mjs` |
| PID-004 | `tests/persona-binding.test.mjs` `WHAT[PID-004] persona_frozen_across_gate_d_events`（Gate D 场景后 persona 冻结 = 换执行者不换人；字节半在 prefix-stability） | MOVE | `node --test requirements/participant-identity/tests/persona-binding.test.mjs` |
| PID-005 | `tests/session-persona.test.mjs` `WHAT[PID-005] system_prompt_id_follows_canonical_role_not_effective_agent_tier`（identity 值 `doesNotMatch /fast\|deep/i`） | MOVE | `node --test requirements/participant-identity/tests/session-persona.test.mjs` |
| PID-006 | `tests/session-persona.test.mjs` `WHAT[PID-006] binding_wire_names_are_machine_routing_identity_not_persona_self_claim`（persona 值非 wire 名、不含 `fast-`/`deep-` 前缀；prompt identity 不含 binding 名）；horizon 侧拦截由 `participant-horizon` Gate B 承担（交叉引用） | MOVE | 同上 |
| PID-007 | `tests/catalog.test.mjs` `WHAT[PID-007] peer_is_same_role_opposite_tier_and_symmetric`；Bookkeeper pair 同律见 `WHAT[PID-009] bookkeeper_pair_has_machine_identity_and_peer_but_no_public_role` | MOVE；旧 pair-model 互异 proof 已废弃，model equality 归 `execution-model-routing` EMR-008 | `node --test requirements/participant-identity/tests/catalog.test.mjs` |
| PID-008 | `tests/session-execution-binding.test.mjs` `WHAT[PID-008] root_requires_external_agent_proof_then_model_is_scheduler_owned`、`WHAT[PID-008] parented_session_uses_stable_agent_lease_and_authorized_peer_only`、`WHAT[PID-008] provider_reasoning_variant_must_match_the_exact_lease`（证明 EffectiveAgent preserve/override、synthetic SendPrompt 保持 `Model=None`、物理 `(SessionId, PhysicalUserMessageId)` execution admission 才取得 base/peer lease；managed model authority/supersession/释放语义由 `execution-model-routing` EMR-006/007/009 接管） | REUSE + GAP-016 | `node --test requirements/participant-identity/tests/session-execution-binding.test.mjs`；model 部分见 execution-model-routing PROOF |
| PID-009 | `tests/catalog.test.mjs` `WHAT[PID-009] bookkeeper_pair_has_machine_identity_and_peer_but_no_public_role`（机器身份 + 无 public Role）+ `tests/session-persona.test.mjs` `WHAT[PID-009] bookkeeperPersona_is_clerk_or_curator_machine_persona` | MOVE | `node --test requirements/participant-identity/tests/{catalog,session-persona}.test.mjs` |
| PID-010 | `tests/session-persona.test.mjs` `WHAT[PID-010] child_session_persona_inherits_owner_persona`（`inheritFromOwner` 后 replica = 'Engineer'） | MOVE | 同上 |

### 移动文件

| 源 | 目标 | 结果 |
|----|------|------|
| `requirements/participant-identity/tests/catalog.test.mjs` | `requirements/participant-identity/tests/catalog.test.mjs` | 6 pass / 0 fail |
| `requirements/participant-identity/tests/session-persona.test.mjs` | `requirements/participant-identity/tests/session-persona.test.mjs` | 6 pass / 0 fail |
| `requirements/participant-identity/tests/persona-binding.test.mjs` | `requirements/participant-identity/tests/persona-binding.test.mjs` | 2 pass / 0 fail |

### 计数

WHAT 命题 10；active test 16（catalog 6 + session-persona 6 + persona-binding 2 + session-execution-binding 3，
拆分后每个 test 恰一个 primary WHAT）；PID-008 的 model-routing 交叉证明等待 `execution-model-routing` GAP-016。

### semantic anchor id 清单（`scripts/checks/semantic-anchors.mjs`）

本包 **不拥有** 任何 ROLE_SEMANTIC_ANCHORS / OFFICE_CAPABILITY_ANCHORS id：
Role Law 语义锚点是 cognition/office 内容（`cognitive-environment` / `office-capability` /
`action-affordance` / `epistemic-reasoning` 等逐 id 声明）。本包的身份命题由 dist 层测试直接证明，
不经语义锚点。`delegation` 包声明的 `persona-not-authority`（fork 组）其 personhood 部分与本包
PID-002/006 同义——按契约「一个 assertion 只有一个 owner」，该 id 由 `delegation` 拥有，本包不重复声明。

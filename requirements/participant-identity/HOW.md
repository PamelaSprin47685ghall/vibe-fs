# participant-identity — HOW

## 架构与核心机制

`participant-identity` 在 Domain/Kernel 层独占身份解析与不可变性：

```text
合法 root input / typed owner-derived evidence
                       │
                       ▼
resolve(Role, initial Tier, persona provenance/version, canonical catalog)
                       │
                       ▼
prepared ParticipantIdentityEvidence {
  SelectedAgent; PeerAgent; Role; InitialTier; Persona; PersonaEvidence
}
                       │
                       ▼
AuthorityRootAccepted { exact root keys; ParticipantIdentityEvidence }  ← single durable append
                       │
                       └──► system-prompt / authority / capability consumers

exact execution request ───► ExecutionBinding { EffectiveAgent; provider/model; lease }
```

1. **Root identity acceptance**：identity owner 只接受合法 root input 或 exact owner-derived evidence，并纯计算/校验完整 `ParticipantIdentityEvidence`。Authority 把它作为 `AuthorityRootAccepted` 的必填 payload 单次原子追加；该追加同时是 identity installation 与 root acceptance 的唯一 durable fact。禁止独立 identity-installation write。child、attached 与 InternalLeaf 若缺少 evidence，或 evidence 的 owner/run 不精确匹配，必须 fail-closed；append 未提交时不得发布任一状态。

2. **Run-scoped fold**：identity 与 authority 投影都从同一 `AuthorityRootAccepted` 重放，以 exact `(SessionId, LogicalRunId, AuthorityRootId)` 为 key；Session cache 与 Host physical parent 不参与。重复 acceptance payload 幂等，任何 run 内不同 payload 都拒绝。

3. **Container reuse**：fresh root acceptance 必须先观察 exact `AuthorityLogicalRunClosed`，其 key 精确匹配旧 `(SessionId, LogicalRunId, AuthorityRootId)`，且 authority fold 已由该事实释放旧 active identity binding。缺少该 closure、仅有 lifecycle terminal/association removal/idle/timeout 或仍有 active run 时不得替换。同一 SessionId 的后继 run 从合法 root input 重新 resolve，不读取旧 run identity。

4. **Execution separation**：canonical SelectedAgent/PeerAgent 是 immutable identity evidence；fallback、Strength、Peer 路由与 provider lease 只生成含当前 EffectiveAgent/provider/model/lease 的新 `ExecutionBinding`。system prompt、authority profile 与 capability projection 消费同一 durable identity evidence，不反向解析或改写身份。内部 Role 使用私有 catalog 分支，不进入 public `Role`。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PID-001 | `requirements/participant-identity/tests/catalog.test.mjs::WHAT[PID-001] catalog_has_exactly_ten_canonical_roles_and_two_tiers`；`requirements/participant-identity/tests/catalog.test.mjs::WHAT[PID-001] required_names_are_exactly_ten_roles_times_two_tiers` |
| PID-002 | `requirements/participant-identity/tests/catalog.test.mjs::WHAT[PID-002] all_legacy_bare_names_are_rejected`；`requirements/participant-identity/tests/catalog.test.mjs::WHAT[PID-002] rejection_prose_is_version_agnostic` |
| PID-003 | `requirements/participant-identity/tests/session-persona.test.mjs::WHAT[PID-003] SessionPersona_binds_once_same_value_idempotent_different_value_rejected` |
| PID-004 | `requirements/participant-identity/tests/persona-binding.test.mjs::WHAT[PID-004] persona_frozen_across_gate_d_events` |
| PID-005 | `requirements/participant-identity/tests/session-persona.test.mjs::WHAT[PID-005] system_prompt_id_follows_canonical_role_not_effective_agent_tier` |
| PID-006 | `requirements/participant-identity/tests/session-persona.test.mjs::WHAT[PID-006] binding_wire_names_are_machine_routing_identity_not_persona_self_claim` |
| PID-007 | `requirements/participant-identity/tests/catalog.test.mjs::WHAT[PID-007] peer_is_same_role_opposite_tier_and_symmetric` |
| PID-008 | `requirements/participant-identity/tests/session-execution-binding.test.mjs::WHAT[PID-008] root_requires_external_agent_proof_then_model_is_scheduler_owned`；`requirements/participant-identity/tests/session-execution-binding.test.mjs::WHAT[PID-008] parented_session_uses_stable_agent_lease_and_authorized_peer_only`；`requirements/participant-identity/tests/session-execution-binding.test.mjs::WHAT[PID-008] provider_reasoning_variant_must_match_the_exact_lease` |
| PID-009 | `requirements/participant-identity/tests/catalog.test.mjs::WHAT[PID-009] bookkeeper_pair_has_machine_identity_and_peer_but_no_public_role` |
| PID-010 | `requirements/participant-identity/tests/session-persona.test.mjs::WHAT[PID-010] child_session_persona_inherits_owner_persona`；`requirements/participant-identity/tests/session-persona.test.mjs::WHAT[PID-010] child_session_persona_inherits_even_when_owner_was_not_yet_queried` |
| PID-011 | `requirements/participant-identity/tests/session-reuse-identity.test.mjs::WHAT[PID-011] reuses SessionId with a fresh closed-run identity`；`requirements/participant-identity/tests/session-reuse-composition.test.mjs::WHAT[PID-011] production plugin replaces identity only after exact durable Manager closure` |

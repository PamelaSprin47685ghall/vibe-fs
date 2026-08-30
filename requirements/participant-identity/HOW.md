# participant-identity — HOW

## 架构与核心机制

`participant-identity` 建立在 Domain/Kernel 层的核心类型之上，为主机与协议调度提供不可变的身份凭证：

```text
Role (Domain 枚举) ───┬──► SystemPromptId (纯函数，仅由 Role 决定)
                      ├──► SessionPersona (单次解析冻结，child 继承)
                      └──► Managed Catalog (生成 fast/deep 对称 Peer 关系)
```

1. **三轴解析流程**：
   - 根 session 创建时，由 `Role × initial tier` 确定 `SessionPersona`，执行单次绑定（bind-once）并固化。
   - 子 session 及内部执行通道通过 `inheritFromOwner` 继承父级 Persona，屏蔽底层物理档位对自我模型的影响。
   - 物理模型调度仅影响 `EffectiveAgent` 与其关联的租约，不触碰身份层。

2. **身份与租约的生命周期绑定**：
   - Managed session 在生命周期内维持 base EffectiveAgent 冻结；显式降级或提升通过单次 `ExplicitExecutionOverride` 注入执行层，执行完毕后恢复基准。
   - 内部身份（如 Bookkeeper）通过专用机器通道生成，不进入公开的 `Role` 枚举与选择视图。

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

# participant-identity — 测试落点表

每条 WHAT 命题恰好一行落点。类型：`MOVE` = 本包 tests/ 下文件（物理移入）；`REUSE` = 留原处，
记精确断言锚点与 cutover 拆分计划。

## 落点表

| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
|------|--------------------------------------|------|----------|
| PID-001 | `tests/catalog.test.mjs` `AGENT_001_catalog_has_exactly_ten_canonical_roles_and_two_tiers` | MOVE | `node --test requirements/participant-identity/tests/catalog.test.mjs` |
| PID-002 | `tests/catalog.test.mjs`（Role/Tier）+ `tests/session-persona.test.mjs`（Persona）+ `tests/unit/invariants/prompt-stability.test.mjs` `PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes` | MOVE+REUSE | `node --test requirements/participant-identity/tests/{catalog,session-persona}.test.mjs` |
| PID-003 | `tests/session-persona.test.mjs` `AGENT_028_SessionPersona_bind_once_and_inherit`（`bindOnce` 异值拒绝 `/already bound/`） | MOVE | 同上 |
| PID-004 | `tests/unit/invariants/prompt-stability.test.mjs` `PROMPT_STABILITY_fallback_peer_switch_keeps_persona_and_system_prompt_bytes` | REUSE（SPLIT：prompt-stability 三方 owner = participant-identity + prefix-stability + provider-language；Persona 断言归本包） | `node --test tests/unit/invariants/prompt-stability.test.mjs` |
| PID-005 | `tests/session-persona.test.mjs` `FALLBACK_014_system_prompt_id_follows_canonical_role_not_effective_agent_tier`（identity 值 `doesNotMatch /fast\|deep/i`） | MOVE | `node --test requirements/participant-identity/tests/session-persona.test.mjs` |
| PID-006 | `tests/session-persona.test.mjs` `FALLBACK_014_...`（prompt identity 不含 binding 名）；horizon 侧拦截由 `participant-horizon` Gate B 承担（交叉引用） | MOVE | 同上 |
| PID-007 | `tests/catalog.test.mjs` `AGENT_003_peer_is_same_role_opposite_tier_and_symmetric` + `requirements/capability-enforcement/tests/managed-agent-config.test.mjs` `MACFG_validate_rejects_duplicate_pair_model` | MOVE + REUSE（SPLIT@cutover：pair-model 互异断言物理在 enforcement 包文件内，语义 owner 是本包） | 两者分别 `node --test` |
| PID-008 | `tests/unit/host/session-execution-binding.test.mjs` `PROMPT_006_parented_session_rejects_agent_and_model_drift_before_host_send`、`PROMPT_006_only_external_user_choice_rebinds_root_session` | REUSE（SPLIT：文件同时含 provider-language bind-once 断言） | `node --test tests/unit/host/session-execution-binding.test.mjs` |
| PID-009 | `tests/catalog.test.mjs`（`AGENT_001` public/internal 划分、`AGENT_002` bookkeeper 名/peer）+ `tests/session-persona.test.mjs`（`bookkeeperPersona` = Clerk/Curator） | MOVE | `node --test requirements/participant-identity/tests/{catalog,session-persona}.test.mjs` |
| PID-010 | `tests/session-persona.test.mjs` `AGENT_028_SessionPersona_bind_once_and_inherit`（`inheritFromOwner` 后 replica = 'Engineer'） | MOVE | 同上 |

## 移动文件

| 源 | 目标 | 结果 |
|----|------|------|
| `tests/unit/agent/catalog.test.mjs` | `requirements/participant-identity/tests/catalog.test.mjs` | 5 pass / 0 fail |
| `tests/unit/prompt/session-persona.test.mjs` | `requirements/participant-identity/tests/session-persona.test.mjs` | 3 pass / 0 fail |

## 计数

WHAT 命题 10；落点 10（MOVE 8 行 × REUSE 4 行，含组合行）；GAP 0。

## semantic anchor id 清单（`scripts/checks/semantic-anchors.mjs`）

本包 **不拥有** 任何 ROLE_SEMANTIC_ANCHORS / OFFICE_CAPABILITY_ANCHORS id：
Role Law 语义锚点是 cognition/office 内容（`cognitive-environment` / `office-capability` /
`action-affordance` / `epistemic-reasoning` 等逐 id 声明）。本包的身份命题由 dist 层测试直接证明，
不经语义锚点。`delegation` 包声明的 `persona-not-authority`（fork 组）其 personhood 部分与本包
PID-002/006 同义——按契约「一个 assertion 只有一个 owner」，该 id 由 `delegation` 拥有，本包不重复声明。

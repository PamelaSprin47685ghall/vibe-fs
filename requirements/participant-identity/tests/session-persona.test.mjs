// requirements/participant-identity/tests/session-persona.test.mjs — AGENT-028 /
// FALLBACK-014 Phase 16, moved from tests/unit/prompt/.
//
// SessionPersona bind-once + child inherit. Full prompt-stability bytes → Phase 19.

import assert from 'node:assert/strict'
import test from 'node:test'

import { AgentTier, Role } from '../../../dist/Foundation/Roles.js'
import * as PersonaCatalog from '../../../dist/Participant/Persona/Catalog.js'
import * as SessionPersona from '../../../dist/Participant/Persona/SessionPersona.js'
import { systemPromptIdFor } from '../../../dist/Interaction/Authority/Model.js'
import { SystemPromptIdModule_value as promptIdValue } from '../../../dist/Foundation/Identity.js'
import { resultOf, unwrapOption } from '../../verification-system/tests/support/domain/interop.mjs'
import { sessionId } from '../../verification-system/tests/support/domain.mjs'

const persona = PersonaCatalog.persona
const bindOnce = (id, value) => resultOf(SessionPersona.bindOnce(id, value))
const inheritFromOwner = (ownerPersona, childId) =>
  resultOf(SessionPersona.inheritFromOwner(ownerPersona, childId))
const tryGet = (id) => unwrapOption(SessionPersona.tryGet(id))
const clearAll = SessionPersona.clearAllForTests

test('WHAT[PID-002] persona_matrix_is_an_independent_axis_from_role_and_binding', () => {
  // Persona 是 role × initial tier 的独立命名空间（PID-002 三轴分离的 Persona 轴）：
  // 同一 role 的不同 tier 得到不同 persona 值，persona 值不冒充 role/binding 名。
  assert.equal(persona(Role.Coder, AgentTier.Fast), 'Coder')
  assert.equal(persona(Role.Coder, AgentTier.Deep), 'Engineer')
  assert.notEqual(persona(Role.Coder, AgentTier.Fast), persona(Role.Coder, AgentTier.Deep))
})

test('WHAT[PID-003] SessionPersona_binds_once_same_value_idempotent_different_value_rejected', () => {
  clearAll()
  const owner = sessionId('ses_owner_persona')

  const bound = bindOnce(owner, 'Engineer')
  assert.equal(bound.ok, true)
  assert.equal(bound.value, 'Engineer')
  assert.equal(tryGet(owner), 'Engineer')

  const again = bindOnce(owner, 'Engineer')
  assert.equal(again.ok, true)

  const conflict = bindOnce(owner, 'Coder')
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)
})

test('WHAT[PID-010] child_session_persona_inherits_owner_persona', () => {
  clearAll()
  const owner = sessionId('ses_owner_persona')
  const replica = sessionId('ses_replica_persona')

  bindOnce(owner, 'Engineer')
  const inherited = inheritFromOwner('Engineer', replica)
  assert.equal(inherited.ok, true)
  assert.equal(inherited.value, 'Engineer')
  assert.equal(tryGet(replica), 'Engineer')
})

test('WHAT[PID-005] system_prompt_id_follows_canonical_role_not_effective_agent_tier', () => {
  const rolePrompt = promptIdValue(systemPromptIdFor(Role.Coder))
  assert.equal(rolePrompt, promptIdValue(systemPromptIdFor(Role.Coder)))
  assert.doesNotMatch(rolePrompt, /fast|deep/i)
})

test('WHAT[PID-006] binding_wire_names_are_machine_routing_identity_not_persona_self_claim', () => {
  // Given: Role × tier 的 persona 解析（PID-002/003 矩阵）
  const fastPersona = persona(Role.Coder, AgentTier.Fast)
  const deepPersona = persona(Role.Coder, AgentTier.Deep)
  // When: 有人把 binding wire 名（fast-coder / deep-coder）当作 provider 自称
  // Then: 拒绝——persona 值既不是 wire 名，也不携带 fast-/deep- 机器标记
  assert.notEqual(fastPersona, 'fast-coder')
  assert.notEqual(deepPersona, 'deep-coder')
  for (const value of [fastPersona, deepPersona]) {
    assert.doesNotMatch(value, /^fast-|^deep-/)
  }
  // prompt identity 不含 binding 名（PID-005 的直接投影）
  assert.doesNotMatch(promptIdValue(systemPromptIdFor(Role.Coder)), /fast-coder|deep-coder/)
})

test('WHAT[PID-009] bookkeeperPersona_is_clerk_or_curator_machine_persona', () => {
  assert.equal(PersonaCatalog.bookkeeperPersona(AgentTier.Fast), 'Clerk')
  assert.equal(PersonaCatalog.bookkeeperPersona(AgentTier.Deep), 'Curator')
})

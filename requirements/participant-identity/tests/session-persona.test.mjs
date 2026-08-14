// requirements/participant-identity/tests/session-persona.test.mjs — AGENT-028 /
// FALLBACK-014 Phase 16, moved from tests/unit/prompt/.
//
// SessionPersona bind-once + child inherit. Full prompt-stability bytes → Phase 19.

import assert from 'node:assert/strict'
import test from 'node:test'

import { AgentTier, Role } from '../../../dist/Kernel/Roles.js'
import * as PersonaCatalog from '../../../dist/Domain/PersonaCatalog.js'
import * as SessionPersona from '../../../dist/Session/SessionPersona.js'
import { systemPromptIdFor } from '../../../dist/Domain/PromptAuthority.js'
import { SystemPromptIdModule_value as promptIdValue } from '../../../dist/Kernel/Identity.js'
import { resultOf, unwrapOption } from '../../verification-system/tests/support/domain/interop.mjs'
import { sessionId } from '../../verification-system/tests/support/domain.mjs'

const persona = PersonaCatalog.persona
const bindOnce = (id, value) => resultOf(SessionPersona.bindOnce(id, value))
const inheritFromOwner = (ownerPersona, childId) =>
  resultOf(SessionPersona.inheritFromOwner(ownerPersona, childId))
const tryGet = (id) => unwrapOption(SessionPersona.tryGet(id))
const clearAll = SessionPersona.clearAllForTests

test('AGENT_028_persona_matrix_resolves_role_times_initial_tier', () => {
  assert.equal(persona(Role.Coder, AgentTier.Fast), 'Coder')
  assert.equal(persona(Role.Coder, AgentTier.Deep), 'Engineer')
  assert.equal(PersonaCatalog.bookkeeperPersona(AgentTier.Fast), 'Clerk')
})

test('AGENT_028_SessionPersona_bind_once_and_inherit', () => {
  clearAll()
  const owner = sessionId('ses_owner_persona')
  const replica = sessionId('ses_replica_persona')

  const bound = bindOnce(owner, 'Engineer')
  assert.equal(bound.ok, true)
  assert.equal(bound.value, 'Engineer')
  assert.equal(tryGet(owner), 'Engineer')

  const again = bindOnce(owner, 'Engineer')
  assert.equal(again.ok, true)

  const conflict = bindOnce(owner, 'Coder')
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)

  const inherited = inheritFromOwner('Engineer', replica)
  assert.equal(inherited.ok, true)
  assert.equal(inherited.value, 'Engineer')
  assert.equal(tryGet(replica), 'Engineer')
})

test('FALLBACK_014_system_prompt_id_follows_canonical_role_not_effective_agent_tier', () => {
  const rolePrompt = promptIdValue(systemPromptIdFor(Role.Coder))
  assert.equal(rolePrompt, promptIdValue(systemPromptIdFor(Role.Coder)))
  assert.doesNotMatch(rolePrompt, /fast|deep/i)
})

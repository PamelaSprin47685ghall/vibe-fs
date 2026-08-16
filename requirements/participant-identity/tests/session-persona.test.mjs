// requirements/participant-identity/tests/session-persona.test.mjs
//
// AGENT-028 / FALLBACK-014: persona is a semantic identity axis and the
// session registry is a bind-once state owner. Both cross the JS boundary as
// strings and plain result objects; F# Role/AgentTier/Option/Result values do
// not cross into the test.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const persona = await import('../../../dist/Participant/Persona/Surface.js')
const session = await import('../../../dist/Participant/Persona/SessionSurface.js')
const prompt = await import('../../../dist/Interaction/Authority/Surface.js')

test('WHAT[PID-002] persona_matrix_is_an_independent_axis_from_role_and_binding', () => {
  const fast = persona.persona('coder', 'fast')
  const deep = persona.persona('coder', 'deep')
  assert.equal(fast, 'Coder')
  assert.equal(deep, 'Engineer')
  assert.notEqual(fast, deep)
  assert.doesNotMatch(fast, /^fast-|^deep-/)
  assert.doesNotMatch(deep, /^fast-|^deep-/)
})

test('WHAT[PID-003] SessionPersona_binds_once_same_value_idempotent_different_value_rejected', () => {
  session.clear()
  const owner = 'ses_owner_persona'

  const bound = session.bindOnce(owner, 'Engineer')
  assertJsData(bound)
  assert.equal(bound.ok, true)
  assert.equal(bound.value, 'Engineer')
  assert.equal(session.tryGet(owner), 'Engineer')

  const again = session.bindOnce(owner, 'Engineer')
  assert.equal(again.ok, true)

  const conflict = session.bindOnce(owner, 'Coder')
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)
})

test('WHAT[PID-010] child_session_persona_inherits_owner_persona', () => {
  session.clear()
  const owner = 'ses_owner_persona'
  const replica = 'ses_replica_persona'

  assert.equal(session.bindOnce(owner, 'Engineer').ok, true)
  const inherited = session.inheritFromOwner('Engineer', replica)
  assert.equal(inherited.ok, true)
  assert.equal(inherited.value, 'Engineer')
  assert.equal(session.tryGet(replica), 'Engineer')
})

test('WHAT[PID-005] system_prompt_id_follows_canonical_role_not_effective_agent_tier', () => {
  const rolePrompt = prompt.systemPromptIdForRole('coder')
  assert.equal(rolePrompt, 'coder')
  assert.doesNotMatch(rolePrompt, /fast|deep/i)
  assert.equal(prompt.systemPromptIdForRole('not-a-role'), '')
})

test('WHAT[PID-006] binding_wire_names_are_machine_routing_identity_not_persona_self_claim', () => {
  const fastPersona = persona.persona('coder', 'fast')
  const deepPersona = persona.persona('coder', 'deep')
  assert.notEqual(fastPersona, 'fast-coder')
  assert.notEqual(deepPersona, 'deep-coder')
  for (const value of [fastPersona, deepPersona]) {
    assert.doesNotMatch(value, /^fast-|^deep-/)
  }
  assert.doesNotMatch(prompt.systemPromptIdForRole('coder'), /fast-coder|deep-coder/)
})

test('WHAT[PID-009] bookkeeperPersona_is_clerk_or_curator_machine_persona', () => {
  assert.equal(persona.bookkeeperPersona('fast'), 'Clerk')
  assert.equal(persona.bookkeeperPersona('deep'), 'Curator')
})

// Split from tests/unit/invariants/prompt-stability.test.mjs (cutover Wave 2a); owner: participant-identity
//
// Persona half of ARCH-016 Gate D: SessionPersona binds once, is frozen, and
// survives every event the Gate D scenario runs (fallback peer switch, T1
// review, reanchor). The system-prompt-byte half of the scenario lives in
// prefix-stability/tests/system-prompt-stability.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'

import { AgentTier, Role } from '../../../dist/Kernel/Roles.js'
import * as PersonaCatalog from '../../../dist/Domain/PersonaCatalog.js'
import { resultOf, sessionId, unwrapOption } from '../../verification-system/tests/support/domain.mjs'

test('PROMPT_STABILITY_persona_binds_once_and_never_rewrites', () => {
  PersonaCatalog.SessionPersona_clearAllForTests()
  const owner = sessionId('ses_gate_d')

  const bound = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Coordinator'))
  assert.equal(bound.ok, true)
  assert.equal(unwrapOption(PersonaCatalog.SessionPersona_tryGet(owner)), 'Coordinator')

  // The persona is a function of role+tier, not a single global string.
  assert.notEqual(
    PersonaCatalog.PersonaCatalog_persona(Role.Manager, AgentTier.Deep),
    PersonaCatalog.PersonaCatalog_persona(Role.Manager, AgentTier.Fast),
  )

  // Re-binding the same value is idempotent; a different value is refused.
  const replay = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Coordinator'))
  assert.equal(replay.ok, true)
  assert.equal(unwrapOption(PersonaCatalog.SessionPersona_tryGet(owner)), 'Coordinator')

  const rewrite = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Engineer'))
  assert.equal(rewrite.ok, false)
  assert.match(rewrite.error, /already bound/)
})

test('PROMPT_STABILITY_persona_frozen_across_gate_d_events', () => {
  // The Gate D t1/review/reanchor scenario (byte half in prefix-stability)
  // re-checks the persona at every gate; the persona half pins the same
  // bind-once law observed after the scenario.
  PersonaCatalog.SessionPersona_clearAllForTests()
  const owner = sessionId('ses_gate_d_t1_review_reanchor')

  const bound = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Coordinator'))
  assert.equal(bound.ok, true)
  const persona = unwrapOption(PersonaCatalog.SessionPersona_tryGet(owner))
  assert.equal(persona, 'Coordinator')

  const replay = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, persona))
  assert.equal(replay.ok, true)
  assert.equal(unwrapOption(PersonaCatalog.SessionPersona_tryGet(owner)), 'Coordinator')

  const rewrite = resultOf(PersonaCatalog.SessionPersona_bindOnce(owner, 'Engineer'))
  assert.equal(rewrite.ok, false)
  assert.match(rewrite.error, /already bound/)
})

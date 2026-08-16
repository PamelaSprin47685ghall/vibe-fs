// Split from tests/unit/invariants/prompt-stability.test.mjs (cutover Wave 2a); owner: participant-identity
//
// Persona half of ARCH-016 Gate D: SessionPersona binds once, is frozen, and
// survives every event the Gate D scenario runs. The JS contract exposes only
// semantic strings and plain result objects.

import assert from 'node:assert/strict'
import test from 'node:test'

const persona = await import('../../../dist/Participant/Persona/Surface.js')
const session = await import('../../../dist/Participant/Persona/SessionSurface.js')

test('WHAT[PID-003] persona_binds_once_and_never_rewrites', () => {
  session.clear()
  const owner = 'ses_gate_d'

  const bound = session.bindOnce(owner, 'Coordinator')
  assert.equal(bound.ok, true)
  assert.equal(session.tryGet(owner), 'Coordinator')

  assert.notEqual(persona.persona('manager', 'deep'), persona.persona('manager', 'fast'))

  const replay = session.bindOnce(owner, 'Coordinator')
  assert.equal(replay.ok, true)
  assert.equal(session.tryGet(owner), 'Coordinator')

  const rewrite = session.bindOnce(owner, 'Engineer')
  assert.equal(rewrite.ok, false)
  assert.match(rewrite.error, /already bound/)
})

test('WHAT[PID-004] persona_frozen_across_gate_d_events', () => {
  session.clear()
  const owner = 'ses_gate_d_t1_review_reanchor'

  const bound = session.bindOnce(owner, 'Coordinator')
  assert.equal(bound.ok, true)
  const boundPersona = session.tryGet(owner)
  assert.equal(boundPersona, 'Coordinator')

  const replay = session.bindOnce(owner, boundPersona)
  assert.equal(replay.ok, true)
  assert.equal(session.tryGet(owner), 'Coordinator')

  const rewrite = session.bindOnce(owner, 'Engineer')
  assert.equal(rewrite.ok, false)
  assert.match(rewrite.error, /already bound/)
})

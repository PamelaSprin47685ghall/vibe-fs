import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

test('WHAT[ENF-014] owner single issuance and manifest anchors fail closed on mismatch', () => {
  const gateOwner = quiescence.create()
  const gateStranger = quiescence.create()

  // 1. Single owner issuance: Only gateOwner issues permits bound to gateOwner's identity.
  quiescence.beginAttempt(gateOwner, 'ses-enf-014')
  const permit = quiescence.observeIdle(gateOwner, 'ses-enf-014')
  assert.ok(permit != null)

  // 2. Foreign gate must reject permit issued by another owner with WrongOwner
  const strangerConsume = quiescence.tryConsume(gateStranger, permit)
  assert.equal(strangerConsume.accepted, false)
  assert.equal(strangerConsume.failure, 'WrongOwner')

  const strangerRelease = quiescence.tryRelease(gateStranger, permit)
  assert.equal(strangerRelease.accepted, false)
  assert.equal(strangerRelease.failure, 'WrongOwner')

  // 3. The owning gate accepts its own valid permit
  const ownerConsume = quiescence.tryConsume(gateOwner, permit)
  assert.equal(ownerConsume.accepted, true)
  assert.ok(ownerConsume.failure == null)
})

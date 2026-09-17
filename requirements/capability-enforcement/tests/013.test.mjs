import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

test('WHAT[ENF-013] authority values strictly classify causal categories and vocabulary rejects name-only guessing', () => {
  // 1. Vocabulary isolation: OfficeCapability permissions returns discrete domain vocabulary labels.
  // Vocabulary items are plain string labels and do not carry execution authority by themselves.
  const engineerPermissions = office.permissions('engineer')
  assert.ok(Array.isArray(engineerPermissions))
  assert.ok(engineerPermissions.includes('Read'))
  assert.ok(engineerPermissions.includes('Write'))
  assert.ok(engineerPermissions.includes('Fission'))

  // 2. Fake capability rejection: A forged object carrying capability-like fields cannot act as an opaque permit.
  const gate = quiescence.create()
  const fakePermit = {
    tag: 0,
    fields: ['ses-fake', 1],
    name: 'QuiescencePermit',
  }

  const fakeConsume = quiescence.tryConsume(gate, fakePermit)
  assert.equal(fakeConsume.accepted, false)
  assert.equal(fakeConsume.failure, 'WrongOwner')

  const fakeRelease = quiescence.tryRelease(gate, fakePermit)
  assert.equal(fakeRelease.accepted, false)
  assert.equal(fakeRelease.failure, 'WrongOwner')

  // 3. Genuine opaque permit: Only authentic capabilities issued by the proper owner are recognized.
  quiescence.beginAttempt(gate, 'ses-enf-013')
  const validPermit = quiescence.observeIdle(gate, 'ses-enf-013')
  assert.ok(validPermit != null)

  const validConsume = quiescence.tryConsume(gate, validPermit)
  assert.equal(validConsume.accepted, true)
  assert.equal(validConsume.failure, undefined)
})

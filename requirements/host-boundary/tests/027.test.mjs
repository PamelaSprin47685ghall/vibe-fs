import assert from 'node:assert/strict'
import test from 'node:test'
import * as m6 from '../../../dist/OpenCode/Host/M6SliceBoundarySurface.js'

test('WHAT[HOST-BOUNDARY-027] production inventory closes Host codec audiences without the wide signal adapter', () => {
  assert.equal(m6.isHostCodecClosed(), true)
})

test('WHAT[HOST-BOUNDARY-027] Host envelope projection is shared and never mutates the raw payload', () => {
  const payload = { a: 1 }
  const projected = m6.projectEnvelope(payload)
  assert.deepEqual(payload, { a: 1 })
  assert.ok(projected)
})

test('WHAT[HOST-BOUNDARY-027] Host message loop and envelope slices reject the old wide signal closure', () => {
  assert.equal(m6.rejectsWideSignalClosure(), true)
})

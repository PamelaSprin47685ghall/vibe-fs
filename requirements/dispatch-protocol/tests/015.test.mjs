import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'
import * as ingressProperty from '../../../dist/Interaction/Dispatch/OpenCode/IngressPropertySurface.js'

test('WHAT[DISPATCH-PROTOCOL-015] ingress identity carrier algebra is exact conflict closed and byte preserving', () => {
  const res = dispatch.decodeSessionIdCarriers({
    sessionID: 'ses-exact-1',
    input: { session: 'ses-exact-1' },
  })
  assert.equal(res.ok, true)
  assert.equal(res.sessionId, 'ses-exact-1')
})

test('WHAT[DISPATCH-PROTOCOL-015] ingress identity carrier algebra rejects conflicting carriers', () => {
  const res = dispatch.decodeSessionIdCarriers({
    sessionID: 'ses-1',
    sessionId: 'ses-2',
  })
  assert.equal(res.ok, false)
})

test('WHAT[DISPATCH-PROTOCOL-015] ingress identity property rejects every malformed or ambiguous carrier world', () => {
  assert.equal(ingressProperty.verifyAllMalformedRejected(), true)
})

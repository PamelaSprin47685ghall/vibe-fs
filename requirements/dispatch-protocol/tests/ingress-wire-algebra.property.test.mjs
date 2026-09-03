import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import { decodeIngress } from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const malformedString = fc.anything({ withBoxedValues: true }).filter(value => typeof value !== 'string')
const nonblankString = fc.string().filter(value => value.trim().length > 0)

test('WHAT[DISPATCH-PROTOCOL-015] ingress identity property rejects every malformed or ambiguous carrier world', () => {
  fc.assert(fc.property(malformedString, nonblankString, (malformed, valid) => {
    assert.doesNotThrow(() => decodeIngress({ sessionID: malformed }, {}))
    assert.equal(decodeIngress({ sessionID: malformed }, {}).sessionId, null)
    assert.equal(decodeIngress({ sessionID: valid, sessionId: malformed }, {}).sessionId, null)
    assert.equal(decodeIngress({ agent: valid }, { message: { agent: malformed } }).explicitAgent, null)
    assert.equal(
      decodeIngress({ metadata: { wanxiangshu_prompt_key: valid } }, { parts: [{ metadata: { wanxiangshu_prompt_key: malformed } }] }).promptKey,
      null,
    )
  }), { seed: 15015, numRuns: 160 })

  fc.assert(fc.property(nonblankString, nonblankString, (left, right) => {
    fc.pre(left !== right)
    assert.equal(decodeIngress({ sessionID: left }, { info: { sessionID: right } }).sessionId, null)
  }), { seed: 25015, numRuns: 120 })
})

test('WHAT[DISPATCH-PROTOCOL-015] generated non-arrays and non-booleans remain inert without exceptions', () => {
  fc.assert(fc.property(fc.anything({ withBoxedValues: true }), value => {
    if (!Array.isArray(value)) {
      assert.doesNotThrow(() => decodeIngress({}, { parts: value }))
      assert.equal(decodeIngress({}, { parts: value }).isHostSynthetic, false)
    }
    if (typeof value !== 'boolean') {
      assert.equal(decodeIngress({}, { message: { summary: value } }).isHostCompaction, false)
      assert.equal(decodeIngress({}, { parts: [{ synthetic: value }] }).isHostSynthetic, false)
    }
  }), { seed: 35015, numRuns: 200 })
})

import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const observation = {
  case: 'BlogObservationCommitted', sessionId: 'main', bloggerSessionId: 'blogger', requestId: 'request',
  frameEpoch: 0, previousIngestedThroughSequence: 0, nextIngestedThroughSequence: 1,
  previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 1,
  nextCoveredPrefixDigest: 'prefix', textRef: 'blobs/text', textDigest: 'text-digest',
  run: 'provider-run', toolCallIds: ['tool-call'], tipName: 'sample-rule',
  tipTextRef: 'blobs/tip-text', tipTextDigest: 'tip-text-digest',
  evidenceRef: 'blobs/evidence', observedPrefixEpoch: 0,
}

test('WHAT[behavior-diagnosis-012] actual fact codec preserves complete observation records', () => {
  for (const fact of [observation]) {
    const line = blog.serializeFact(fact)
    const decoded = blog.deserializeFact(line)
    assert.equal(decoded.ok, true)
    assert.equal(decoded.line, line)
  }
})

test('WHAT[behavior-diagnosis-012] actual codec refuses retired split observation facts', () => {
  for (const [fact, retired] of [[observation, 'BlogEntryCommitted'], [observation, 'BlogSquashCommitted'], [observation, 'BlogObservationsSquashed']]) {
    const line = blog.serializeFact(fact).replaceAll(fact.case, retired)
    assert.equal(blog.deserializeFact(line).ok, false)
  }
  assert.throws(() => blog.serializeFact({ case: 'EnforcementCycleCommitted' }), /unknown fact/)
})

test.todo('WHAT[behavior-diagnosis-012] GAP-112 actual commit fault and disk reopen prove frame coverage tip and identities are published atomically')

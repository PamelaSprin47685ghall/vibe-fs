import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const observation = {
  case: 'BlogObservationCommitted', sessionId: 'main', bloggerSessionId: 'blogger', requestId: 'request',
  frameEpoch: 0, previousIngestedThroughSequence: 0, nextIngestedThroughSequence: 1,
  previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 1,
  nextCoveredPrefixDigest: 'prefix', textRef: 'blobs/text', textDigest: 'text-digest',
  run: 'provider-run', toolCallIds: ['tool-call'], tipRuleId: 'sample-rule', fieldNameAtCommit: 'sample-rule',
  evidenceRef: 'blobs/evidence', observedPrefixEpoch: 0,
}
const squash = {
  case: 'BlogObservationsSquashed', sessionId: 'main', bloggerSessionId: 'blogger', requestId: 'squash',
  previousFrameEpoch: 0, nextFrameEpoch: 1, coveredFrameCount: 1,
  textRef: 'blobs/squash', textDigest: 'squash-digest', run: 'squash-run',
}

test('WHAT[behavior-diagnosis-012] actual fact codec preserves complete observation and squash records', () => {
  for (const fact of [observation, squash]) {
    const line = blog.serializeFact(fact)
    const decoded = blog.deserializeFact(line)
    assert.equal(decoded.ok, true)
    assert.equal(decoded.line, line)
  }
})

test('WHAT[behavior-diagnosis-012] actual codec refuses retired split observation facts', () => {
  for (const [fact, retired] of [[observation, 'BlogEntryCommitted'], [squash, 'BlogSquashCommitted']]) {
    const line = blog.serializeFact(fact).replaceAll(fact.case, retired)
    assert.equal(blog.deserializeFact(line).ok, false)
  }
  assert.throws(() => blog.serializeFact({ case: 'EnforcementCycleCommitted' }), /unknown fact/)
})

test.todo('WHAT[behavior-diagnosis-012] GAP-112 actual commit fault and disk reopen prove frame coverage tip and identities are published atomically')

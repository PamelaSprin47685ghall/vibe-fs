// PROVIDER-PROJECTION algebra oracle over Strength intents.
//
// The registered ProjectionSurface owns F# intent construction, planner
// reduction, and renderer output. This test only exchanges JSON-shaped values
// and an opaque hash callback at that boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const H = (text) => `H(${text})`
const textMessage = (role, text) => ({ role, parts: [{ kind: 'text', text }] })
const emptySnapshot = () => Projection.projectionSnapshot(
  Projection.semanticProjection([]),
  { blogFrames: [], transportMessages: [], hostReanchor: null },
)

const bundle = {
  digest: 'bundle-digest',
  byteLength: 10,
  batches: [
    {
      requestOrdinal: 1,
      exchanges: [
        { toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' },
        { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' },
      ],
    },
  ],
}

test('WHAT[PROVIDER-PROJECTION-005] STRENGTH_009_016_projection_exposes_strength_intent_constructors', () => {
  assert.equal(typeof Projection.useStrengthMirror, 'function')
  assert.equal(typeof Projection.strengthCandidate, 'function')
  assert.equal(typeof Projection.strengthPromoted, 'function')
  assert.equal(typeof Projection.strengthReplicaLocal, 'function')
})

test('WHAT[PROVIDER-PROJECTION-006] STRENGTH_009_mirror_conflicts_with_normal_work_base_selection', () => {
  const mirror = Projection.useStrengthMirror({
    decisionId: 'd1',
    targetProviderRun: 'target',
    semanticDigest: 'sem-a',
    messages: [textMessage('user', 'mirror')],
  })
  const planned = Projection.plan([Projection.keepPhysicalPrefix, mirror])
  assert.equal(planned.ok, false)
  assert.equal(planned.conflict, 'ConflictingPrefixSelection')
})

test('WHAT[PROVIDER-PROJECTION-006] STRENGTH_008_009_multiple_promoted_absolute_anchors_are_registration_order_independent', () => {
  const base = [
    textMessage('user', 'u1'),
    textMessage('assistant', 'target-1'),
    textMessage('user', 'u2'),
    textMessage('assistant', 'target-2'),
  ]
  const first = Projection.strengthPromoted({
    ownerSessionId: 'owner',
    decisionId: 'd1',
    targetProviderRun: 'target-1',
    beforeIndex: 1,
    isReplicaRequest: false,
    bundle,
  })
  const second = Projection.strengthPromoted({
    ownerSessionId: 'owner',
    decisionId: 'd2',
    targetProviderRun: 'target-2',
    beforeIndex: 3,
    isReplicaRequest: false,
    bundle,
  })

  const forward = Projection.renderMessagesWithHostIds(H, emptySnapshot(), base, [first, second])
  const reverse = Projection.renderMessagesWithHostIds(H, emptySnapshot(), base, [second, first])

  assert.deepEqual(forward.messages.map((item) => item.role), [
    'user', 'assistant', 'tool', 'assistant', 'user', 'assistant', 'tool', 'assistant',
  ])
  assert.equal(
    forward.hostMessageIds[1],
    H(`owner\u001fd1\u001f1\u001fcall\u001fbundle-digest`),
  )
  assert.equal(
    forward.hostMessageIds[5],
    H(`owner\u001fd2\u001f1\u001fcall\u001fbundle-digest`),
  )
  assert.deepEqual(reverse.hostMessageIds, forward.hostMessageIds)
  assert.equal(Projection.renderWire(forward.messages), Projection.renderWire(reverse.messages))
})

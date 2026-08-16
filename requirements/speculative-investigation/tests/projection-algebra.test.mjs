import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = (text) => `H(${text})`
const bundle = Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }, { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }] }]).value
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages), { committedPrefix: null, blogFrames: [], transportMessages: [], hostReanchor: null })
const text = (textValue) => ({ kind: 'text', text: textValue })
const message = (role, parts) => ({ role, parts })

test('WHAT[SPEC-INV-009] STRENGTH_006_009_candidate_wrong_target_and_promoted_replica_reflection_conflict', () => {
  const wrongTarget = Projection.strengthCandidate({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', currentProviderRun: 'target-b', bundle })
  assert.equal(Projection.plan([wrongTarget]).ok, false)
  assert.equal(Projection.plan([wrongTarget]).conflict, 'StrengthCandidateWrongTarget')
  const reflected = Projection.strengthPromoted({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', beforeIndex: 0, isReplicaRequest: true, bundle })
  assert.equal(Projection.plan([reflected]).ok, false)
  assert.equal(Projection.plan([reflected]).conflict, 'StrengthPromotedReplicaReflection')
})

test('WHAT[SPEC-INV-005] STRENGTH_005_009_candidate_renders_concurrent_calls_then_results_with_stable_ids', () => {
  const base = [message('user', [text('base')])]
  const intent = Projection.strengthCandidate({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target', currentProviderRun: 'target', bundle })
  const first = Projection.renderMessagesWithHostIds(H, snapshot(base), base, [intent])
  const second = Projection.renderMessagesWithHostIds(H, snapshot(base), base, [intent])
  assert.equal(first.messages.length, 3)
  assert.equal(first.messages[0].role, 'user')
  assert.equal(first.messages[1].role, 'assistant')
  assert.equal(first.messages[2].role, 'tool')
  const calls = first.messages[1].parts
  const results = first.messages[2].parts
  assert.deepEqual(calls.map((part) => part.kind), ['tool-call', 'tool-call'])
  assert.deepEqual(results.map((part) => part.kind), ['tool-result', 'tool-result'])
  assert.deepEqual(calls.map((part) => part.callId), results.map((part) => part.callId))
  assert.equal(Projection.renderWire(first.messages), Projection.renderWire(second.messages))
})

test('WHAT[SPEC-INV-009] STRENGTH_009_012_policy_promoted_frames_leave_later_pair_anchor_messages_in_place', () => {
  const base = [message('user', [text('u1')]), message('assistant', [text('target-assistant')]), message('user', [text('pair-anchor-stand-in')])]
  const promoted = Projection.strengthPromoted({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-1', beforeIndex: 1, isReplicaRequest: false, bundle })
  const rendered = Projection.renderMessagesWithHostIds(H, snapshot(base), base, [promoted])
  assert.deepEqual(rendered.messages.map((item) => item.role), ['user', 'assistant', 'tool', 'assistant', 'user'])
  assert.equal(rendered.messages.at(-1).parts[0].text, 'pair-anchor-stand-in')
})

test('WHAT[SPEC-INV-009] STRENGTH_009_replica_mirror_replaces_base_then_local_batches_append', () => {
  const mirrorMessages = [message('user', [text('mirror-base')])]
  const mirror = Projection.useStrengthMirror({ decisionId: 'd1', targetProviderRun: 'target', semanticDigest: 'sem-a', messages: mirrorMessages })
  const local = Projection.strengthReplicaLocal({ ownerSessionId: 'owner', decisionId: 'd1', bundle })
  const base = [message('user', [text('child-physical')])]
  const rendered = Projection.renderMessagesWithHostIds(H, snapshot(base), base, [mirror, local])
  assert.equal(rendered.messages.length, 3)
  assert.equal(rendered.messages[0].parts[0].text, 'mirror-base')
  assert.equal(rendered.messages[1].role, 'assistant')
  assert.equal(rendered.messages[2].role, 'tool')
})

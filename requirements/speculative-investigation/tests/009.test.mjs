import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
const exchange = (toolName, canonicalArguments, canonicalResult) => ({ toolName, canonicalArguments, canonicalResult })
const batch = (requestOrdinal, exchanges) => ({ requestOrdinal, exchanges })

test('WHAT[SPEC-INV-009] STRENGTH_009_replica_mirror_localizes_owner_call_ids_without_changing_semantics', () => {
  const ownerMessages = [
    { role: 'assistant', parts: [call('owner-a', 'read', '{"filePath":"a"}'), call('owner-b', 'grep', '{"pattern":"x"}')] },
    { role: 'tool', parts: [result('owner-b', 'hit'), result('owner-a', 'alpha')] },
  ]
  const digest = H(Strength.renderSemantic(ownerMessages))
  const first = Strength.frameTryLocalizeMirror(H, 'd1', digest, ownerMessages)
  const second = Strength.frameTryLocalizeMirror(H, 'd1', digest, ownerMessages)
  assert.equal(first.ok, true)
  assert.equal(second.ok, true)
  assert.equal(Strength.renderSemantic(first.value), Strength.renderSemantic(ownerMessages))
  assert.equal(Strength.renderWire(first.value), Strength.renderWire(second.value))
  assert.doesNotMatch(Strength.renderWire(first.value), /owner-a|owner-b/)
  const localizedCalls = first.value[0].parts.filter((part) => part.kind === 'tool-call').map((part) => part.callId)
  const localizedResults = first.value[1].parts.filter((part) => part.kind === 'tool-result').map((part) => part.callId)
  assert.deepEqual(localizedResults, [localizedCalls[1], localizedCalls[0]])
  const orphan = Strength.frameTryLocalizeMirror(H, 'd2', digest, [{ role: 'tool', parts: [result('missing', 'no-call')] }])
  assert.equal(orphan.ok, false)
  assert.equal(orphan.error, 'OrphanToolResultId')
  const media = Strength.frameTryLocalizeMirror(H, 'd3', digest, [{ role: 'user', parts: [{ kind: 'media', mediaType: null, contentDigest: 'digest' }] }])
  assert.equal(media.ok, false)
  assert.equal(media.error, 'MediaCannotCrossSession')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Adapter = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");
const Projection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const text = (value) => ({ kind: 'text', text: value })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
const media = (mediaType, contentDigest) => ({ kind: 'media', mediaType, contentDigest })
const msg = (role, parts) => ({ role, parts })
const rendered = (messages) => ({ messages, hostMessageIds: messages.map(() => null), hostIsPhysical: messages.map(() => false) })

test('WHAT[SPEC-INV-009] STRENGTH_009_rendered_message_adapter_roundtrips_wire_semantics_with_host_only_ids', () => {
  const input = rendered([msg('user', [text('hello')]), msg('assistant', [text('world')])])
  const applied = Adapter.tryApplyRenderedMessages('replica-session', H, input)
  assert.equal(applied.ok, true)
  assert.equal(applied.value.length, 2)
  assert.equal(applied.value[0].info.sessionID, 'replica-session')
  assert.doesNotMatch(applied.value[0].info.id, /strength|replica|prefetch/i)
  const decoded = Adapter.decodeMessageView(applied.value)
  assert.equal(Projection.renderWire(decoded.messages), Projection.renderWire(input.messages))
})
test('WHAT[SPEC-INV-009] STRENGTH_009_host_adapter_encodes_strength_tool_pairs_as_native_completed_OpenCode_parts', () => {
  const input = {
    messages: [msg('user', [text('owner mirror')]), msg('assistant', [call('c1', 'read', '{"filePath":"README.md"}'), call('c2', 'grep', '{"pattern":"Strength"}')]), msg('tool', [result('c1', 'alpha'), result('c2', 'beta')])],
    hostMessageIds: [null, 'synthetic-call-message', 'synthetic-result-message'],
    hostIsPhysical: [false, false, false],
  }
  const applied = Strength.tryApplyRenderedMessages('replica-session', H, input)
  assert.equal(applied.ok, true)
  assert.equal(applied.value.length, 2)
  assert.equal(applied.value[1].info.role, 'assistant')
  assert.deepEqual(applied.value[1].parts.map((part) => part.type), ['tool', 'tool'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.callID), ['c1', 'c2'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.tool), ['read', 'grep'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.state.status), ['completed', 'completed'])
  assert.deepEqual(applied.value[1].parts.map((part) => part.state.input), [{ filePath: 'README.md' }, { pattern: 'Strength' }])
  assert.deepEqual(applied.value[1].parts.map((part) => part.state.output), ['alpha', 'beta'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Projection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const bundle = Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }, { toolName: 'grep', canonicalArguments: '{"pattern":"x"}', canonicalResult: 'a:1:x' }] }]).value
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const text = (textValue) => ({ kind: 'text', text: textValue })
const message = (role, parts) => ({ role, parts })

test('WHAT[SPEC-INV-009] STRENGTH_006_009_candidate_wrong_target_and_promoted_replica_reflection_conflict', () => {
  const wrongTarget = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', currentProviderRun: 'target-b', bundle })
  assert.equal(wrongTarget.ok, false)
  assert.equal(wrongTarget.error, 'StrengthCandidateWrongTarget')
  const reflected = Strength.promoted(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', beforeIndex: 0, isReplicaRequest: true, bundle })
  assert.equal(reflected.ok, false)
  assert.equal(reflected.error, 'StrengthPromotedReplicaReflection')
  const badDigest = Strength.candidate(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', currentProviderRun: 'target-a', bundle: { ...bundle, digest: 'tampered' } })
  assert.equal(badDigest.ok, false)
  assert.equal(badDigest.error, 'StrengthFrameDigestMismatch')
  const invalidAnchor = Strength.promoted(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-a', beforeIndex: -1, isReplicaRequest: false, bundle })
  assert.equal(invalidAnchor.ok, false)
  assert.equal(invalidAnchor.error, 'InvalidStrengthAnchor')
})
test('WHAT[SPEC-INV-009] STRENGTH_009_012_policy_promoted_frames_leave_later_pair_anchor_messages_in_place', () => {
  const base = [message('user', [text('u1')]), message('assistant', [text('target-assistant')]), message('user', [text('pair-anchor-stand-in')])]
  const promoted = Strength.promoted(H, { ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-1', beforeIndex: 1, isReplicaRequest: false, bundle }).value
  const rendered = Projection.renderMessagesWithHostIds(snapshot(base), base, [promoted])
  assert.deepEqual(rendered.messages.map((item) => item.role), ['user', 'assistant', 'tool', 'assistant', 'user'])
  assert.equal(rendered.messages.at(-1).parts[0].text, 'pair-anchor-stand-in')
})
test('WHAT[SPEC-INV-009] STRENGTH_009_replica_mirror_replaces_base_then_local_batches_append', () => {
  const mirrorMessages = [message('user', [text('mirror-base')])]
  const mirror = Strength.projectionMirror({ decisionId: 'd1', targetProviderRun: 'target', semanticDigest: 'sem-a', rows: [{ message: mirrorMessages[0], hostMessageId: 'mirror-host-id', hostIsPhysical: true }] }).value
  const local = Strength.replicaLocal(H, { ownerSessionId: 'owner', decisionId: 'd1', bundle }).value
  const base = [message('user', [text('child-physical')])]
  const rendered = Projection.renderMessagesWithHostIds(snapshot(base), base, [mirror, local])
  assert.equal(rendered.messages.length, 3)
  assert.equal(rendered.messages[0].parts[0].text, 'mirror-base')
  assert.equal(rendered.hostMessageIds[0], 'mirror-host-id')
  assert.equal(rendered.hostIsPhysical[0], true)
  assert.equal(rendered.messages[1].role, 'assistant')
  assert.equal(rendered.messages[2].role, 'tool')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Wire = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostCall = (callId, tool, input) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output: 'pending' } })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const tool = (id, sessionId, parts) => ({ info: { id, role: 'tool', sessionID: sessionId }, parts })
const binding = (replica, budget) => Strength.runtimeBinding('owner', replica, `decision-${replica}`, `target-${replica}`, 'Coder', budget, 65536, `semantic-${replica}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const registered = (replica, budget) => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding(replica, budget)).ok, true)
  return runtime
}
const apply = async (runtime, output) => Strength.transformApply(H, runtime, output)

test('WHAT[SPEC-INV-009] STRENGTH_003_004_replica_initial_transform_replaces_bootstrap_with_frozen_owner_mirror', async () => {
  const runtime = registered('replica-initial', 'K1')
  const output = { messages: [user('u1', 'replica-initial', [hostText('Continue.')])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Ready')
  assert.deepEqual(outcome.batches, [])
  assert.deepEqual(outcome.aborted, [])
  const decoded = Wire.decodeMessageView(outcome.output)
  assert.equal(decoded.messages.length, 1)
  assert.equal(decoded.messages[0].parts[0].text, 'owner mirror')
})
}

import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../../../dist/Strength/Surface.js'
import * as Projection from '../../../../../dist/Participant/Provider/Projection/Surface.js'
import * as Adapter from '../../../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import { createLocalEventStore } from '../../../../verification-system/tests/support/local-event-store.mjs'

const H = (text) => `H(${text})`
const frame = Strength.frameTryBuild(H, 65536, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"src/a.fs"}', canonicalResult: 'let a = 1' }, { toolName: 'grep', canonicalArguments: '{"pattern":"a"}', canonicalResult: 'src/a.fs:1:let a = 1' }] }]).value
const text = (value) => ({ kind: 'text', text: value })
const message = (role, parts) => ({ role, parts })
const snapshot = (messages) => Projection.projectionSnapshot(Projection.semanticProjection(messages), { committedPrefix: null, blogFrames: [], transportMessages: [], hostReanchor: null })
const append = async (durability, event) => {
  const result = await Strength.durabilityAppend(durability, event)
  assert.equal(result.ok, true, result.error)
}

test('WHAT[SPEC-INV-008] STRENGTH_INTEGRATION_Prepared_candidate_consumption_Promoted_restart_replay_Traced', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const preparedEvent = Strength.eventPrepared('owner', 'decision-1', 'run-1', 'replica-1', 'K1', 'anchor-1', frame.digest, frame.byteLength, ['payload-a'])
    assert.equal((await Strength.durabilityPublishPrepared(durability, { ownerSessionId: 'owner', decisionId: 'decision-1', targetProviderRun: 'run-1', replicaSessionId: 'replica-1', budget: 'K1', anchorDigest: 'anchor-1', bundle: frame })).kind, 'Published')
    let loaded = await Strength.durabilityLoadProjection(durability)
    assert.equal(loaded.ok, true)
    let projection = loaded.value
    assert.equal(Strength.projectionIsPromoted('decision-1', projection), false)
    assert.equal((await Strength.lifecycleReplayPlans('owner', [{ id: 'run-1' }], frame, projection)).value.length, 0)

    const current = [message('user', [text('inspect the file')])]
    const candidateIntent = Projection.strengthCandidate({ ownerSessionId: 'owner', decisionId: 'decision-1', targetProviderRun: 'run-1', currentProviderRun: 'run-1', bundle: frame })
    const candidateWire = Projection.renderMessagesWithHostIds(H, snapshot(current), current, [candidateIntent])
    assert.deepEqual(candidateWire.messages.map((item) => item.role), ['user', 'assistant', 'tool'])

    const consumed = {
      sessionId: 'owner', physicalUserMessageId: 'user-1', authorityRootUserMessageId: 'user-1', providerRun: 'run-1',
      parts: [{ kind: 'tool-call', callId: 'real-call', name: 'read', args: '{}' }], outcome: 'completed',
    }
    const promotion = Strength.lifecycleReconcileHandle(projection, consumed)
    assert.equal(promotion.view.kind, 'Promoted')
    await append(durability, promotion.event)

    const restarted = Strength.durabilityCreate(local.store)
    projection = (await Strength.durabilityLoadProjection(restarted)).value
    assert.equal(Strength.projectionIsPromoted('decision-1', projection), true)

    const baseWire = [message('user', [text('inspect the file')]), message('assistant', [text('primary output')]), message('user', [text('continue')])]
    const rawResult = Adapter.tryApplyRenderedMessages('owner', H, { messages: baseWire, hostMessageIds: ['user-1', 'run-1', 'user-2'], hostIsPhysical: [false, false, false] })
    assert.equal(rawResult.ok, true)
    const rawBase = rawResult.value
    const replayPlans = await Strength.lifecycleReplayPlans('owner', rawBase.map((value, index) => ({ id: ['user-1', 'run-1', 'user-2'][index] })), frame, projection)
    assert.equal(replayPlans.ok, true)
    const [plan] = replayPlans.value
    assert.equal(plan.beforeMessageIndex, 1)

    const replayed = Projection.renderMessagesWithHostIds(H, snapshot(baseWire), baseWire, Strength.lifecycleReplayIntents(replayPlans.value))
    const written = Adapter.tryApplyRenderedInsertionsPreservingBase('owner', H, rawBase, replayed)
    assert.equal(written.ok, true)
    assert.equal(written.value[0], rawBase[0])
    assert.equal(written.value[3], rawBase[1])
    assert.deepEqual(Adapter.decodeMessageView(written.value).messages.map((item) => item.role), ['user', 'assistant', 'tool', 'assistant', 'user'])

    await append(restarted, Strength.eventTraced('decision-1', 20n, 24n))
    projection = (await Strength.durabilityLoadProjection(restarted)).value
    const tracedPlans = (await Strength.lifecycleReplayPlans('owner', rawBase.map((value, index) => ({ id: ['user-1', 'run-1', 'user-2'][index] })), frame, projection)).value
    const [traced] = tracedPlans
    assert.equal(Strength.lifecycleNeedsRawReplay(22n, traced), true)
    assert.equal(Strength.lifecycleNeedsRawReplay(23n, traced), false)
  } finally { local.close() }
})

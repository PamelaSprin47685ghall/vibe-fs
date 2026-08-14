import assert from 'node:assert/strict'
import test from 'node:test'

import * as Lifecycle from '../../../../../dist/Strength/Lifecycle.js'
import * as Durability from '../../../../../dist/Strength/Persistence/Durability.js'
import { createLocalEventStore } from '../../../../verification-system/tests/support/local-event-store.mjs'
import * as WireDecode from '../../../../../dist/OpenCode/Codec/ProviderWireDecode.js'
import * as WireCapture from '../../../../../dist/OpenCode/Codec/ProviderWireCapture.js'
import * as MessageEdit from '../../../../../dist/OpenCode/Codec/ProjectionMessageEdit.js'
import * as Events from '../../../../../dist/Strength/Events.js'
import * as Frame from '../../../../../dist/Strength/Frame.js'
import * as ProjectionIntent from '../../../../../dist/Participant/Provider/Projection/Intent.js'
import * as ProjectionRenderer from '../../../../../dist/Participant/Provider/Projection/Renderer.js'
const Projection = { ...ProjectionIntent, ...ProjectionRenderer }
import * as StrengthProjection from '../../../../../dist/Strength/Projection/Model.js'
import * as Provider from '../../../../../dist/Participant/Provider/Projection/Model.js'
import { TurnOutcome } from '../../../../../dist/Composition/Turn/Program.js'
import { StrengthBudget } from '../../../../../dist/Strength/Budget.js'
import { MessagePart } from '../../../../../dist/OpenCode/Codec/HostMessageCodec.js'
import * as HostDigest from '../../../../../dist/Host/Digest.js'
import * as Id from '../../../../../dist/Foundation/Identity.js'
import { listItems, toList } from '../../../../verification-system/tests/support/domain.mjs'

const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const text = (value) => new Provider.WirePart(0, [value])
const message = (role, parts) => ({ Role: role, Parts: toList(parts) })
const exchange = (tool, args, result) => ({ ToolName: tool, CanonicalArguments: args, CanonicalResult: result })
const batch = (ordinal, exchanges) => ({ RequestOrdinal: ordinal, Exchanges: toList(exchanges) })
const providerCall = (id, name, args) => new MessagePart(2, [id, name, args])

const snapshot = (messages) => new Projection.ProjectionSnapshot(
  { ProviderId: undefined, ModelId: undefined, Variant: undefined, Tools: toList([]), System: toList([]), Messages: toList(messages) },
  undefined,
  toList([]),
  undefined,
  undefined,
)

const rawMessages = (sessionId, messages, ids) => {
  const rendered = {
    Messages: toList(messages),
    HostMessageIds: toList(ids),
    HostIsPhysical: toList(ids.map(() => false)),
  }
  const applied = resultOf(MessageEdit.tryApplyRenderedMessages(sessionId, HostDigest.sha256Hex, rendered))
  assert.equal(applied.ok, true)
  return listItems(applied.value)
}

test('STRENGTH_INTEGRATION_Prepared_candidate_consumption_Promoted_restart_replay_Traced', async () => {
  const local = createLocalEventStore()
  const store = local.store
  const durability = Durability.create(store)
  const owner = session('owner')
  const target = run('run-1')
  const id = decision('decision-1')
  const bundle = resultOf(Frame.StrengthFrame_tryBuild(
    HostDigest.sha256Hex,
    65536,
    toList([
      batch(1, [
        exchange('read', '{"filePath":"src/a.fs"}', 'let a = 1'),
        exchange('grep', '{"pattern":"a"}', 'src/a.fs:1:let a = 1'),
      ]),
    ]),
  )).value

  const published = await durability.PublishPrepared({
    OwnerSessionId: owner,
    DecisionId: id,
    TargetProviderRun: target,
    ReplicaSessionId: session('replica-1'),
    Budget: StrengthBudget.K1,
    AnchorDigest: 'anchor-1',
    Bundle: bundle,
  })
  assert.equal(caseOf(published), 'Published')

  let projection = resultOf(await durability.LoadProjection()).value
  assert.equal(StrengthProjection.StrengthProjectionModule_isPromoted(id, projection), false)
  assert.equal(listItems(resultOf(
    await Lifecycle.StrengthLifecycle_replayPlans(
      owner,
      (m) => m.id,
      toList([{ id: 'run-1' }]),
      durability.LoadFrameBundle,
      projection,
    ),
  ).value).length, 0)

  const current = [message('user', [text('inspect the file')])]
  const candidateIntent = Projection.ProjectionIntentModule_strengthCandidate(owner, id, target, target, bundle)
  const candidateWire = Projection.ProjectionRenderer_renderMessagesWithHostIds(
    HostDigest.sha256Hex,
    snapshot(current),
    toList(current),
    toList([candidateIntent]),
  )
  assert.deepEqual(listItems(candidateWire.Messages).map((item) => item.Role), ['user', 'assistant', 'tool'])

  const consumed = {
    ProviderRun: target,
    Parts: [providerCall('real-call', 'read', '{"filePath":"other"}')],
    Outcome: new TurnOutcome(1, ['continue']),
  }
  const promotion = Lifecycle.StrengthLifecycle_reconcileEvent(projection, consumed)
  assert.equal(caseOf(promotion), 'Promoted')
  assert.equal(resultOf(await durability.Append(promotion)).ok, true)

  // Restart view: reconstruct only from the unified EventStore + payload closure.
  const restarted = Durability.create(store)
  projection = resultOf(await restarted.LoadProjection()).value
  assert.equal(StrengthProjection.StrengthProjectionModule_isPromoted(id, projection), true)

  const baseWire = [
    message('user', [text('inspect the file')]),
    message('assistant', [text('primary output')]),
    message('user', [text('continue')]),
  ]
  const rawBase = rawMessages('owner', baseWire, ['user-1', 'run-1', 'user-2'])
  const replayPlans = resultOf(
    await Lifecycle.StrengthLifecycle_replayPlans(owner, WireDecode.hostMessageId, toList(rawBase), restarted.LoadFrameBundle, projection),
  )
  assert.equal(replayPlans.ok, true)
  const [plan] = listItems(replayPlans.value)
  assert.equal(plan.BeforeMessageIndex, 1)

  const replayed = Projection.ProjectionRenderer_renderMessagesWithHostIds(
    HostDigest.sha256Hex,
    snapshot(baseWire),
    toList(baseWire),
    Lifecycle.StrengthLifecycle_replayIntents(replayPlans.value),
  )
  const written = resultOf(
    MessageEdit.tryApplyRenderedInsertionsPreservingBase('owner', HostDigest.sha256Hex, toList(rawBase), replayed),
  )
  assert.equal(written.ok, true)
  const writtenRaw = listItems(written.value)
  assert.equal(writtenRaw[0], rawBase[0], 'existing Host rows stay object-identical')
  assert.equal(writtenRaw[3], rawBase[1], 'target assistant stays after replayed Strength frame')
  assert.deepEqual(listItems(WireCapture.decodeMessageView(written.value).Messages).map((item) => item.Role), [
    'user', 'assistant', 'tool', 'assistant', 'user',
  ])

  assert.equal(resultOf(await restarted.Append(Events.StrengthEvents_traced(id, 20n, 24n))).ok, true)
  projection = resultOf(await restarted.LoadProjection()).value
  const tracedPlans = resultOf(
    await Lifecycle.StrengthLifecycle_replayPlans(owner, WireDecode.hostMessageId, toList(rawBase), restarted.LoadFrameBundle, projection),
  ).value
  const [traced] = listItems(tracedPlans)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(22n, traced), true)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(23n, traced), false)
  local.close()
})

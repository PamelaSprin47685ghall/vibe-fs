import assert from 'node:assert/strict'
import test from 'node:test'

import * as Lifecycle from '../../../dist/Application/Strength/StrengthLifecycle.js'
import * as Durability from '../../../dist/Infrastructure/Persist/StrengthDurability.js'
import * as PersistStore from '../../../dist/Infrastructure/Persist/EventStore.js'
import * as Raw from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import * as ProjectionAdapter from '../../../dist/Infrastructure/OpenCode/Codec/Projection.js'
import * as Events from '../../../dist/Domain/StrengthEvents.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as ProjectionIntent from '../../../dist/Domain/ProjectionIntent.js'
import * as ProjectionRenderer from '../../../dist/Domain/ProjectionRenderer.js'
const Projection = { ...ProjectionIntent, ...ProjectionRenderer }
import * as StrengthProjection from '../../../dist/Domain/StrengthProjection.js'
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import { MessagePart } from '../../../dist/Infrastructure/OpenCode/Codec/HostMessageCodec.js'
import * as HostDigest from '../../../dist/Host/HostDigest.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

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
  const applied = resultOf(ProjectionAdapter.tryApplyRenderedMessages(sessionId, HostDigest.sha256Hex, rendered))
  assert.equal(applied.ok, true)
  return listItems(applied.value)
}

test('STRENGTH_INTEGRATION_Prepared_candidate_consumption_Promoted_restart_replay_Traced', () => {
  const raw = Raw.GitRawStore_createInMemory()
  const store = PersistStore.EventStore_create(raw)
  const durability = Durability.create(raw, store)
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

  const published = durability.PublishPrepared({
    OwnerSessionId: owner,
    DecisionId: id,
    TargetProviderRun: target,
    ReplicaSessionId: session('replica-1'),
    Budget: StrengthBudget.K1,
    AnchorDigest: 'anchor-1',
    Bundle: bundle,
  })
  assert.equal(caseOf(published), 'Published')

  let projection = resultOf(durability.LoadProjection()).value
  assert.equal(StrengthProjection.StrengthProjectionModule_isPromoted(id, projection), false)
  assert.equal(listItems(resultOf(
    Lifecycle.StrengthLifecycle_replayPlans(
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
  assert.equal(resultOf(durability.Append(promotion)).ok, true)

  // Restart view: reconstruct only from the unified EventStore + payload closure.
  const restarted = Durability.create(raw, store)
  projection = resultOf(restarted.LoadProjection()).value
  assert.equal(StrengthProjection.StrengthProjectionModule_isPromoted(id, projection), true)

  const baseWire = [
    message('user', [text('inspect the file')]),
    message('assistant', [text('primary output')]),
    message('user', [text('continue')]),
  ]
  const rawBase = rawMessages('owner', baseWire, ['user-1', 'run-1', 'user-2'])
  const replayPlans = resultOf(
    Lifecycle.StrengthLifecycle_replayPlans(owner, ProjectionAdapter.hostMessageId, toList(rawBase), restarted.LoadFrameBundle, projection),
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
    ProjectionAdapter.tryApplyRenderedInsertionsPreservingBase('owner', HostDigest.sha256Hex, toList(rawBase), replayed),
  )
  assert.equal(written.ok, true)
  const writtenRaw = listItems(written.value)
  assert.equal(writtenRaw[0], rawBase[0], 'existing Host rows stay object-identical')
  assert.equal(writtenRaw[3], rawBase[1], 'target assistant stays after replayed Strength frame')
  assert.deepEqual(listItems(ProjectionAdapter.decodeMessageView(written.value).Messages).map((item) => item.Role), [
    'user', 'assistant', 'tool', 'assistant', 'user',
  ])

  assert.equal(resultOf(restarted.Append(Events.StrengthEvents_traced(id, 20n, 24n))).ok, true)
  projection = resultOf(restarted.LoadProjection()).value
  const tracedPlans = resultOf(
    Lifecycle.StrengthLifecycle_replayPlans(owner, ProjectionAdapter.hostMessageId, toList(rawBase), restarted.LoadFrameBundle, projection),
  ).value
  const [traced] = listItems(tracedPlans)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(22n, traced), true)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(23n, traced), false)
})

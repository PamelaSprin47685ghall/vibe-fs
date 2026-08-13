import assert from 'node:assert/strict'
import test from 'node:test'

import * as Durability from '../../../dist/Infrastructure/Persist/StrengthDurability.js'
import * as Raw from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import * as PersistStore from '../../../dist/Infrastructure/Persist/EventStore.js'
import * as Events from '../../../dist/Domain/StrengthEvents.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Projection from '../../../dist/Domain/StrengthProjection.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import * as HostDigest from '../../../dist/Host/HostDigest.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const exchange = (tool, args, result) => ({ ToolName: tool, CanonicalArguments: args, CanonicalResult: result })
const batch = (ordinal, exchanges) => ({ RequestOrdinal: ordinal, Exchanges: toList(exchanges) })

const frame = () => resultOf(Frame.StrengthFrame_tryBuild(
  HostDigest.sha256Hex,
  10000,
  toList([batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')])]),
)).value

test('STRENGTH_006_008_durability_port_publishes_payload_closure_and_reloads_the_same_bundle', async () => {
  const raw = Raw.GitRawStore_createInMemory()
  const store = PersistStore.EventStore_create(raw)
  const durability = Durability.create(raw, store)
  const bundle = frame()

  const published = await durability.PublishPrepared({
    OwnerSessionId: session('owner'),
    DecisionId: decision('d1'),
    TargetProviderRun: run('run-1'),
    ReplicaSessionId: session('replica-1'),
    Budget: StrengthBudget.K1,
    AnchorDigest: 'anchor-a',
    Bundle: bundle,
  })
  assert.equal(caseOf(published), 'Published')

  let projection = resultOf(await durability.LoadProjection())
  assert.equal(projection.ok, true)
  const view = Projection.StrengthProjectionModule_tryCandidate(decision('d1'), projection.value)
  assert.equal(view.Prepared.FrameDigest, bundle.Digest)
  assert.equal([...view.Prepared.MaterialPayloads].length, 1)

  const loaded = resultOf(await durability.LoadFrameBundle(view.Prepared))
  assert.equal(loaded.ok, true)
  assert.equal(loaded.value.Digest, bundle.Digest)
  assert.equal(loaded.value.ByteLength, bundle.ByteLength)

  const promotion = Events.StrengthEvents_promoted(
    session('owner'), decision('d1'), run('run-1'), bundle.Digest, view.Prepared.MaterialPayloads,
  )
  assert.equal(resultOf(await durability.Append(promotion)).ok, true)
  assert.equal(resultOf(await durability.Append(Events.StrengthEvents_traced(decision('d1'), 5n, 7n))).ok, true)

  projection = resultOf(await durability.LoadProjection())
  assert.equal(projection.ok, true)
  assert.equal(Projection.StrengthProjectionModule_isPromoted(decision('d1'), projection.value), true)
  const range = Projection.StrengthProjectionModule_tryTraceRange(decision('d1'), projection.value)
  assert.equal(range.StartInclusive, 5n)
  assert.equal(range.EndExclusive, 7n)
})

test('STRENGTH_006_durability_port_rejects_conflicting_Prepared_identity', async () => {
  const raw = Raw.GitRawStore_createInMemory()
  const store = PersistStore.EventStore_create(raw)
  const durability = Durability.create(raw, store)
  const first = frame()

  assert.equal(caseOf(await durability.PublishPrepared({
    OwnerSessionId: session('owner'),
    DecisionId: decision('d1'),
    TargetProviderRun: run('run-1'),
    ReplicaSessionId: session('replica-1'),
    Budget: StrengthBudget.K1,
    AnchorDigest: 'anchor-a',
    Bundle: first,
  })), 'Published')

  const other = resultOf(Frame.StrengthFrame_tryBuild(
    HostDigest.sha256Hex,
    10000,
    toList([batch(1, [exchange('grep', '{"pattern":"x"}', 'a:1:x')])]),
  )).value

  const conflict = await durability.PublishPrepared({
    OwnerSessionId: session('owner'),
    DecisionId: decision('d1'),
    TargetProviderRun: run('run-1'),
    ReplicaSessionId: session('replica-2'),
    Budget: StrengthBudget.K1,
    AnchorDigest: 'anchor-a',
    Bundle: other,
  })
  assert.equal(caseOf(conflict), 'StorageInvalid')
})

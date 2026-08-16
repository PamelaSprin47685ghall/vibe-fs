import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const H = (text) => createHash('sha256').update(text).digest('hex')
const frame = (toolName = 'read', args = '{"filePath":"a"}', result = 'alpha') => Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName, canonicalArguments: args, canonicalResult: result }] }]).value
const publishRequest = (bundle, replica = 'replica-1') => ({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'run-1', replicaSessionId: replica, budget: 'K1', anchorDigest: 'anchor-a', bundle })

test('WHAT[SPEC-INV-006] STRENGTH_006_008_durability_port_publishes_payload_closure_and_reloads_the_same_bundle', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame()
    assert.equal((await Strength.durabilityPublishPrepared(durability, publishRequest(bundle))).kind, 'Published')
    let projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('d1', projection)
    assert.equal(view.prepared.frameDigest, bundle.digest)
    assert.equal(view.prepared.materialPayloads.length, 1)
    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, 'd1')
    assert.equal(loaded.ok, true)
    assert.equal(loaded.value.digest, bundle.digest)
    assert.equal(loaded.value.byteLength, bundle.byteLength)
    assert.equal((await Strength.durabilityAppend(durability, Strength.eventPromoted('owner', 'd1', 'run-1', bundle.digest, view.prepared.materialPayloads))).ok, true)
    assert.equal((await Strength.durabilityAppend(durability, Strength.eventTraced('d1', 5n, 7n))).ok, true)
    projection = (await Strength.durabilityLoadProjection(durability)).value
    assert.equal(Strength.projectionIsPromoted('d1', projection), true)
    assert.deepEqual(Strength.projectionTraceRange('d1', projection), { startInclusive: 5n, endExclusive: 7n })
  } finally { local.close() }
})

test('WHAT[SPEC-INV-006] STRENGTH_006_durability_port_rejects_conflicting_Prepared_identity', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const first = frame()
    assert.equal((await Strength.durabilityPublishPrepared(durability, publishRequest(first))).kind, 'Published')
    const other = frame('grep', '{"pattern":"x"}', 'a:1:x')
    const conflict = await Strength.durabilityPublishPrepared(durability, publishRequest(other, 'replica-2'))
    assert.equal(conflict.kind, 'StorageInvalid')
  } finally { local.close() }
})

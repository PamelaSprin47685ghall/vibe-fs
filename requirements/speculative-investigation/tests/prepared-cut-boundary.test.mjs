// SPEC-INV-006 (fatal-sweep): the Strength Prepared cut boundary —
// publishPrepared's SemanticCut path has no optional fatal hook left; the
// durable bad-fact + ProjectionCutTail pair is already settled by the
// Integrator before the call returns, so local Current never leads the
// receipt. The fatal decision belongs to the single composition owner
// (PluginStrengthPorts.commitAppendResult for Append; the Speculate publish
// path returns the Rejected outcome to its owner), never to an unregistered
// module-global handler inside the durability adapter.
//
// This test drives the compiled production path (Strength durability surface +
// the real IEventStore append) on a real store: legal pre-state + legal command
// always folds (unit-level proof for the Prepared constructor), the complete
// payload closure (frame bundle + material refs) round-trips, and reopen
// observes exactly the durable receipts.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const makeDir = (prefix) => mkdtempSync(join(tmpdir(), prefix))
const H = (text) => createHash('sha256').update(text).digest('hex')
const frame = (toolName = 'read', args = '{"filePath":"a"}', result = 'alpha') =>
  Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName, canonicalArguments: args, canonicalResult: result }] }]).value
const publishRequest = (bundle, decision = 'cut-d1', replica = 'replica-1') => ({
  ownerSessionId: 'owner',
  decisionId: decision,
  targetProviderRun: 'run-1',
  replicaSessionId: replica,
  budget: 'K1',
  anchorDigest: 'anchor-a',
  bundle,
})

test('WHAT[SPEC-INV-006] prepared_cut_legal_command_is_accepted_by_the_fold', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame()
    const published = await Strength.durabilityPublishPrepared(durability, publishRequest(bundle))
    assert.equal(published.kind, 'Published')

    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('cut-d1', projection)
    assert.equal(view.prepared.frameDigest, bundle.digest)
    assert.equal(view.prepared.byteLength, bundle.byteLength)
    assert.equal(view.prepared.materialPayloads.length, 1)

    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, 'cut-d1')
    assert.equal(loaded.ok, true)
    assert.equal(loaded.value.digest, bundle.digest)
    assert.equal(loaded.value.byteLength, bundle.byteLength)
  } finally { local.close() }
})

test('WHAT[SPEC-INV-006] prepared_cut_payload_closure_is_complete_before_the_receipt', async () => {
  const local = createLocalEventStore()
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame('grep', '{"pattern":"x"}', 'a:1:x')
    const published = await Strength.durabilityPublishPrepared(durability, publishRequest(bundle, 'cut-d2'))
    assert.equal(published.kind, 'Published')

    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('cut-d2', projection)
    assert.deepEqual(
      Object.keys(view.prepared).sort(),
      ['anchorDigest', 'budget', 'byteLength', 'decisionId', 'frameDigest', 'materialPayloads', 'ownerSessionId', 'replicaSessionId', 'targetProviderRun'].sort(),
      'the complete Prepared write set rides the durable fact, not one field',
    )
  } finally { local.close() }
})

test('WHAT[SPEC-INV-006] prepared_cut_reopen_observes_only_durable_receipts', async () => {
  const base = makeDir('wxs-strength-cut-reopen-')
  const commonDir = join(base, '.git')
  const local = createLocalEventStore({ commonDir })
  try {
    const durability = Strength.durabilityCreate(local.store)
    const bundle = frame()
    assert.equal((await Strength.durabilityPublishPrepared(durability, publishRequest(bundle, 'cut-d3'))).kind, 'Published')
    const before = (await Strength.durabilityLoadProjection(durability)).value
    assert.ok(Strength.projectionCandidate('cut-d3', before))
  } finally { local.close() }

  const reopened = createLocalEventStore({ commonDir })
  try {
    const durability = Strength.durabilityCreate(reopened.store)
    const projection = (await Strength.durabilityLoadProjection(durability)).value
    const view = Strength.projectionCandidate('cut-d3', projection)
    assert.ok(view, 'reopen replays the durable Prepared receipt into Current')
    const loaded = await Strength.durabilityLoadBundleForDecision(durability, projection, 'cut-d3')
    assert.equal(loaded.ok, true, 'payload closure survives reopen with the fact')
  } finally {
    reopened.close()
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[SPEC-INV-006] prepared_cut_has_no_optional_fatal_handler_path', async () => {
  const { readFileSync } = await import('node:fs')
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Strength/Persistence/Durability.fs', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(source, /fatalTripHandler|setFatalTripHandler/)
})

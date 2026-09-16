import test from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

// WHAT[EPI-028]: the research export separates identifiable protocol objects
// from external truth. Sphinx observations are encoded by the pure
// `encodeSphinxEnvelope` codec and appended to the canonical EventStore; the
// bundle is rendered from the durable spine by the pure `exportFromEvents`
// fold. The bundle is an envelope skeleton: events, head, hashes, answer,
// branch tree, initial disposition and two claim kinds are populated, while
// plugin/schema manifests, model manifest, randomization matrix, resource
// ledger, certificates, diagnostics, reflective disposition and minority
// modes are structurally present but content-vacuous. A fresh replay of the
// bundle yields the same semantic and answer hashes; and no claim is
// rendered as externally grounded without an external source.

const REQUIRED_BUNDLE_FIELDS = [
  'events',
  'eventHead',
  'semanticHash',
  'answerHash',
  'pluginManifests',
  'schemaManifests',
  'modelManifest',
  'branchTree',
  'randomizationMatrix',
  'resourceLedger',
  'certificates',
  'rankingDiagnostics',
  'framingDiagnostics',
  'calibrationDiagnostics',
  'initialDisposition',
  'reflectiveDisposition',
  'minorityModes',
  'answer',
  'claims',
]

const CLAIM_KINDS = [
  'model-belief',
  'reflective-model-belief',
  'cross-branch-consensus',
  'protocol-stable-judgment',
  'externally-grounded-claim',
]

const mustEncode = (event) => {
  const result = gecSurface.encodeSphinxEnvelope(event)
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
  return result.envelope
}

const readSpine = (handle, stream) => {
  const head = eventStore.head(handle, stream)
  if (head == null) return []
  const ordered = []
  let cursor = head
  while (cursor != null) {
    const envelope = eventStore.read(handle, cursor)
    assert.ok(envelope != null, `durable spine is missing ${cursor}`)
    ordered.unshift(envelope)
    cursor = envelope.parents[0] ?? null
  }
  return ordered
}

const buildSpineWithObservations = async (t, observations) => {
  const root = mkdtempSync(join(tmpdir(), 'sphinx-spine-export-'))
  execFileSync('git', ['init', '-q', root])
  t.after(() => rmSync(root, { recursive: true, force: true }))
  const commonDir = join(root, '.git')
  const inquiryId = `iq_${randomUUID()}`
  const stream = `sphinx/${inquiryId}`
  const writer = eventStore.create(commonDir, `writer-export-${inquiryId.slice(3, 11)}`)
  try {
    let parents = []
    let revision = 0
    for (const observation of observations) {
      const envelope = mustEncode({
        inquiryId,
        revision,
        kind: 'observation-accepted',
        parents,
        payload: observation,
      })
      const receipt = await eventStore.append(writer, [envelope])
      assert.equal(receipt.ok, true, JSON.stringify(receipt.error ?? null))
      parents = [envelope.id]
      revision += 1
    }
    const answerEnvelope = mustEncode({
      inquiryId,
      revision,
      kind: 'answer-committed',
      parents,
      payload: { text: 'Small reversible changes with matched evidence ship faster.' },
    })
    const answerReceipt = await eventStore.append(writer, [answerEnvelope])
    assert.equal(answerReceipt.ok, true, JSON.stringify(answerReceipt.error ?? null))
  } finally {
    eventStore.dispose(writer)
  }
  // Read the spine back through a fresh handle: the export source is the
  // durable log, never process memory.
  const reader = eventStore.create(commonDir, `writer-export-reader-${inquiryId.slice(3, 11)}`)
  try {
    return readSpine(reader, stream)
  } finally {
    eventStore.dispose(reader)
  }
}

test('WHAT[EPI-028] export_contains_every_required_field_and_replay_matches_semantic_and_answer_hashes', async (t) => {
  const events = await buildSpineWithObservations(t, [
    { claim: 'Small changes fail less often.', source: { id: 'doc-sre-1', kind: 'document' } },
  ])

  const bundle = gecSurface.exportFromEvents({ events })
  assert.equal(bundle.error, undefined)
  for (const field of REQUIRED_BUNDLE_FIELDS) {
    assert.ok(bundle[field] !== undefined && bundle[field] !== null, `export bundle missing ${field}`)
  }
  for (const claim of bundle.claims) {
    assert.ok(CLAIM_KINDS.includes(claim.kind), `unknown claim kind ${claim.kind}`)
  }

  // Envelope-skeleton honesty: manifests, matrices, ledgers and diagnostics
  // are structurally present but content-vacuous until callers supply them.
  assert.equal(bundle.modelManifest.id, 'sphinx-unknown')
  assert.deepEqual(bundle.randomizationMatrix.assignments, [])
  assert.deepEqual(bundle.resourceLedger.entries, [])
  assert.deepEqual(bundle.certificates, {})
  assert.deepEqual(bundle.minorityModes, [])

  // A fresh process replaying only the bundle must converge on both hashes.
  const replayed = gecSurface.replayExportBundle({ bundle })
  assert.equal(replayed.error, undefined)
  assert.equal(replayed.semanticHash, bundle.semanticHash)
  assert.equal(replayed.answerHash, bundle.answerHash)

  const replayedAgain = gecSurface.replayExportBundle({
    bundle: JSON.parse(JSON.stringify(bundle)),
  })
  assert.equal(replayedAgain.semanticHash, bundle.semanticHash)
  assert.equal(replayedAgain.answerHash, bundle.answerHash)
})

test('WHAT[EPI-028] externally_grounded_claims_stay_empty_without_external_source', async (t) => {
  const modelOnly = gecSurface.exportFromEvents({
    events: await buildSpineWithObservations(t, [
      { claim: 'The model believes small changes are safer.', source: null },
    ]),
  })
  assert.equal(modelOnly.error, undefined)
  const groundedEmpty = modelOnly.claims.filter((claim) => claim.kind === 'externally-grounded-claim')
  assert.equal(groundedEmpty.length, 0)
  assert.ok(
    modelOnly.claims.some((claim) => claim.kind === 'model-belief'),
    'model-only content must still render as model belief, not vanish',
  )

  const sourced = gecSurface.exportFromEvents({
    events: await buildSpineWithObservations(t, [
      { claim: 'The model believes small changes are safer.', source: null },
      { claim: 'Controlled rollout data shows fewer failures.', source: { id: 'doc-sre-2', kind: 'document' } },
    ]),
  })
  assert.equal(sourced.error, undefined)
  const grounded = sourced.claims.filter((claim) => claim.kind === 'externally-grounded-claim')
  assert.equal(grounded.length, 1)
  assert.equal(grounded[0].sources[0].id, 'doc-sre-2')
})

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { loadGecSurface } from './gec-support.mjs'

// WHAT[EPI-028]: the research export separates identifiable protocol objects
// from external truth. The bundle carries every required manifest, tree,
// matrix, ledger, certificate and diagnostic field; a fresh replay of the
// bundle yields the same semantic and answer hashes; and no claim is
// rendered as externally grounded without an external source.

const initialEnvelope = {
  schema: { id: 'sphinx.probe.open/input@1', hash: 'input-hash-001' },
  payload: { question: 'Which practice actually reduces defects?' },
}

const pluginLock = [{ id: 'sphinx-legacy', release: '0.8.4', abiHash: 'abi-hash-001' }]

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

async function buildStoreWithObservations(t, gecSurface, observations) {
  const commonDir = await mkdtemp(join(tmpdir(), 'sphinx-gec-export-'))
  t.after(() => rm(commonDir, { recursive: true, force: true }))
  const inquiryId = `iq_${randomUUID()}`
  const tag = inquiryId.slice(3, 11)
  const { storeId } = await gecSurface.createEventStore({ commonDir, inquiryId, initialEnvelope, pluginLock })
  let revision = 0
  for (const [index, observation] of observations.entries()) {
    const appended = await gecSurface.appendEvent({
      storeId,
      expectedRevision: revision,
      event: {
        eventId: `ev_${tag}_${index}`,
        kind: 'ObservationAccepted',
        inquiryId,
        revision,
        parent: index === 0 ? null : `ev_${tag}_${index - 1}`,
        workId: `work_branch_${index}`,
        attempt: 1,
        payload: observation,
      },
    })
    assert.equal(appended.error, undefined)
    revision = appended.revision
  }
  await gecSurface.appendEvent({
    storeId,
    expectedRevision: revision,
    event: {
      eventId: `ev_${tag}_answer`,
      kind: 'AnswerCommitted',
      inquiryId,
      revision,
      parent: `ev_${tag}_${observations.length - 1}`,
      payload: { text: 'Small reversible changes with matched evidence ship faster.' },
    },
  })
  return storeId
}

test('WHAT[EPI-028] export_contains_every_required_field_and_replay_matches_semantic_and_answer_hashes', async (t) => {
  const gecSurface = await loadGecSurface()
  const storeId = await buildStoreWithObservations(t, gecSurface, [
    { claim: 'Small changes fail less often.', source: { id: 'doc-sre-1', kind: 'document' } },
  ])

  const bundle = await gecSurface.exportInquiry({ storeId })
  assert.equal(bundle.error, undefined)
  for (const field of REQUIRED_BUNDLE_FIELDS) {
    assert.ok(bundle[field] !== undefined && bundle[field] !== null, `export bundle missing ${field}`)
  }
  for (const claim of bundle.claims) {
    assert.ok(CLAIM_KINDS.includes(claim.kind), `unknown claim kind ${claim.kind}`)
  }

  // A fresh process replaying only the bundle must converge on both hashes.
  const replayed = await gecSurface.replayExport({ bundle })
  assert.equal(replayed.error, undefined)
  assert.equal(replayed.semanticHash, bundle.semanticHash)
  assert.equal(replayed.answerHash, bundle.answerHash)

  const replayedAgain = await gecSurface.replayExport({
    bundle: JSON.parse(JSON.stringify(bundle)),
  })
  assert.equal(replayedAgain.semanticHash, bundle.semanticHash)
  assert.equal(replayedAgain.answerHash, bundle.answerHash)
})

test('WHAT[EPI-028] externally_grounded_claims_stay_empty_without_external_source', async (t) => {
  const gecSurface = await loadGecSurface()

  const modelOnlyStore = await buildStoreWithObservations(t, gecSurface, [
    { claim: 'The model believes small changes are safer.', source: null },
  ])
  const modelOnly = await gecSurface.exportInquiry({ storeId: modelOnlyStore })
  assert.equal(modelOnly.error, undefined)
  const groundedEmpty = modelOnly.claims.filter((claim) => claim.kind === 'externally-grounded-claim')
  assert.equal(groundedEmpty.length, 0)
  assert.ok(
    modelOnly.claims.some((claim) => claim.kind === 'model-belief'),
    'model-only content must still render as model belief, not vanish',
  )

  const sourcedStore = await buildStoreWithObservations(t, gecSurface, [
    { claim: 'The model believes small changes are safer.', source: null },
    { claim: 'Controlled rollout data shows fewer failures.', source: { id: 'doc-sre-2', kind: 'document' } },
  ])
  const sourced = await gecSurface.exportInquiry({ storeId: sourcedStore })
  assert.equal(sourced.error, undefined)
  const grounded = sourced.claims.filter((claim) => claim.kind === 'externally-grounded-claim')
  assert.equal(grounded.length, 1)
  assert.equal(grounded[0].sources[0].id, 'doc-sre-2')
})

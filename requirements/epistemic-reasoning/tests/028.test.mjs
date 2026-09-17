import test from 'node:test'

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { randomUUID } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const SEED = 0x51eca129
const WAVES = 12
const SUBJECTS = Array.from({ length: 16 }, (_, index) => `witness-${String(index).padStart(2, '0')}`)
const TREATMENTS = ['wording-a', 'wording-b']
const CANDIDATES = ['c1', 'c2', 'c3']
const PLANTED_EFFECT = 0.15
const xorshift = (initial) => {
  let state = initial >>> 0 || 1
  return () => {
    state ^= state << 13
    state >>>= 0
    state ^= state >>> 17
    state ^= state << 5
    state >>>= 0
    return state / 4294967296
  }
}
const waveInput = (wave) => ({
  rootSnapshot: `snap-soak-${String(wave).padStart(2, '0')}`,
  seed: (SEED + wave * 7919) >>> 0,
  subjects: [...SUBJECTS],
  treatments: [...TREATMENTS],
  candidates: [...CANDIDATES],
})
const waveEvents = (wave, assignment) => {
  const inquiry = `iq_soak${String(wave).padStart(4, '0')}`
  const branch = `branch_soak${String(wave).padStart(4, '0')}`
  const work = `work_soak${String(wave).padStart(4, '0')}`
  const lock = [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }]
  return [
    {
      type: 'InquiryCreated',
      inquiry,
      revision: 0,
      parent: 'none',
      question: 'soak probe',
      pluginLock: lock,
      budget: { compute: 100, budget: 100 },
      root: {
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { wave } },
        adapter: 'question-to-root:v1',
      },
    },
    {
      type: 'WorkPlanned',
      inquiry,
      revision: 1,
      parent: 'ev0',
      work: { id: work, branch, attempt: 1 },
    },
    {
      type: 'ObservationAccepted',
      inquiry,
      revision: 2,
      parent: 'ev1',
      observation: {
        rootSnapshotHash: `snap-soak-${String(wave).padStart(2, '0')}`,
        branch,
        work,
        attempt: 1,
        pluginLock: lock,
        schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
        promptId: `prompt-soak-${wave}`,
        questionId: `q-soak-${wave}`,
        wording: { frame: 'open', polarity: 'neutral' },
        permutation: { candidates: [...CANDIDATES], labels: ['A', 'B', 'C'], order: [0, 1, 2] },
        treatment: assignment,
        blindToken: `blind01soakwave${String(wave).padStart(4, '0')}`,
        seed: `seed-soak-${wave}`,
        model: { provider: 'local-sim', name: 'sim-1' },
        sampling: { temperature: 0, maxTokens: 16 },
        usage: { promptTokens: 5, completionTokens: 3 },
        payload: { wave },
      },
    },
    { type: 'BudgetDebited', inquiry, revision: 3, parent: 'ev2', debit: { compute: 7, budget: 7 } },
  ]
}

test('WHAT[EPI-028] soak_export_bundles_replay_to_identical_hashes_every_wave', async () => {
  for (let wave = 0; wave < WAVES; wave += 1) {
    const events = waveEvents(wave, 'wording-a')
    const bundle = gecSurface.exportFromEvents({ events })
    assert.equal(bundle.error, undefined)
    assert.ok(bundle.semanticHash && bundle.answerHash)
    const replayed = gecSurface.replayExportBundle({ bundle })
    assert.equal(replayed.error, undefined)
    assert.equal(replayed.semanticHash, bundle.semanticHash, `wave ${wave} semantic hash must survive export`)
    assert.equal(replayed.answerHash, bundle.answerHash, `wave ${wave} answer hash must survive export`)
  }
})
}

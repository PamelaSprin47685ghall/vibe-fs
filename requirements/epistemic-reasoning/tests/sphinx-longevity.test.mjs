import assert from 'node:assert/strict'
import test from 'node:test'

import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

// Sphinx longevity soak: seeded multi-wave elicitation with cross-wave
// determinism, replay stability and export conservation. Mirrors the
// capacity-soak convention (xorshift seed, fixed rounds, exact auditor):
// every number below is reproducible from SEED, no wall clock, no provider.

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

test('WHAT[EPI-023] soak_seeded_waves_reproduce_identical_matrices_and_stable_signed_effects', async () => {
  const auditor = { operations: 0, matrices: new Set(), estimates: [] }
  for (let wave = 0; wave < WAVES; wave += 1) {
    const input = waveInput(wave)
    const first = gecSurface.splitBallot(input)
    assert.equal(first.ok, true)
    const second = gecSurface.splitBallot(input)
    assert.deepEqual(second, first, `wave ${wave} must be identical under the same seed`)
    assert.equal(first.assignments.length, SUBJECTS.length)
    const counts = { 'wording-a': 0, 'wording-b': 0 }
    const tokens = new Set()
    for (const item of first.assignments) {
      counts[item.treatment] += 1
      tokens.add(item.blindToken)
    }
    assert.deepEqual(counts, { 'wording-a': 8, 'wording-b': 8 }, `wave ${wave} must stay balanced`)
    assert.equal(tokens.size, SUBJECTS.length, `wave ${wave} blind tokens must be unique`)
    auditor.matrices.add(JSON.stringify(first.assignments.map((item) => item.treatment)))

    const next = xorshift(input.seed)
    const outcomes = {}
    for (const item of first.assignments) {
      const edge = item.treatment === 'wording-b' ? PLANTED_EFFECT : 0
      outcomes[item.subject] = next() < 0.5 + edge ? 1 : 0
    }
    const estimated = gecSurface.splitBallot({
      ...input,
      outcomes,
      estimand: 'difference-in-means',
      contrast: ['wording-b', 'wording-a'],
    })
    assert.equal(estimated.ok, true)
    assert.ok(estimated.effect.estimate > -0.1, `wave ${wave} planted effect must survive noise`)
    auditor.estimates.push(estimated.effect.estimate)
    auditor.operations += 1
  }
  assert.ok(auditor.matrices.size >= 10, 'distinct seeds must produce distinct assignment matrices')
  assert.equal(auditor.operations, WAVES)
  assert.ok(!('providerCalls' in auditor), 'soak must never record provider invocations')
})

test('WHAT[EPI-017] soak_replay_and_hash_stay_stable_across_repeated_waves', async () => {
  const hashes = new Set()
  for (let wave = 0; wave < WAVES; wave += 1) {
    const events = waveEvents(wave, wave % 2 === 0 ? 'wording-a' : 'wording-b')
    const hashed = gecSurface.semanticHash({ events })
    assert.match(hashed.hash, /^[0-9a-f]{64}$/)
    const first = gecSurface.replay({ events })
    assert.equal(first.ok, true)
    assert.equal(first.stateHash, hashed.hash, `wave ${wave} replay must equal the canonical hash`)
    const second = gecSurface.replay({ events })
    assert.deepEqual(second.state, first.state, `wave ${wave} replay must be deterministic`)
    assert.equal(second.stateHash, first.stateHash)
    hashes.add(first.stateHash)
  }
  assert.equal(hashes.size, WAVES, 'distinct wave payloads must diverge to distinct hashes')
})

test('WHAT[EPI-016] soak_multi_plugin_waves_stay_deterministic_and_honestly_labeled', async () => {
  const recommendations = new Set()
  for (let wave = 0; wave < WAVES; wave += 1) {
    const input = waveInput(wave)
    const assigned = gecSurface.splitBallot({
      ...input,
      treatmentDetails: { 'wording-b': { wording: 'reversed text', polarity: -1, openFirst: false } },
    })
    assert.equal(assigned.ok, true)
    const next = xorshift(input.seed ^ 0x9e3779b9)
    const ballots = assigned.assignments.map((item) => {
      const prefersFirst = (item.treatment === 'wording-b') !== (next() < 0.3)
      return prefersFirst ? ['c1', 'c2', 'c3'] : ['c3', 'c2', 'c1']
    })
    const bordaInput = { candidates: [...CANDIDATES], ballots }
    const bordaFirst = gecSurface.borda(bordaInput)
    assert.equal(bordaFirst.ok, true)
    assert.deepEqual(gecSurface.borda(bordaInput), bordaFirst, `wave ${wave} borda must be deterministic`)
    assert.deepEqual(bordaFirst.guarantees, ['ballot-order-invariance', 'candidate-label-equivariance'])

    const pairs = [
      ['c1', 'c2'],
      ['c1', 'c3'],
      ['c2', 'c3'],
    ].map(([first, second]) => {
      let firstWins = 0
      let secondWins = 0
      for (const ballot of ballots) {
        if (ballot.indexOf(first) < ballot.indexOf(second)) firstWins += 1
        else secondWins += 1
      }
      return { a: first, b: second, winsA: firstWins, winsB: secondWins }
    })
    const btlInput = {
      candidates: [...CANDIDATES],
      comparisons: pairs,
      regularization: 0.5,
    }
    const btlFirst = gecSurface.bradleyTerry(btlInput)
    assert.equal(btlFirst.ok, true)
    assert.deepEqual(gecSurface.bradleyTerry(btlInput), btlFirst, `wave ${wave} btl must be deterministic`)
    for (const strength of Object.values(btlFirst.strengths)) assert.ok(Number.isFinite(strength))

    const total = Object.values(bordaFirst.meanScores).reduce((sum, value) => sum + value, 0)
    const forecast = Object.fromEntries(
      Object.entries(bordaFirst.meanScores).map(([candidate, value]) => [candidate, value / total]),
    )
    const predictionInput = {
      workId: `work_soak${String(wave).padStart(4, '0')}`,
      predicted: forecast,
      outcome: bordaFirst.ranking[0],
      epsilon: 0.01,
      committedBeforeStimulus: true,
      heldOut: false,
    }
    const scored = gecSurface.selfPrediction(predictionInput)
    assert.equal(scored.ok, true)
    assert.deepEqual(gecSurface.selfPrediction(predictionInput), scored, `wave ${wave} scoring must be deterministic`)
    assert.ok(Number.isFinite(scored.logScore))
    assert.ok(Number.isFinite(scored.brierScore))
    assert.equal(scored.calibrationUpdateAllowed, false)

    const stopInput = {
      testedFramings: ['wording-a', 'wording-b'],
      decisionPosterior: { approve: 0.68, reject: 0.32 },
      framingStability: { approve: [0.66, 0.7], reject: [0.3, 0.34] },
      minorityStable: true,
      checksSoFar: 2,
      alpha: 0.05,
      evidence: 30 + wave * 2,
    }
    const stopped = gecSurface.stopCertificate(stopInput)
    assert.equal(stopped.ok, true)
    assert.deepEqual(gecSurface.stopCertificate(stopInput), stopped, `wave ${wave} stop must be deterministic`)
    assert.match(stopped.certificate.sequentialError.method, /bonferroni/i)
    assert.ok(['stop', 'continue'].includes(stopped.recommendation))
    recommendations.add(stopped.recommendation)

    const patch = {
      kind: 'mcts-sample',
      root: 'root',
      children: { root: ['weak', 'strong'], weak: ['weak-terminal'], strong: ['strong-terminal'] },
      terminalReward: { 'weak-terminal': 0.1, 'strong-terminal': 0.95 },
      prior: { weak: 0.5, strong: 0.5 },
      iterations: 40,
      seed: input.seed,
      delta: 0.05,
    }
    const sampled = await gecSurface.refineCertificate({}, patch)
    assert.equal(sampled.ok, true)
    assert.deepEqual(await gecSurface.refineCertificate({}, patch), sampled, `wave ${wave} mcts must be deterministic`)
    assert.equal(sampled.coverage.scope, 'reference-only-no-finite-sample-coverage')
    assert.match(sampled.guarantee, /descriptive sample summary/i)
  }
  assert.deepEqual(
    [...recommendations].sort(),
    ['continue', 'stop'],
    'evidence crossing the bonferroni threshold mid-soak must flip the verdict',
  )
})

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

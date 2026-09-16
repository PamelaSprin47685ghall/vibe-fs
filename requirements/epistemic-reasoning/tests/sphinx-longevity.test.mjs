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

test('WHAT[EPI-029] soak_evidence_threshold_flips_once_and_voc_vetoes_every_wave', async () => {
  const stopInput = (evidence) => ({
    testedFramings: ['wording-a', 'wording-b'],
    decisionPosterior: { approve: 0.68, reject: 0.32 },
    framingStability: { approve: [0.66, 0.7], reject: [0.3, 0.34] },
    minorityStable: true,
    checksSoFar: 2,
    alpha: 0.05,
    evidence,
  })
  let seenStop = false
  let flips = 0
  for (let wave = 0; wave < WAVES; wave += 1) {
    const evidence = 30 + wave * 2
    const result = gecSurface.stopCertificate(stopInput(evidence))
    assert.equal(result.ok, true)
    assert.equal(result.certificate.sequentialError.method, 'bonferroni-fixed-split')
    assert.ok(result.certificate.sequentialError.cumulativeError <= 0.05 + 1e-12)
    assert.deepEqual(result.certificate.testedFamily, ['wording-a', 'wording-b'])
    assert.match(result.certificate.scope, /tested-framing-family/)
    assert.equal(result.decision.kind, 'decision-distribution')
    assert.ok(!('winner' in result.decision), `wave ${wave} must not collapse a stable minority to a single winner`)
    const minority = result.decision.minorityModes.find((mode) => mode.decision === 'reject')
    assert.ok(minority)
    assert.ok(Math.abs(minority.mass - 0.32) < 1e-12)
    if (result.recommendation === 'stop') {
      if (!seenStop) flips += 1
      seenStop = true
    } else {
      assert.equal(seenStop, false, `wave ${wave} flipped back to continue after stop at evidence ${evidence}`)
    }
    const vetoed = gecSurface.stopCertificate({
      ...stopInput(evidence),
      voc: { point: 0.01, upper: 0.5, threshold: 0.1 },
    })
    assert.equal(vetoed.ok, true)
    assert.equal(vetoed.recommendation, 'continue', `wave ${wave} VOC veto must hold even at evidence ${evidence}`)
    assert.ok(vetoed.voc.upper >= vetoed.voc.point)
  }
  assert.equal(flips, 1, 'the evidence sweep must cross the stopping threshold exactly once')
  assert.equal(seenStop, true)
  let previous = Infinity
  for (const checksSoFar of [1, 2, 5, 10]) {
    const checked = gecSurface.stopCertificate({
      testedFramings: ['wording-a', 'wording-b'],
      decisionPosterior: { approve: 0.68, reject: 0.32 },
      checksSoFar,
      alpha: 0.05,
    })
    assert.equal(checked.ok, true)
    assert.ok(checked.certificate.sequentialAlpha < previous, `checksSoFar=${checksSoFar} must tighten the sequential alpha`)
    assert.ok(checked.certificate.sequentialError.cumulativeError <= 0.05 + 1e-12)
    previous = checked.certificate.sequentialAlpha
  }
})

test('WHAT[EPI-016] soak_honesty_labels_stay_pinned_across_seeded_refiner_waves', async () => {
  const scopes = new Set()
  const codes = new Set()
  for (let wave = 0; wave < WAVES; wave += 1) {
    const input = waveInput(wave)
    const orders =
      wave % 2 === 0
        ? [['c1', 'c2', 'c3'], ['c3', 'c2', 'c1']]
        : [['c2', 'c3', 'c1'], ['c1', 'c3', 'c2']]
    const ballots = orders.map((order) => order.map((label) => [label]))
    const bordaFirst = gecSurface.borda({ candidates: [...CANDIDATES], ballots })
    assert.equal(bordaFirst.ok, true)
    assert.deepEqual(gecSurface.borda({ candidates: [...CANDIDATES], ballots }), bordaFirst)
    assert.deepEqual([...bordaFirst.guarantees].sort(), ['ballot-order-invariance', 'candidate-label-equivariance'])
    assert.ok(!bordaFirst.guarantees.includes('clone-independence'))
    assert.ok(!bordaFirst.guarantees.includes('iia'))
    assert.equal(bordaFirst.extension, 'complete-baseline')
    const reversed = gecSurface.borda({ candidates: [...CANDIDATES], ballots: [...ballots].reverse() })
    assert.deepEqual(reversed.scores, bordaFirst.scores, `wave ${wave} ballot order must not move borda scores`)

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
    const btl = gecSurface.bradleyTerry({ candidates: [...CANDIDATES], comparisons: pairs, regularization: 0.5 })
    assert.equal(btl.ok, true)
    assert.deepEqual(gecSurface.bradleyTerry({ candidates: [...CANDIDATES], comparisons: pairs, regularization: 0.5 }), btl)
    const gauge = Object.values(btl.strengths).reduce((sum, value) => sum + value, 0)
    assert.ok(Math.abs(gauge) < 1e-12, `wave ${wave} BTL strengths must hold the zero-sum gauge`)
    for (const strength of Object.values(btl.strengths)) assert.ok(Number.isFinite(strength))
    for (const error of Object.values(btl.uncertainty.standardErrors)) {
      assert.ok(Number.isFinite(error))
      assert.ok(error >= 0)
    }
    assert.ok(Math.abs(btl.diagnostics.regularization - 0.5) < 1e-12)
    assert.ok(Number.isFinite(btl.diagnostics.logLikelihood))
    assert.ok(btl.assumptions.includes('zero-sum-gauge'))

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
    assert.deepEqual(await gecSurface.refineCertificate({}, patch), sampled)
    assert.equal(sampled.coverage.scope, 'reference-only-no-finite-sample-coverage')
    assert.ok(!('level' in sampled.coverage), `wave ${wave} sample coverage must never claim a level`)
    assert.match(sampled.guarantee, /descriptive sample summary/i)
    assert.ok(!/singleton/i.test(sampled.guarantee))
    assert.ok(!/probabilistic-coverage/i.test(sampled.guarantee))
    for (const estimate of Object.values(sampled.estimates)) {
      assert.ok(Number.isFinite(estimate))
      assert.ok(estimate >= 0 && estimate <= 1)
    }
    scopes.add(sampled.coverage.scope)

    const masquerade = await gecSurface.refineCertificate({
      certificate: { nodeId: 'n01h455vb4pex5vsknk084sn02b', witnesses: ['ev-root'], derivations: ['ev-root'] },
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'inclusion' } },
    })
    assert.equal(masquerade.ok, false)
    assert.equal(masquerade.error.code, 'missing-coverage')
    codes.add(masquerade.error.code)
  }
  assert.equal(scopes.size, 1, 'the MCTS honesty scope must not drift across waves')
  assert.equal(codes.size, 1, 'the sample-slot rejection code must not drift across waves')
})

test('WHAT[EPI-010] soak_seeded_bayes_and_astar_entries_stay_deterministic_across_waves', async () => {
  const posteriors = new Set()
  const astarPatch = {
    kind: 'astar',
    start: 'S',
    goal: 'G',
    edges: [
      { from: 'S', to: 'A', cost: 2 },
      { from: 'S', to: 'B', cost: 2 },
      { from: 'A', to: 'C', cost: 2 },
      { from: 'B', to: 'C', cost: 1 },
      { from: 'C', to: 'G', cost: 2 },
    ],
    heuristic: { S: 4, A: 1, B: 3, C: 0, G: 0 },
  }
  for (let wave = 0; wave < WAVES; wave += 1) {
    const input = waveInput(wave)
    const next = xorshift((input.seed ^ 0x51ab) >>> 0)
    const factors = [1, 2, 3].map((index) => ({
      dependencyKey: `soak-dep-${String(wave).padStart(2, '0')}-${index}`,
      likelihoods: { up: 0.2 + next() * 0.6, down: 0.2 + next() * 0.6 },
    }))
    const bayesPatch = { kind: 'bayes-exact', factors }
    const bayesInput = { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } }
    const bayesFirst = await gecSurface.refineCertificate(bayesInput, bayesPatch)
    assert.equal(bayesFirst.ok, true)
    assert.deepEqual(await gecSurface.refineCertificate(bayesInput, bayesPatch), bayesFirst, `wave ${wave} bayes must be deterministic`)
    assert.ok(Math.abs(bayesFirst.posterior.up + bayesFirst.posterior.down - 1) < 1e-12)
    for (const value of Object.values(bayesFirst.posterior)) {
      assert.ok(Number.isFinite(value))
      assert.ok(value >= 0 && value <= 1)
    }
    posteriors.add(`${bayesFirst.posterior.up.toFixed(9)}/${bayesFirst.posterior.down.toFixed(9)}`)

    const astarFirst = await gecSurface.refineCertificate({}, astarPatch)
    assert.equal(astarFirst.ok, true)
    assert.deepEqual(await gecSurface.refineCertificate({}, astarPatch), astarFirst, `wave ${wave} astar must be deterministic`)
    assert.deepEqual(astarFirst.path, ['S', 'B', 'C', 'G'])
    assert.ok(Number.isFinite(astarFirst.lowerBound))
    assert.ok(Number.isFinite(astarFirst.upperBound))
    assert.ok(astarFirst.lowerBound <= astarFirst.cost + 1e-12)
    assert.ok(astarFirst.cost <= astarFirst.upperBound + 1e-12)

    const targets = [
      { id: 't-zeta', dependencies: [], conflictKeys: [], cost: { compute: 1, budget: 1 }, loss: { currency: 'shared', value: 0.3 }, commonCurrency: 'shared' },
      { id: 't-mid', dependencies: [], conflictKeys: [], cost: { compute: 1, budget: 1 }, loss: { currency: 'shared', value: 0.2 }, commonCurrency: 'shared' },
      { id: 't-alpha', dependencies: [], conflictKeys: [], cost: { compute: 1, budget: 1 }, loss: { currency: 'shared', value: 0.1 }, commonCurrency: 'shared' },
    ]
    const budgeted = { compute: 10, budget: 10 }
    const first = gecSurface.schedule({ targets, budget: budgeted, completed: [] })
    assert.equal(first.ok, true)
    assert.deepEqual(
      gecSurface.schedule({ targets: [...targets].reverse(), budget: budgeted, completed: [] }),
      first,
      `wave ${wave} schedule must be invariant to input permutation`,
    )
    assert.deepEqual(first.order, ['t-alpha', 't-mid', 't-zeta'])
    assert.ok(!('summedDelta' in first), `wave ${wave} schedule must expose an order, never an additive delta sum`)
  }
  assert.ok(posteriors.size >= 10, 'distinct wave seeds must drive distinct bayes posteriors')
})

test('WHAT[EPI-025] soak_scored_forecasts_keep_seal_and_simplex_gates_across_waves', async () => {
  for (let wave = 0; wave < WAVES; wave += 1) {
    const input = waveInput(wave)
    const next = xorshift((input.seed ^ 0x9e3779b9) >>> 0)
    const ballots = Array.from({ length: 6 }, () =>
      next() < 0.5 ? [['c1'], ['c2'], ['c3']] : [['c3'], ['c2'], ['c1']],
    )
    const borda = gecSurface.borda({ candidates: [...CANDIDATES], ballots })
    assert.equal(borda.ok, true)
    const total = Object.values(borda.meanScores).reduce((sum, value) => sum + value, 0)
    const forecast = Object.fromEntries(
      Object.entries(borda.meanScores).map(([candidate, value]) => [candidate, value / total]),
    )
    const mass = Object.values(forecast).reduce((sum, value) => sum + value, 0)
    assert.ok(Math.abs(mass - 1) < 1e-12, `wave ${wave} borda-derived forecast must be a simplex`)
    const workId = `work_soak${String(wave).padStart(4, '0')}`
    const sealedInput = {
      workId,
      predicted: forecast,
      outcome: borda.ranking[0],
      epsilon: 0.01,
      committedBeforeStimulus: true,
      heldOut: false,
    }
    const sealed = gecSurface.selfPrediction(sealedInput)
    assert.equal(sealed.ok, true)
    assert.deepEqual(gecSurface.selfPrediction(sealedInput), sealed, `wave ${wave} sealed scoring must be deterministic`)
    assert.ok(Number.isFinite(sealed.logScore))
    assert.ok(Number.isFinite(sealed.brierScore))
    assert.ok('calibration' in sealed && 'sharpness' in sealed)
    assert.ok(!('answer' in sealed), `wave ${wave} raw score must never render the answer`)
    assert.equal(sealed.calibrationUpdateAllowed, false)
    const heldOut = gecSurface.selfPrediction({ ...sealedInput, heldOut: true })
    assert.equal(heldOut.ok, true)
    assert.equal(heldOut.calibrationUpdateAllowed, true, `wave ${wave} held-out target must gate the calibration update`)
    const unsealed = gecSurface.selfPrediction({ ...sealedInput, committedBeforeStimulus: false })
    assert.equal(unsealed.ok, false)
    assert.match(unsealed.error, /commit|seal|reveal/i)
    const zeroed = { ...forecast, [borda.ranking[0]]: 0 }
    const renorm = Object.values(zeroed).reduce((sum, value) => sum + value, 0)
    for (const candidate of Object.keys(zeroed)) zeroed[candidate] /= renorm
    const floored = gecSurface.selfPrediction({
      workId,
      predicted: zeroed,
      outcome: borda.ranking[0],
      epsilon: 0.01,
      committedBeforeStimulus: true,
      heldOut: false,
    })
    assert.equal(floored.ok, true)
    assert.ok(Math.abs(floored.logScore - Math.log(0.01)) < 1e-12, `wave ${wave} zero-probability outcome must hit the epsilon floor`)
    const unnormalized = gecSurface.selfPrediction({
      ...sealedInput,
      predicted: Object.fromEntries(Object.entries(forecast).map(([candidate, value]) => [candidate, value * 2])),
    })
    assert.equal(unnormalized.ok, false)
    assert.match(unnormalized.error, /simplex/i)
  }
})

test('WHAT[EPI-019] soak_pure_spine_gates_conflicts_and_refold_recovers_without_io', async () => {
  for (let wave = 0; wave < WAVES; wave += 1) {
    const tag = String(wave).padStart(4, '0')
    const inquiryId = `iq_soakpure${tag}`
    const genesis = gecSurface.sphinxCurrent({ envelopes: [] })
    assert.equal(genesis.ok, true)
    assert.equal(genesis.current.revision, 0)
    const bound = gecSurface.encodeSphinxEnvelope({
      inquiryId,
      revision: 0,
      kind: 'plugin-set-bound',
      parents: [],
      payload: { plugins: ['sphinx-legacy'] },
    })
    assert.equal(bound.ok, true)
    const gate0 = gecSurface.checkAppend({ current: genesis.current, envelope: bound.envelope, expectedRevision: 0 })
    assert.equal(gate0.ok, true)
    assert.equal(gate0.duplicate, false)
    assert.equal(gate0.revision, 1)
    const afterBound = gecSurface.sphinxCurrent({ envelopes: [bound.envelope] })
    assert.equal(afterBound.current.revision, 1)
    const observed = gecSurface.encodeSphinxEnvelope({
      inquiryId,
      revision: 1,
      kind: 'observation-accepted',
      parents: [bound.envelope.id],
      payload: { workId: `work_soakpure${tag}`, attempt: 1, observation: `first-${tag}` },
    })
    assert.equal(observed.ok, true)
    const gate1 = gecSurface.checkAppend({ current: afterBound.current, envelope: observed.envelope, expectedRevision: 1 })
    assert.equal(gate1.ok, true)
    assert.equal(gate1.revision, 2)
    const spine = [bound.envelope, observed.envelope]
    const folded = gecSurface.sphinxCurrent({ envelopes: spine })
    assert.equal(folded.current.revision, 2)
    assert.equal(folded.eventHead, observed.envelope.id)
    const redelivered = gecSurface.checkAppend({ current: folded.current, envelope: observed.envelope, expectedRevision: 2 })
    assert.equal(redelivered.ok, true)
    assert.equal(redelivered.duplicate, true, `wave ${wave} identical redelivery must report a duplicate`)
    assert.equal(redelivered.revision, 2)
    const conflicting = gecSurface.encodeSphinxEnvelope({
      inquiryId,
      revision: 1,
      kind: 'observation-accepted',
      parents: [bound.envelope.id],
      payload: { workId: `work_soakpure${tag}`, attempt: 1, observation: `contradictory-rewrite-${tag}` },
    })
    assert.equal(conflicting.ok, true)
    const conflict = gecSurface.checkAppend({ current: folded.current, envelope: conflicting.envelope, expectedRevision: 2 })
    assert.equal(conflict.ok, false)
    assert.match(conflict.error.code, /DUPLICATE_CONFLICT/)
    const stale = gecSurface.checkAppend({ current: folded.current, envelope: conflicting.envelope, expectedRevision: 0 })
    assert.equal(stale.ok, false)
    assert.match(stale.error.code, /REVISION_CONFLICT/)
    const refolded = gecSurface.sphinxCurrent({ envelopes: spine })
    assert.deepEqual(refolded.current, folded.current, `wave ${wave} dropping Current must refold identically`)
    assert.equal(refolded.eventHead, folded.eventHead)
    assert.equal(refolded.semanticHash, folded.semanticHash)

    const events = waveEvents(wave, 'wording-a')
    const hashed = gecSurface.semanticHash({ events })
    const replayed = gecSurface.replay({ events })
    assert.equal(replayed.ok, true)
    const bundle = gecSurface.exportFromEvents({ events })
    assert.equal(bundle.error, undefined)
    assert.equal(replayed.stateHash, hashed.hash, `wave ${wave} replay must equal the canonical hash`)
    assert.equal(bundle.semanticHash, hashed.hash, `wave ${wave} export must agree with replay on one hash`)
  }
})

test('WHAT[EPI-021] soak_replay_rejects_lifecycle_violations_with_stable_codes_across_waves', async () => {
  const lock = [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }]
  const fields = ['leaseExpiresAt', 'heartbeatTimeout', 'wallClock', 'expiresAt', 'timeoutMs']
  for (let wave = 0; wave < WAVES; wave += 1) {
    const tag = String(wave).padStart(4, '0')
    const inquiry = `iq_soaklife${tag}`
    const branch = `branch_soaklife${tag}`
    const workId = `work_soaklife${tag}`
    const created = {
      type: 'InquiryCreated',
      inquiry,
      revision: 0,
      parent: 'none',
      question: 'lifecycle soak probe',
      pluginLock: lock,
      budget: { compute: 10, budget: 10 },
      root: {
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { wave } },
        adapter: 'question-to-root:v1',
      },
    }
    const planned = {
      type: 'WorkPlanned',
      inquiry,
      revision: 1,
      parent: 'ev0',
      work: { id: workId, branch, attempt: 1 },
    }
    const ref = (attempt = 1, extra = {}) => ({ id: workId, branch, attempt, ...extra })
    const transition = (revision, parent, work, from, to, extra = {}) => ({
      type: 'WorkTransitioned',
      inquiry,
      revision,
      parent,
      work,
      from,
      to,
      ...extra,
    })
    const legalPrefix = [
      created,
      planned,
      transition(2, 'ev1', ref(), 'Planned', 'Ready', {}),
      transition(3, 'ev2', ref(1, { fence: `fence-${tag}` }), 'Ready', 'Leased', {}),
      transition(4, 'ev3', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Leased', 'Executing', {}),
    ]
    assert.equal(gecSurface.replay({ events: legalPrefix }).ok, true, `wave ${wave} legal prefix must replay`)
    const succeeded = [
      ...legalPrefix,
      transition(5, 'ev4', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Executing', 'Succeeded', { observation: `obs-${tag}` }),
    ]
    assert.equal(gecSurface.replay({ events: succeeded }).ok, true, `wave ${wave} run to Succeeded must replay`)
    const resurrection = gecSurface.replay({
      events: [...succeeded, transition(6, 'ev5', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Succeeded', 'Executing', {})],
    })
    assert.equal(resurrection.ok, false)
    assert.equal(resurrection.error.code, 'illegal-transition', `wave ${wave} terminal must never return to executing`)
    const secondObservation = gecSurface.replay({
      events: [
        ...succeeded,
        transition(6, 'ev5', ref(1, { fence: `fence-${tag}`, session: `sess-${tag}` }), 'Succeeded', 'Succeeded', { observation: `obs-rewrite-${tag}` }),
      ],
    })
    assert.equal(secondObservation.ok, false)
    assert.equal(secondObservation.error.code, 'duplicate-observation', `wave ${wave} one attempt must accept one observation`)
    const timed = gecSurface.replay({
      events: [created, planned, transition(2, 'ev1', ref(), 'Planned', 'Ready', { [fields[wave % fields.length]]: 1234567890 })],
    })
    assert.equal(timed.ok, false)
    assert.equal(timed.error.code, 'wall-clock-field', `wave ${wave} field ${fields[wave % fields.length]} must never drive the lifecycle`)
  }
})

import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const nodeId = 'n01h455vb4pex5vsknk084sn02b';
function baseCertificate() {
  return { nodeId, witnesses: ['ev-root'], derivations: ['ev-root'] };
}
function patchesInCanonicalOrder() {
  return [
    { slot: 'exact', value: { mean: 0.7 }, guarantee: { kind: 'inclusion' }, witnesses: ['ev-exact'], derivations: ['ev-exact'] },
    { slot: 'bound', lower: 0.6, upper: 0.8, guarantee: { kind: 'inclusion' }, witnesses: ['ev-bound'], derivations: ['ev-bound'] },
    {
      slot: 'sample',
      summary: { mean: 0.71, n: 2000 },
      guarantee: { kind: 'coverage', level: 0.95, assumptions: ['iid-draws'], error: 0.02 },
      witnesses: ['ev-sample'],
      derivations: ['ev-sample'],
    },
    { slot: 'ordinal', constraints: [{ before: 'a', after: 'b' }], guarantee: { kind: 'ordinal' }, witnesses: ['ev-ord'], derivations: ['ev-ord'] },
    {
      slot: 'latent',
      posterior: { family: 'dirichlet', params: [7, 3] },
      guarantee: { kind: 'coverage', level: 0.9, assumptions: ['correct-spec'], error: 0.05 },
      witnesses: ['ev-latent'],
      derivations: ['ev-latent'],
    },
    { slot: 'residual', value: 0.04, witnesses: ['ev-res'], derivations: ['ev-res'] },
  ];
}

test('WHAT[epistemic-reasoning-016] single_certificate_holds_exact_bound_sample_ordinal_latent_together_or_solver_mode_splits_state', async () => {
  const surface = gecSurface;
  let certificate = baseCertificate();
  for (const patch of patchesInCanonicalOrder()) {
    const result = await surface.refineCertificate({ certificate, patch });
    assert.equal(result.ok, true, `slot ${patch.slot} must apply without evicting siblings`);
    certificate = result.certificate;
  }
  assert.equal(certificate.nodeId, nodeId);
  assert.ok(certificate.exact, 'exact slot must survive alongside other slots');
  assert.ok(certificate.lowerEnvelope !== undefined || certificate.bound !== undefined || certificate.lower !== undefined, 'lower bound slot must survive');
  assert.ok(certificate.upperEnvelope !== undefined || certificate.bound !== undefined || certificate.upper !== undefined, 'upper bound slot must survive');
  assert.ok(certificate.sampleSummary || certificate.sample, 'sample slot must survive alongside exact and bound');
  assert.ok(certificate.ordinalConstraints || certificate.ordinal, 'ordinal slot must survive');
  assert.ok(certificate.latentPosterior || certificate.latent, 'latent slot must survive');
  assert.ok(certificate.residual !== undefined, 'residual slot must survive');
  assert.ok(Array.isArray(certificate.witnesses) && certificate.witnesses.length >= 6, 'witnesses must accumulate across slots');
  assert.ok(Array.isArray(certificate.derivations) && certificate.derivations.length >= 6, 'derivations must accumulate across slots');

  const reversed = patchesInCanonicalOrder().slice().reverse();
  let other = baseCertificate();
  for (const patch of reversed) {
    const result = await surface.refineCertificate({ certificate: other, patch });
    assert.equal(result.ok, true, `slot ${patch.slot} must apply in reverse order too`);
    other = result.certificate;
  }
  assert.deepEqual(
    { exact: other.exact, sample: other.sampleSummary || other.sample, residual: other.residual },
    { exact: certificate.exact, sample: certificate.sampleSummary || certificate.sample, residual: certificate.residual },
    'slot values must be order independent while witnesses accumulate',
  );
});
test('WHAT[epistemic-reasoning-016] sample_slot_requires_coverage_assumptions_or_point_estimate_masquerades_as_bound', async () => {
  const surface = gecSurface;
  const invalidPatches = [
    {
      name: 'lower above upper',
      patch: { slot: 'bound', lower: 0.9, upper: 0.1, guarantee: { kind: 'inclusion' } },
      code: 'invalid-bound',
    },
    {
      name: 'sample claims deterministic inclusion',
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'inclusion' } },
      code: 'missing-coverage',
    },
    {
      name: 'sample without level',
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'coverage', assumptions: ['iid-draws'], error: 0.02 } },
      code: 'missing-coverage',
    },
    {
      name: 'sample without assumptions',
      patch: { slot: 'sample', summary: { mean: 0.5, n: 100 }, guarantee: { kind: 'coverage', level: 0.95, error: 0.02 } },
      code: 'missing-coverage',
    },
    {
      name: 'exact without deterministic guarantee',
      patch: { slot: 'exact', value: { mean: 0.5 } },
      code: 'missing-guarantee',
    },
    {
      name: 'latent without coverage',
      patch: { slot: 'latent', posterior: { family: 'dirichlet', params: [1, 1] }, guarantee: { kind: 'inclusion' } },
      code: 'missing-coverage',
    },
  ];
  for (const { name, patch, code } of invalidPatches) {
    const result = await surface.refineCertificate({ certificate: baseCertificate(), patch });
    assert.equal(result.ok, false, `${name} must fail with a typed error`);
    assert.equal(result.error.code, code, `${name} must report ${code}`);
  }

  const validSample = await surface.refineCertificate({
    certificate: baseCertificate(),
    patch: {
      slot: 'sample',
      summary: { mean: 0.5, n: 500 },
      guarantee: { kind: 'coverage', level: 0.95, assumptions: ['iid-draws'], error: 0.03 },
    },
  });
  assert.equal(validSample.ok, true, 'well-formed sample with coverage must be accepted');
});
test('WHAT[epistemic-reasoning-016] exact_bound_declare_inclusion_while_sample_declares_coverage_or_value_preorder_collapses', async () => {
  const surface = gecSurface;
  const start = baseCertificate();
  const withExact = await surface.refineCertificate({
    certificate: start,
    patch: { slot: 'exact', value: { mean: 0.62 }, guarantee: { kind: 'inclusion' }, witnesses: ['ev-a'], derivations: ['ev-a'] },
  });
  assert.equal(withExact.ok, true);
  const witnessOnly = await surface.refineCertificate({
    certificate: withExact.certificate,
    patch: { slot: 'witness', witnesses: ['ev-extra'], derivations: ['ev-extra'] },
  });
  const afterWitness = witnessOnly.ok ? witnessOnly.certificate : withExact.certificate;
  assert.deepEqual(afterWitness.exact, withExact.certificate.exact, 'witness growth alone must not perturb declared value slots');
  assert.ok(
    (afterWitness.witnesses || []).length > (withExact.certificate.witnesses || []).length ||
      witnessOnly.ok === false,
    'witness growth must accumulate or be an explicit witness slot, never silently rewrite values',
  );

  const sampleAsInclusion = await surface.refineCertificate({
    certificate: withExact.certificate,
    patch: { slot: 'sample', summary: { mean: 0.62, n: 800 }, guarantee: { kind: 'inclusion' } },
  });
  assert.equal(sampleAsInclusion.ok, false, 'sample slot must never accept a deterministic inclusion guarantee');
  assert.equal(sampleAsInclusion.error.code, 'missing-coverage');

  const boundAsCoverage = await surface.refineCertificate({
    certificate: withExact.certificate,
    patch: { slot: 'bound', lower: 0.55, upper: 0.7, guarantee: { kind: 'inclusion' } },
  });
  assert.equal(boundAsCoverage.ok, true, 'bound slot must accept deterministic inclusion');
  assert.deepEqual(boundAsCoverage.certificate.exact, withExact.certificate.exact, 'adding a bound must not evict the exact slot');
});
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

test('WHAT[epistemic-reasoning-016] soak_multi_plugin_waves_stay_deterministic_and_honestly_labeled', async () => {
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
test('WHAT[epistemic-reasoning-016] soak_honesty_labels_stay_pinned_across_seeded_refiner_waves', async () => {
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
}

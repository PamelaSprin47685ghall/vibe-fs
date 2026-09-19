import test from 'node:test'

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const tinyFactors = Array.from({ length: 40 }, (_, index) => ({
  dependencyKey: `tiny-dep-${String(index + 1).padStart(2, '0')}`,
  likelihoods: { a: 1e-10, b: 2e-10 },
}))

test('WHAT[epistemic-reasoning-010] log-space-bayes-survives-likelihood-product-underflow', async () => {
  const result = await gecSurface.refineCertificate(
    { hypotheses: ['a', 'b'], priors: { a: 0.5, b: 0.5 } },
    { kind: 'bayes-exact', factors: tinyFactors },
  )
  assert.equal(result.ok, true)
  assert.ok(Number.isFinite(result.posterior.a))
  assert.ok(Number.isFinite(result.posterior.b))
  assert.ok(result.posterior.a > 0)
  assert.ok(result.posterior.b > 0)
  assert.ok(Math.abs(result.posterior.a + result.posterior.b - 1) < 1e-12)
  const expectedLogOdds = 40 * Math.log(0.5)
  const actualLogOdds = Math.log(result.posterior.a / result.posterior.b)
  assert.ok(Math.abs(actualLogOdds - expectedLogOdds) < 1e-9)
})
test('WHAT[epistemic-reasoning-010] exact-bayes-matches-brute-force-normalized-product-when-representable', async () => {
  const result = await gecSurface.refineCertificate(
    { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } },
    {
      kind: 'bayes-exact',
      factors: [
        { dependencyKey: 'dep-one', likelihoods: { up: 0.8, down: 0.2 } },
        { dependencyKey: 'dep-two', likelihoods: { up: 0.6, down: 0.4 } },
      ],
    },
  )
  assert.equal(result.ok, true)
  assert.ok(Math.abs(result.posterior.up - 0.72) < 1e-12)
  assert.ok(Math.abs(result.posterior.down - 0.28) < 1e-12)
})
test('WHAT[epistemic-reasoning-010] astar-reports-global-frontier-bound-incumbent-and-reopens-better-g', async () => {
  const result = await gecSurface.refineCertificate(
    {},
    {
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
    },
  )
  assert.equal(result.ok, true)
  assert.deepEqual(result.path, ['S', 'B', 'C', 'G'])
  assert.ok(Math.abs(result.cost - 5) < 1e-12)
  assert.ok(result.expanded.filter((node) => node === 'C').length >= 2)
  assert.ok(Number.isFinite(result.lowerBound))
  assert.ok(Number.isFinite(result.upperBound))
  assert.ok(result.lowerBound <= result.cost + 1e-12)
  assert.ok(result.cost <= result.upperBound + 1e-12)
  assert.ok(Math.abs(result.lowerBound - 5) < 1e-12)
  assert.ok(Math.abs(result.upperBound - 5) < 1e-12)
})
test('WHAT[epistemic-reasoning-010] astar-rejects-nonzero-goal-heuristic-and-exposes-admissibility-assumption', async () => {
  const rejected = await gecSurface.refineCertificate(
    {},
    {
      kind: 'astar',
      start: 'S',
      goal: 'G',
      edges: [{ from: 'S', to: 'G', cost: 2 }],
      heuristic: { S: 1, G: 5 },
    },
  )
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error.code, 'non-zero-goal-heuristic')

  const admitted = await gecSurface.refineCertificate(
    {},
    {
      kind: 'astar',
      start: 'S',
      goal: 'G',
      edges: [{ from: 'S', to: 'G', cost: 2 }],
      heuristic: { S: 1 },
    },
  )
  assert.equal(admitted.ok, true)
  assert.deepEqual(admitted.path, ['S', 'G'])
  assert.ok(admitted.assumptions.includes('admissible-heuristic-assumed-unverified'))
})
test('WHAT[epistemic-reasoning-010] exact-bayes-reports-canonical-factors-and-ignores-invalid-shadows', async () => {
  const result = await gecSurface.refineCertificate(
    { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } },
    {
      kind: 'bayes-exact',
      factors: [
        { dependencyKey: 'dep-one', likelihoods: { up: 0.8, down: 0.2 } },
        { dependencyKey: 'dep-two', likelihoods: { up: 0.6, down: 0.4 } },
      ],
    },
  )
  assert.equal(result.ok, true)
  assert.deepEqual(result.usedFactors, ['dep-one', 'dep-two'])
  assert.ok(Number.isFinite(result.logPartition))
  assert.ok(Math.abs(result.posterior.up - 0.72) < 1e-12)

  const shadowed = await gecSurface.refineCertificate(
    { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } },
    {
      kind: 'bayes-exact',
      factors: [
        { dependencyKey: 'dep-one', likelihoods: { up: 0.8, down: 0.2 } },
        { dependencyKey: 'dep-one', likelihoods: { up: 0.9 } },
      ],
    },
  )
  assert.equal(shadowed.ok, true)
  assert.deepEqual(shadowed.usedFactors, ['dep-one'])
  assert.ok(Math.abs(shadowed.posterior.up - (0.8 * 0.3) / (0.8 * 0.3 + 0.2 * 0.7)) < 1e-12)
})
test('WHAT[epistemic-reasoning-010] seeded-mcts-returns-descriptive-sample-summary-not-deterministic-truth', async () => {
  const patch = {
    kind: 'mcts-sample',
    root: 'root',
    children: {
      root: ['weak', 'strong'],
      weak: ['weak-terminal'],
      strong: ['strong-terminal'],
    },
    terminalReward: { 'weak-terminal': 0.1, 'strong-terminal': 0.95 },
    prior: { weak: 0.5, strong: 0.5 },
    iterations: 40,
    seed: 7,
    delta: 0.05,
  }
  const first = await gecSurface.refineCertificate({}, patch)
  const second = await gecSurface.refineCertificate({}, patch)
  assert.equal(first.ok, true)
  assert.equal(second.ok, true)
  assert.deepEqual(second, first)
  assert.ok(Math.abs(first.coverage.delta - 0.05) < 1e-12)
  assert.equal(first.coverage.scope, 'reference-only-no-finite-sample-coverage')
  assert.ok(!('level' in first.coverage))
  for (const key of Object.keys(first.estimates)) {
    assert.ok(Number.isFinite(first.estimates[key]))
    assert.ok(first.estimates[key] >= 0 && first.estimates[key] <= 1)
  }
  assert.match(first.guarantee, /descriptive sample summary/i)
  const guaranteeText = `${first.guarantee} ${Object.keys(first)}`
  assert.ok(!/deterministic truth/i.test(guaranteeText))
  assert.ok(!/singleton/i.test(guaranteeText))
  assert.ok(!/probabilistic-coverage/i.test(guaranteeText))
})
test('WHAT[epistemic-reasoning-010] mcts-sample-accepts-negative-rewards-and-ignores-legacy-prior', async () => {
  const patch = {
    kind: 'mcts-sample',
    root: 'root',
    children: {
      root: ['loss', 'gain'],
      loss: ['loss-terminal'],
      gain: ['gain-terminal'],
    },
    terminalReward: { 'loss-terminal': -2.0, 'gain-terminal': 4.0 },
    iterations: 40,
    seed: 11,
    delta: 0.05,
  }
  const first = await gecSurface.refineCertificate({}, patch)
  assert.equal(first.ok, true)
  assert.equal(first.coverage.rewardLo, -2.0)
  assert.equal(first.coverage.rewardHi, 4.0)
  for (const key of Object.keys(first.estimates)) {
    assert.ok(Number.isFinite(first.estimates[key]))
  }
  assert.match(first.guarantee, /prior.*ignored|ignored.*prior/i)

  const withPrior = await gecSurface.refineCertificate({}, { ...patch, prior: { loss: 0.9, gain: 0.1 } })
  assert.equal(withPrior.ok, true)
  assert.deepEqual(withPrior.estimates, first.estimates)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mapOfEntries, run, uct } = await import("./support.mjs");

const map = (entries) => mapOfEntries(entries)
const model = (root, children, terminalReward, prior) => ({ root, children, terminalReward, prior })

test('WHAT[epistemic-reasoning-010] mcts_selection_expansion_rollout_backup_prefers_high_value_branch', () => {
  const result = run(
    40,
    model(
      'root',
      map([
        ['root', ['weak', 'strong']],
        ['weak', ['weak-terminal']],
        ['strong', ['strong-terminal']],
      ]),
      map([
        ['weak-terminal', 0.1],
        ['strong-terminal', 0.95],
      ]),
      map([
        ['weak', 0.5],
        ['strong', 0.5],
      ]),
    ),
  )

  assert.equal(result.bestAction, 'strong')
  assert.equal(result.iterations, 40)
})
test('WHAT[epistemic-reasoning-010] graph_mcts_shares_transposition_statistics_by_semantic_node_key', () => {
  const result = run(
    20,
    model(
      'root',
      map([
        ['root', ['a', 'b']],
        ['a', ['shared']],
        ['b', ['shared']],
      ]),
      map([['shared', 0.8]]),
      map([
        ['a', 0.5],
        ['b', 0.5],
        ['shared', 0.8],
      ]),
    ),
  )

  const shared = result.nodes.find((node) => node.semanticKey === 'shared')
  assert.ok(shared.visits > 1)
  assert.ok(result.nodes.length <= 4)
})
test('WHAT[epistemic-reasoning-010] uct_for_unvisited_node_is_infinite', () => {
  const node = { semanticKey: 'new', visits: 0, valueSum: 0, prior: 0.5 }
  assert.equal(uct(10, Math.SQRT2, node), Number.POSITIVE_INFINITY)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mapOfEntries, solveGraph } = await import("./support.mjs");

const map = (entries) => mapOfEntries(entries)
const edges = (rows) => rows.map(([from, to, cost]) => ({ from, to, cost }))
const problem = (start, goal, graphEdges, heuristic) => ({ start, goal, edges: graphEdges, heuristic })

test('WHAT[epistemic-reasoning-010] graph_astar_degenerates_to_standard_g_plus_h_shortest_path', () => {
  const solved = solveGraph(
    problem(
      'S',
      'G',
      edges([
        ['S', 'A', 1],
        ['S', 'B', 4],
        ['A', 'C', 1],
        ['C', 'G', 1],
        ['B', 'G', 1],
      ]),
      map([
        ['S', 3],
        ['A', 2],
        ['B', 1],
        ['C', 1],
        ['G', 0],
      ]),
    ),
  )
  assert.equal(solved.cost, 3)
  assert.deepEqual(solved.path, ['S', 'A', 'C', 'G'])
})
test('WHAT[epistemic-reasoning-010] graph_astar_reopens_closed_node_when_better_g_is_discovered', () => {
  const solved = solveGraph(
    problem(
      'S',
      'G',
      edges([
        ['S', 'A', 2],
        ['S', 'B', 2],
        ['A', 'C', 2],
        ['B', 'C', 1],
        ['C', 'G', 2],
      ]),
      map([
        ['S', 4],
        ['A', 1],
        ['B', 3],
        ['C', 0],
        ['G', 0],
      ]),
    ),
  )

  assert.equal(solved.cost, 5)
  assert.deepEqual(solved.path, ['S', 'B', 'C', 'G'])
  assert.ok(solved.expanded.filter((node) => node === 'C').length >= 2)
})
test('WHAT[epistemic-reasoning-010] graph_astar_rejects_negative_cost_graph', () => {
  const solved = solveGraph(problem('S', 'G', edges([['S', 'G', -1]]), map([['S', 0], ['G', 0]])))
  assert.equal(solved, null)
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

test('WHAT[epistemic-reasoning-010] soak_seeded_bayes_and_astar_entries_stay_deterministic_across_waves', async () => {
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
}

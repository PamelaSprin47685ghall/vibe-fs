import test from 'node:test'

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

test('WHAT[epistemic-reasoning-029] soak_evidence_threshold_flips_once_and_voc_vetoes_every_wave', async () => {
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
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const posterior = { approve: 0.68, reject: 0.32 }

test('WHAT[epistemic-reasoning-029] certificate-bounds-guarantee-to-tested-framing-family-only', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    checksSoFar: 1,
    alpha: 0.05,
  })
  assert.equal(result.ok, true)
  assert.deepEqual(result.certificate.testedFamily, ['neutral', 'reverse-wording'])
  const scopeText = `${result.certificate.scope} ${result.certificate.guarantee}`
  assert.match(result.certificate.scope, /tested/i)
  assert.ok(!/universal/i.test(scopeText))
  assert.ok(!/all wording/i.test(scopeText))
  assert.ok(!result.certificate.testedFamily.includes('leading-question'))
})
test('WHAT[epistemic-reasoning-029] sequential-error-control-tightens-with-repeated-checks', async () => {
  const base = {
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    alpha: 0.05,
  }
  const early = await gecSurface.stopCertificate({ ...base, checksSoFar: 1 })
  const late = await gecSurface.stopCertificate({ ...base, checksSoFar: 5 })
  assert.equal(early.ok, true)
  assert.equal(late.ok, true)
  assert.ok(late.certificate.sequentialAlpha < early.certificate.sequentialAlpha)
  assert.ok(early.certificate.sequentialError.cumulativeError <= 0.05 + 1e-12)
  assert.ok(late.certificate.sequentialError.cumulativeError <= 0.05 + 1e-12)
  assert.match(late.certificate.sequentialError.method, /bonferroni/i)
})
test('WHAT[epistemic-reasoning-029] stable-minority-mode-returns-decision-distribution-not-single-winner', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    framingStability: { approve: [0.66, 0.7], reject: [0.3, 0.34] },
    minorityStable: true,
    checksSoFar: 2,
    alpha: 0.05,
  })
  assert.equal(result.ok, true)
  assert.ok(result.decision.kind !== 'single-winner')
  assert.ok(!('winner' in result.decision))
  assert.equal(result.decision.modes.length, 2)
  const minority = result.decision.modes.find((mode) => mode.decision === 'reject')
  assert.ok(minority)
  assert.ok(Math.abs(minority.mass - 0.32) < 1e-12)
  assert.deepEqual(result.decision.minorityModes, [{ decision: 'reject', mass: 0.32 }])
})
test('WHAT[epistemic-reasoning-029] caller-supplied-coverage-and-minority-thresholds-bind', async () => {
  const base = {
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    checksSoFar: 1,
    alpha: 0.05,
  }
  const custom = await gecSurface.stopCertificate({ ...base, requiredCoverage: 0.9, minorityThreshold: 0.4 })
  assert.equal(custom.ok, true)
  assert.ok(Math.abs(custom.certificate.requiredCoverage - 0.9) < 1e-12)
  assert.ok(Math.abs(custom.certificate.minorityThreshold - 0.4) < 1e-12)

  const fallback = await gecSurface.stopCertificate(base)
  assert.equal(fallback.ok, true)
  assert.ok(Math.abs(fallback.certificate.requiredCoverage - 0.5) < 1e-12)
  assert.ok(Math.abs(fallback.certificate.minorityThreshold - 0.05) < 1e-12)
})
test('WHAT[epistemic-reasoning-029] caller-evidence-fires-stop-when-all-checks-pass', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    framingStability: { approve: [0.66, 0.7], reject: [0.3, 0.34] },
    checksSoFar: 1,
    alpha: 0.05,
    evidence: 25,
  })
  assert.equal(result.ok, true)
  assert.deepEqual(
    result.certificate.testedFamily,
    ['neutral', 'reverse-wording'],
  )
  assert.equal(result.certificate.checks.length, 4)
  for (const check of result.certificate.checks) {
    assert.equal(check.passed, true)
  }
  assert.equal(result.recommendation, 'stop')
})
test('WHAT[epistemic-reasoning-029] conservative-upper-voc-blocks-stopping-on-point-estimate-alone', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    checksSoFar: 2,
    alpha: 0.05,
    voc: { point: 0.01, upper: 0.5, threshold: 0.1 },
  })
  assert.equal(result.ok, true)
  assert.ok(result.voc.upper >= result.voc.point)
  assert.ok(Math.abs(result.voc.point - 0.01) < 1e-12)
  assert.ok(Math.abs(result.voc.upper - 0.5) < 1e-12)
  assert.equal(result.recommendation, 'continue')
})
}

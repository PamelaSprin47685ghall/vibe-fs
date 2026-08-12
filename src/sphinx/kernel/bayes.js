export function createBeliefState(hypotheses = []) {
  return {
    hypotheses: hypotheses.map(normalizeHypothesis),
    factors: [],
    entropy: posteriorEntropy(hypotheses),
  }
}

export function normalizeHypothesis(hypothesis) {
  return {
    id: hypothesis.id,
    semanticKey: hypothesis.semanticKey,
    label: hypothesis.label,
    method: hypothesis.method,
    prior: typeof hypothesis.prior === 'number' ? hypothesis.prior : null,
    likelihood: typeof hypothesis.likelihood === 'number' ? hypothesis.likelihood : null,
    posterior: typeof hypothesis.posterior === 'number' ? hypothesis.posterior : null,
    dependencies: Array.isArray(hypothesis.dependencies) ? [...hypothesis.dependencies] : [],
  }
}

export function uniformPrior(hypotheses) {
  if (!hypotheses.length) return []
  const prior = 1 / hypotheses.length
  return hypotheses.map((hypothesis) => ({
    ...normalizeHypothesis(hypothesis),
    prior,
    posterior: prior,
  }))
}

export function likelihoodForEvidence(hypothesis, evidence = {}) {
  if (typeof hypothesis.likelihood === 'number') return hypothesis.likelihood
  const supports = evidence.supports ?? []
  const refutes = evidence.refutes ?? []
  if (supports.includes(hypothesis.semanticKey)) return 0.92
  if (refutes.includes(hypothesis.semanticKey)) return 0.08
  return 0.5
}

export function updatePosteriors(hypotheses, evidence = {}) {
  if (!hypotheses.length) return []
  const seeded = hypotheses.every((h) => typeof h.prior === 'number')
    ? hypotheses
    : uniformPrior(hypotheses)
  const weighted = seeded.map((hypothesis) => {
    const prior = hypothesis.posterior ?? hypothesis.prior ?? 1 / seeded.length
    const likelihood = likelihoodForEvidence(hypothesis, evidence)
    return { ...hypothesis, prior, likelihood, weight: prior * likelihood }
  })
  const total = weighted.reduce((sum, row) => sum + row.weight, 0) || 1
  return weighted.map((row) => ({
    ...row,
    posterior: row.weight / total,
  }))
}

export function posteriorEntropy(hypotheses) {
  const rows = hypotheses.filter((h) => typeof (h.posterior ?? h.prior) === 'number')
  if (!rows.length) return 0
  return -rows.reduce((sum, row) => {
    const p = row.posterior ?? row.prior
    return p > 0 ? sum + p * Math.log2(p) : sum
  }, 0)
}

export function bayesRisk(hypotheses, lossByKey = {}) {
  return hypotheses.reduce((sum, hypothesis) => {
    const posterior = hypothesis.posterior ?? hypothesis.prior ?? 0
    const loss = lossByKey[hypothesis.semanticKey] ?? 1 - posterior
    return sum + posterior * loss
  }, 0)
}

export function expectedValueOfInformation(state, action) {
  const hypotheses = state.B?.hypotheses ?? []
  if (hypotheses.length < 2) return 0
  const before = posteriorEntropy(hypotheses)
  const simulated = updatePosteriors(hypotheses, {
    supports: action.semanticKey ? [action.semanticKey] : [],
  })
  const after = posteriorEntropy(simulated)
  const cost = Number(action.cost) || 1
  return Math.max(0, before - after) / cost
}

export function syncBayesianBelief(state) {
  const hypotheses = state.B?.hypotheses ?? []
  if (!hypotheses.length) {
    return {
      ...state,
      B: { ...state.B, belief: createBeliefState([]) },
    }
  }

  let current = hypotheses.every((h) => typeof h.prior === 'number')
    ? hypotheses.map(normalizeHypothesis)
    : uniformPrior(hypotheses)

  for (const item of state.E) {
    if (item.type !== 'Evidence' && item.type !== 'Candidates') continue
    const raw = item.raw ?? {}
    if (item.type === 'Evidence' || raw.supports || raw.refutes) {
      current = updatePosteriors(current, {
        supports: raw.supports ?? [],
        refutes: raw.refutes ?? [],
      })
    }
  }

  const factors = current.map((hypothesis) => ({
    key: hypothesis.semanticKey,
    prior: hypothesis.prior,
    likelihood: hypothesis.likelihood,
    posterior: hypothesis.posterior,
    dependsOn: hypothesis.dependencies,
  }))

  return {
    ...state,
    B: {
      ...state.B,
      hypotheses: current,
      belief: {
        hypotheses: current,
        factors,
        entropy: posteriorEntropy(current),
        risk: bayesRisk(current),
      },
    },
  }
}

export function frozenBayesianInference(hypotheses, evidenceSteps = []) {
  let current = uniformPrior(hypotheses)
  for (const evidence of evidenceSteps) {
    current = updatePosteriors(current, evidence)
  }
  return current
}

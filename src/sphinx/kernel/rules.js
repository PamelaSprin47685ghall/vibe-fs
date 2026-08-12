export const METHODS = [
  'Multidisciplinary',
  'Abduction',
  'Analogy',
  'Counterexample',
  'Synthesis',
]

const FORM_APPLICABILITY = {
  Multidisciplinary: { Why: 0.95, How: 0.7, What: 0.35, Polar: 0.25, Other: 0.3 },
  Abduction: { Why: 0.9, How: 0.55, Polar: 0.45, What: 0.3, Other: 0.35 },
  Analogy: { Polar: 0.85, Why: 0.45, How: 0.5, Which: 0.55, Other: 0.4 },
  Counterexample: { Polar: 0.8, Why: 0.55, Which: 0.5, How: 0.35, Other: 0.3 },
  Synthesis: { Why: 0.85, How: 0.8, What: 0.4, Polar: 0.5, Other: 0.45 },
}

const FACET_APPLICABILITY = {
  Multidisciplinary: { explanatory: 0.9, causal: 0.75, predictive: 0.2 },
  Abduction: { causal: 0.9, explanatory: 0.7, predictive: 0.35 },
  Analogy: { predictive: 0.85, comparative: 0.9, explanatory: 0.35 },
  Counterexample: { predictive: 0.55, comparative: 0.6, causal: 0.45 },
  Synthesis: { explanatory: 0.85, causal: 0.6, predictive: 0.4 },
}

const METHOD_COST = {
  Multidisciplinary: 1.1,
  Abduction: 1.0,
  Analogy: 0.9,
  Counterexample: 0.85,
  Synthesis: 1.2,
}

function formScore(method, formBelief) {
  if (!formBelief) return 0.2
  const table = FORM_APPLICABILITY[method] ?? {}
  let total = 0
  for (const [form, p] of Object.entries(formBelief)) {
    total += (Number(p) || 0) * (table[form] ?? 0.2)
  }
  return total
}

function facetScore(method, facets) {
  if (!facets) return 0.25
  const table = FACET_APPLICABILITY[method] ?? {}
  let weighted = 0
  let mass = 0
  for (const [facet, p] of Object.entries(facets)) {
    const w = Number(p) || 0
    mass += w
    weighted += w * (table[facet] ?? 0.25)
  }
  return mass > 0 ? weighted / mass : 0.25
}

export function methodUtility(method, state) {
  const formBelief = state.B.formBelief ?? state.R?.formBelief
  const facets = state.B.facets ?? state.R?.facets
  const applicability =
    0.55 * formScore(method, formBelief) + 0.45 * facetScore(method, facets)
  const expectedGain = applicability * (0.55 + 0.45 * (1 - Math.min(1, state.B.evidenceMass)))
  const hasCandidates = state.A.some((a) => a.kind === 'candidate' && a.method === method)
  const synthesisBonus =
    method === 'Synthesis' && state.A.filter((a) => a.kind === 'candidate').length >= 1
      ? 0.35
      : 0
  const candidatePenalty = method !== 'Synthesis' && hasCandidates ? 0.25 : 0
  return expectedGain + synthesisBonus - candidatePenalty - 0.15 * (METHOD_COST[method] ?? 1)
}

export function scoreMethods(state) {
  return METHODS.map((method) => ({
    method,
    utility: methodUtility(method, state),
  })).sort((a, b) => b.utility - a.utility)
}

export function activateMethods(state, threshold = 0.22) {
  const scored = scoreMethods(state)
  const activated = scored.filter((row) => row.utility >= threshold).map((row) => row.method)
  if (activated.length === 0 && scored[0]) activated.push(scored[0].method)
  return activated
}

function semanticKeyFor(method, label) {
  return `${method}:${String(label).trim().toLowerCase().replace(/\s+/g, ' ')}`
}

export function structuralCandidates(method, state) {
  const q = state.rootQuestion
  const templates = {
    Multidisciplinary: [
      `discipline lens for: ${q}`,
      `cross-domain mechanism for: ${q}`,
    ],
    Abduction: [
      `competing hypothesis for: ${q}`,
      `best explanation residual for: ${q}`,
    ],
    Analogy: [
      `structural analogy for: ${q}`,
      `transfer case for: ${q}`,
    ],
    Counterexample: [
      `falsifying case for: ${q}`,
      `boundary failure for: ${q}`,
    ],
    Synthesis: [`compose strands for: ${q}`],
  }
  const labels = templates[method] ?? [`explore: ${q}`]
  return labels.map((label, index) => ({
    id: `${method}-${index}`,
    kind: method === 'Synthesis' ? 'synthesize' : 'candidate',
    method,
    label,
    semanticKey: semanticKeyFor(method, label),
    provenance: `rule:${method}`,
    value: null,
    cost: METHOD_COST[method] ?? 1,
    novelty: 1,
  }))
}

export function generateFromRules(state) {
  const activated = activateMethods(state)
  const actions = []
  for (const method of activated) {
    for (const action of structuralCandidates(method, state)) {
      actions.push(action)
    }
  }
  return { activated, actions }
}

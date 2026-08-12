import { deriveRootContract } from './state.js'
import { generateFromRules } from './rules.js'
import { revalueActions } from './value.js'
import { syncSearchFrontier, reopenOnBeliefShift } from './search.js'
import { syncBayesianBelief } from './bayes.js'
import { syncMcts } from './mcts.js'
import { optimizeRepresentation } from './represent.js'

function observationType(observation) {
  if (!observation || typeof observation !== 'object') return null
  return observation.type ?? observation.kind ?? null
}

function normalizeText(value) {
  return String(value ?? '')
    .trim()
    .toLowerCase()
    .replace(/\s+/g, ' ')
}

export function semanticKeyOf(observation, fallback = '') {
  if (observation?.semanticKey) return normalizeText(observation.semanticKey)
  const type = observationType(observation)
  if (type === 'SemanticAssessment') {
    return `assessment:${JSON.stringify(observation.forms ?? {})}:${JSON.stringify(observation.facets ?? {})}`
  }
  if (type === 'Candidates') {
    const items = Array.isArray(observation.items) ? observation.items : []
    return `candidates:${items.map((item) => normalizeText(item.semanticKey ?? item.text ?? item.label ?? '')).join('|')}`
  }
  if (type === 'ValueEstimates') {
    return `values:${JSON.stringify(observation.estimates ?? [])}`
  }
  if (type === 'Synthesis') {
    return `synthesis:${normalizeText(observation.text ?? observation.content ?? '')}`
  }
  if (type === 'Evidence') {
    return `evidence:${JSON.stringify({ supports: observation.supports ?? [], refutes: observation.refutes ?? [] })}`
  }
  return normalizeText(fallback || JSON.stringify(observation ?? {}))
}

function fingerprint(state) {
  return JSON.stringify({
    mass: state.B.evidenceMass,
    methods: state.activatedMethods,
    keys: state.A.map((a) => a.semanticKey).sort(),
    values: state.A.map((a) => [a.id, a.value]),
    hyp: state.B.hypotheses.map((h) => h.semanticKey).sort(),
    synthesis: state.synthesis,
    R: state.R,
  })
}

function absorb(state, observation, exogenous) {
  if (!observation) return state
  const type = observationType(observation)
  const key = semanticKeyOf(observation)
  const duplicate = state.E.some((item) => item.semanticKey === key)
  const next = {
    ...state,
    C: { ...state.C, step: state.C.step + 1 },
    E: duplicate
      ? state.E
      : [...state.E, { type, semanticKey: key, exogenous: Boolean(exogenous), raw: observation }],
    D: duplicate
      ? state.D
      : [...state.D, { from: 'observation', to: key, via: type }],
  }

  if (duplicate) {
    return {
      ...next,
      B: { ...next.B },
    }
  }

  let evidenceMass = next.B.evidenceMass
  if (exogenous && type === 'SemanticAssessment') {
    evidenceMass = Math.min(1, evidenceMass + 0.2)
  }
  if (exogenous && type === 'Synthesis') {
    evidenceMass = Math.min(1, evidenceMass + 0.3)
  }

  if (type === 'SemanticAssessment') {
    const forms = observation.forms ?? observation.QuestionForm ?? {}
    const facets = observation.facets ?? observation.Facets ?? {}
    const R = deriveRootContract(forms, facets)
    return {
      ...next,
      R,
      B: {
        ...next.B,
        formBelief: { ...forms },
        facets: { ...facets },
        evidenceMass,
      },
    }
  }

  if (type === 'Candidates') {
    const items = Array.isArray(observation.items) ? observation.items : []
    const existingKeys = new Set(next.B.hypotheses.map((h) => h.semanticKey))
    const hypotheses = []
    let novelCount = 0
    for (const [index, item] of items.entries()) {
      const label = item.text ?? item.label ?? `candidate-${index}`
      const method = item.method ?? 'Abduction'
      const itemKey = normalizeText(item.semanticKey ?? `${method}:${label}`)
      const novel = !existingKeys.has(itemKey)
      if (novel) {
        novelCount += 1
        existingKeys.add(itemKey)
      }
      hypotheses.push({
        id: item.id ?? `obs-${method}-${index}`,
        kind: 'candidate',
        method,
        label,
        semanticKey: itemKey,
        provenance: 'llm:Candidates',
        llmValue: typeof item.value === 'number' ? item.value : null,
        cost: item.cost ?? 1,
        novelty: novel ? 1 : 0,
        prior: typeof item.prior === 'number' ? item.prior : null,
        likelihood: typeof item.likelihood === 'number' ? item.likelihood : null,
        posterior: typeof item.posterior === 'number' ? item.posterior : null,
        dependencies: Array.isArray(item.dependencies) ? item.dependencies : [],
      })
    }
    const massGain = exogenous ? Math.min(0.25, 0.08 * novelCount) : 0
    return {
      ...next,
      B: {
        ...next.B,
        hypotheses: [...next.B.hypotheses, ...hypotheses.filter((h) => h.novelty > 0)],
        evidenceMass: Math.min(1, next.B.evidenceMass + massGain),
      },
    }
  }

  if (type === 'ValueEstimates') {
    const estimates = Array.isArray(observation.estimates) ? observation.estimates : []
    const byId = new Map(
      estimates.map((row) => [row.actionId ?? row.id, Number(row.rootRelativeValue ?? row.value)]),
    )
    const A = next.A.map((action) =>
      byId.has(action.id) ? { ...action, llmValue: byId.get(action.id) } : action,
    )
    return {
      ...next,
      A,
      B: { ...next.B, evidenceMass },
    }
  }

  if (type === 'Synthesis') {
    const text = observation.text ?? observation.content ?? ''
    return {
      ...next,
      synthesis: {
        text,
        strands: observation.strands ?? [],
        semanticKey: key,
      },
      B: { ...next.B, evidenceMass },
    }
  }

  if (type === 'Evidence') {
    return {
      ...next,
      B: { ...next.B, evidenceMass: Math.min(1, evidenceMass + 0.1) },
    }
  }

  return next
}

function infer(state) {
  const fromRules = state.R ? generateFromRules(state) : { activated: [], actions: [] }
  const mergedHypotheses = [...state.B.hypotheses]
  for (const action of fromRules.actions) {
    if (action.kind !== 'candidate') continue
    if (mergedHypotheses.some((h) => h.semanticKey === action.semanticKey)) continue
    mergedHypotheses.push({
      id: action.id,
      semanticKey: action.semanticKey,
      method: action.method,
      label: action.label,
    })
  }
  return {
    ...state,
    activatedMethods: fromRules.activated,
    B: { ...state.B, hypotheses: mergedHypotheses },
    _ruleActions: fromRules.actions,
  }
}

function reduce(state) {
  const pool = []
  const seen = new Map()

  const consider = (action) => {
    const key = action.semanticKey
    const prev = seen.get(key)
    if (!prev) {
      seen.set(key, action)
      pool.push(action)
      return
    }
    const prevScore = (prev.value ?? 0) + (prev.novelty ?? 1)
    const nextScore = (action.value ?? 0) + (action.novelty ?? 1)
    if (nextScore > prevScore) {
      const index = pool.findIndex((row) => row.semanticKey === key)
      pool[index] = action
      seen.set(key, action)
    }
  }

  for (const action of state.A) consider(action)
  for (const action of state._ruleActions ?? []) consider(action)
  for (const hyp of state.B.hypotheses) {
    consider({
      id: hyp.id,
      kind: 'candidate',
      method: hyp.method,
      label: hyp.label,
      semanticKey: hyp.semanticKey,
      provenance: hyp.provenance ?? 'hypothesis',
      llmValue: hyp.llmValue ?? null,
      cost: hyp.cost ?? 1,
      novelty: hyp.novelty ?? 1,
    })
  }

  const contentSeen = new Set()
  const reduced = []
  for (const action of pool) {
    if (contentSeen.has(action.semanticKey)) {
      continue
    }
    contentSeen.add(action.semanticKey)
    if ((action.novelty ?? 1) <= 0 && action.kind !== 'synthesize') continue
    reduced.push(action)
  }

  const byMethodLabel = new Map()
  for (const action of reduced) {
    const key = action.semanticKey
    const prev = byMethodLabel.get(key)
    if (!prev) {
      byMethodLabel.set(key, action)
      continue
    }
    const better =
      (action.value ?? actionValueProxy(action)) > (prev.value ?? actionValueProxy(prev))
    if (better) byMethodLabel.set(key, action)
  }

  return {
    ...state,
    A: [...byMethodLabel.values()],
    _ruleActions: undefined,
  }
}

function actionValueProxy(action) {
  return (action.novelty ?? 1) - (action.cost ?? 1) * 0.1
}

function propagate(state) {
  if (!state.R || state.B.hypotheses.length === 0) return state
  const derived = state.B.hypotheses.map((hyp) => ({
    from: hyp.semanticKey,
    to: `belief:${hyp.semanticKey}`,
    via: 'propagation',
  }))
  const existing = new Set(state.D.map((d) => `${d.from}->${d.to}`))
  const additions = derived.filter((d) => !existing.has(`${d.from}->${d.to}`))
  return additions.length ? { ...state, D: [...state.D, ...additions] } : state
}

export function closure(state, observation = null, { exogenous = false } = {}) {
  const previousMass = state.B.evidenceMass
  let current = absorb(state, observation, exogenous)
  if (exogenous && observation) {
    current = reopenOnBeliefShift(current, previousMass)
  }
  let guard = 0
  while (guard++ < 24) {
    const before = fingerprint(current)
    current = infer(current)
    current = propagate(current)
    current = reduce(current)
    current = optimizeRepresentation(current)
    current = syncBayesianBelief(current)
    current = revalueActions(current)
    current = syncSearchFrontier(current)
    current = syncMcts(current)
    if (fingerprint(current) === before) break
  }
  return current
}

export function evidenceMassWithoutExogenous(state) {
  return closure({ ...state, E: [...state.E] }, null, { exogenous: false }).B.evidenceMass
}

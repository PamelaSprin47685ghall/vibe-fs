import { expectedValueOfInformation } from './bayes.js'

export function rootInformationGain(action, state) {
  const mass = state.B.evidenceMass
  const form = state.R?.primaryForm ?? 'Other'
  const methodBias = {
    Multidisciplinary: form === 'Why' || form === 'How' ? 0.85 : 0.45,
    Abduction: form === 'Why' ? 0.9 : 0.5,
    Analogy: form === 'Polar' ? 0.8 : 0.45,
    Counterexample: form === 'Polar' || mass > 0.3 ? 0.7 : 0.4,
    Synthesis: mass >= 0.35 || state.A.filter((a) => a.kind === 'candidate').length >= 2 ? 0.95 : 0.25,
  }
  const bias = methodBias[action.method] ?? 0.4
  const novelty = action.novelty == null ? 1 : action.novelty
  return bias * novelty * (0.4 + 0.6 * (1 - Math.min(1, mass)))
}

export function actionValue(action, state) {
  if (state.synthesis && action.kind === 'synthesize') return -1
  if (typeof action.llmValue === 'number') {
    return state.synthesis ? Math.min(action.llmValue, 0.35) : action.llmValue
  }
  const rootGain = rootInformationGain(action, state)
  if (action.kind === 'synthesize') {
    const strands = state.A.filter((a) => a.kind === 'candidate').length
    return strands >= 1 ? rootGain + 0.2 * Math.min(3, strands) - (action.cost ?? 1) * 0.2 : -0.2
  }
  const explored = rootGain - (action.cost ?? 1) * 0.2
  const evi = expectedValueOfInformation(state, action)
  const adjusted = explored + 0.2 * evi
  return state.synthesis ? Math.min(adjusted, 0.35) : adjusted
}

export function stopValue(state) {
  const mass = state.B.evidenceMass
  const hasSynthesis = Boolean(state.synthesis)
  const budgetPressure = state.C.yieldsUsed / Math.max(1, state.C.maxYields)
  const strandCount = state.A.filter((a) => a.kind === 'candidate').length
  let v = 0.05
  v += 0.45 * Math.min(1, mass)
  if (hasSynthesis) v += 1.1
  if (strandCount >= 2 && hasSynthesis) v += 0.15
  v += 0.5 * budgetPressure
  if (state.C.yieldsUsed >= state.C.maxYields) v = Number.POSITIVE_INFINITY
  return v
}

export function revalueActions(state) {
  const actions = state.A.map((action) => ({
    ...action,
    value: actionValue(action, state),
  }))
  return { ...state, A: actions }
}

export function bestActionValue(state) {
  if (!state.A.length) return Number.NEGATIVE_INFINITY
  return Math.max(...state.A.map((action) => actionValue(action, state)))
}

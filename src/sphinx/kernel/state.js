const MAX_YIELDS = 8

export function createEpistemicState(question) {
  return {
    rootQuestion: String(question ?? ''),
    R: null,
    B: {
      formBelief: null,
      facets: null,
      hypotheses: [],
      evidenceMass: 0,
      belief: null,
    },
    E: [],
    D: [],
    A: [],
    C: {
      maxYields: MAX_YIELDS,
      maxExploreSteps: 4,
      yieldsUsed: 0,
      step: 0,
    },
    activatedMethods: [],
    synthesis: null,
    lastRequestType: null,
    search: {
      bestG: {},
      closed: {},
      frontier: [],
      reopenCount: 0,
      exploreSteps: 0,
    },
    mcts: {
      nodes: {},
      rollouts: 0,
      transpositions: 0,
    },
    represent: {
      classes: {},
      pivots: [],
      factors: [],
    },
  }
}

export function cloneState(state) {
  return structuredClone(state)
}

export function primaryForm(formBelief) {
  if (!formBelief || typeof formBelief !== 'object') return 'Other'
  let best = 'Other'
  let score = -1
  for (const [form, p] of Object.entries(formBelief)) {
    const n = Number(p) || 0
    if (n > score) {
      score = n
      best = form
    }
  }
  return best
}

export function deriveRootContract(formBelief, facets) {
  const form = primaryForm(formBelief)
  const contractByForm = {
    Why: 'Explanation',
    How: 'Plan',
    What: 'Direct',
    Who: 'Direct',
    Where: 'Direct',
    When: 'Direct',
    Which: 'Ranking',
    Polar: 'Judgment',
    Other: 'Credence',
  }
  return {
    primaryForm: form,
    primaryContract: contractByForm[form] ?? 'Credence',
    formBelief: { ...formBelief },
    facets: { ...(facets ?? {}) },
  }
}

import { createEpistemicState } from './state.js'
import { closure } from './closure.js'
import { stopValue, bestActionValue } from './value.js'
import { canonicalAnswer, anytimeAnswer } from './answer.js'
import { orderActionsByFrontier, topFrontierAction, markExplored } from './search.js'

function observationType(observation) {
  if (!observation || typeof observation !== 'object') return null
  return observation.type ?? observation.kind ?? null
}

function yieldResult(state, request) {
  const bestAnswer = anytimeAnswer(state)
  return {
    state: {
      ...state,
      C: { ...state.C, yieldsUsed: state.C.yieldsUsed + 1 },
      lastRequestType: request.type,
    },
    result: {
      status: 'yield',
      request,
      ...(bestAnswer ? { bestAnswer } : {}),
    },
  }
}

function answeredResult(state, reason) {
  const answer = canonicalAnswer(state, reason)
  return {
    state: { ...state, answer },
    result: {
      status: 'answered',
      answer,
    },
  }
}

function generateCandidatesRequest(state) {
  return {
    type: 'GenerateCandidatesRequest',
    question: state.rootQuestion,
    methods: state.activatedMethods.filter((m) => m !== 'Synthesis'),
    contract: state.R,
    existingKeys: state.A.map((a) => a.semanticKey),
  }
}

function estimateValueRequest(state) {
  const actions = orderActionsByFrontier(
    state,
    state.A.map((a) => ({
      id: a.id,
      method: a.method,
      kind: a.kind,
      label: a.label,
      semanticKey: a.semanticKey,
    })),
  )
  return {
    type: 'EstimateValueRequest',
    question: state.rootQuestion,
    actions,
    frontierHead: topFrontierAction(state),
  }
}

function expandFrontierRequest(state, head) {
  return {
    type: 'ExpandFrontierRequest',
    question: state.rootQuestion,
    method: head.method,
    semanticKey: head.key,
    contract: state.R,
    priority: head.f,
    pathCost: head.g,
    rootGain: head.rootGain,
  }
}

function synthesizeRequest(state) {
  return {
    type: 'SynthesizeRequest',
    question: state.rootQuestion,
    contract: state.R,
    strands: state.A.filter((a) => a.kind === 'candidate').map((a) => ({
      method: a.method,
      label: a.label,
      semanticKey: a.semanticKey,
      value: a.value,
    })),
  }
}

function budgetExhausted(state) {
  return state.C.yieldsUsed >= state.C.maxYields
}

function stopDominates(state) {
  return stopValue(state) >= bestActionValue(state)
}

function decide(state) {
  if (budgetExhausted(state)) return answeredResult(state, 'budget')

  const hasCandidatesObs = state.E.some((e) => e.type === 'Candidates')
  const hasValueObs = state.E.some((e) => e.type === 'ValueEstimates')
  const hasSynthesis = Boolean(state.synthesis)

  if (!hasCandidatesObs) {
    return yieldResult(state, generateCandidatesRequest(state))
  }

  if (!hasValueObs) {
    return yieldResult(state, estimateValueRequest(state))
  }

  const allCandidatesValued = state.A.filter((action) => action.kind === 'candidate').every(
    (action) => typeof action.llmValue === 'number',
  )
  if (!hasSynthesis && state.activatedMethods.includes('Synthesis') && allCandidatesValued) {
    return yieldResult(state, synthesizeRequest(state))
  }

  const exploreCap = state.C.maxExploreSteps ?? 4
  const head = topFrontierAction(state)
  if (
    head &&
    (state.search?.exploreSteps ?? 0) < exploreCap &&
    !hasSynthesis &&
    !state.search?.closed?.[head.key] &&
    head.f > stopValue(state)
  ) {
    return yieldResult(markExplored(state, head.key), expandFrontierRequest(state, head))
  }

  const unvaluedCandidates = state.A.filter(
    (action) => action.kind === 'candidate' && typeof action.llmValue !== 'number',
  )
  if (unvaluedCandidates.length > 0) {
    return yieldResult(state, estimateValueRequest(state))
  }

  if (!hasSynthesis && state.activatedMethods.includes('Synthesis')) {
    return yieldResult(state, synthesizeRequest(state))
  }

  if (hasSynthesis || stopDominates(state)) {
    return answeredResult(state, hasSynthesis && stopDominates(state) ? 'stop-dominates' : 'assembled')
  }

  return answeredResult(state, 'policy-exhausted')
}

export function continueInquiry(state) {
  return decide(state)
}

export function startInquiry(question) {
  const text = String(question ?? '').trim()
  if (!text) {
    return {
      state: createEpistemicState(''),
      result: { status: 'error', error: 'question required' },
    }
  }
  const state = createEpistemicState(text)
  return yieldResult(state, {
    type: 'SemanticAssessmentRequest',
    question: text,
  })
}

export function resumeInquiry(state, observation) {
  if (!state || typeof state !== 'object') {
    return {
      state,
      result: { status: 'error', error: 'missing state' },
    }
  }
  if (!observation || typeof observation !== 'object' || Array.isArray(observation)) {
    return {
      state,
      result: { status: 'error', error: 'observation must be object' },
    }
  }
  const type = observationType(observation)
  if (!type) {
    return {
      state,
      result: { status: 'error', error: 'observation.type required' },
    }
  }

  const closed = closure(state, observation, { exogenous: true })
  return decide(closed)
}

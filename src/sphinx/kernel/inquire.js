import { createEpistemicState } from './state.js'
import { closure } from './closure.js'
import { stopValue, bestActionValue } from './value.js'
import { canonicalAnswer } from './answer.js'

function observationType(observation) {
  if (!observation || typeof observation !== 'object') return null
  return observation.type ?? observation.kind ?? null
}

function yieldResult(state, request) {
  return {
    state: {
      ...state,
      C: { ...state.C, yieldsUsed: state.C.yieldsUsed + 1 },
      lastRequestType: request.type,
    },
    result: {
      status: 'yield',
      request,
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
  return {
    type: 'EstimateValueRequest',
    question: state.rootQuestion,
    actions: state.A.map((a) => ({
      id: a.id,
      method: a.method,
      kind: a.kind,
      label: a.label,
      semanticKey: a.semanticKey,
    })),
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

  if (!hasSynthesis && state.activatedMethods.includes('Synthesis')) {
    return yieldResult(state, synthesizeRequest(state))
  }

  if (hasSynthesis || stopDominates(state)) {
    return answeredResult(state, hasSynthesis && stopDominates(state) ? 'stop-dominates' : 'assembled')
  }

  return answeredResult(state, 'policy-exhausted')
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

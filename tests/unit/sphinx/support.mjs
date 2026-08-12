import {
  SessionStore_$ctor,
  SessionStore__Resume_433E080,
  SessionStore__Start_Z721C83C5,
  SessionStore__TryState_Z721C83C5,
} from '../../../dist/Sphinx/Session.js'

export const createStore = () => SessionStore_$ctor()
export const start = (store, question) => SessionStore__Start_Z721C83C5(store, question)
export const resume = (store, handle, observation) =>
  SessionStore__Resume_433E080(store, handle, observation)
export const state = (store, handle) => SessionStore__TryState_Z721C83C5(store, handle)

export const assessWhy = (store, handle) =>
  resume(store, handle, {
    type: 'SemanticAssessment',
    forms: { Why: 0.8, How: 0.2 },
    facets: { causal: 0.9, explanatory: 1 },
  })

export const assessPolar = (store, handle) =>
  resume(store, handle, {
    type: 'SemanticAssessment',
    forms: { Polar: 0.95, Other: 0.05 },
    facets: { predictive: 1, evidence: 0.8 },
  })
